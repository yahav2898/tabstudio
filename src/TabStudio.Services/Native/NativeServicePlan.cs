using System.Globalization;
using System.Text.RegularExpressions;
using TabStudio.Services.Compose;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Services.Native;

/// <summary>A foreground server, initialization and a graceful stop, all in the same PRoot mounts.</summary>
public sealed record NativeServicePlan(ProcessStartSpec Start, ProcessStartSpec Stop, ProcessStartSpec Probe, IReadOnlyList<ServicePort> Ports, string DataDirectory)
{
    public const string GuestDirectory = "/tabstudio-service";

    public static NativeServicePlan Create(ComposeService service, ServiceMapping mapping, ServiceStorage storage, IReadOnlyList<ServicePort> ports, Func<string, string>? rewrite = null, Func<string, string, string>? rewriteSetting = null)
    {
        var mounts = storage.Prepare(service);
        var directory = storage.ServiceDirectory(service.Name);
        var environment = new Dictionary<string, string?>(service.Environment, StringComparer.Ordinal);
        rewrite ??= x => x;
        // A setting is rewritten knowing its name, so the server's own POSTGRES_USER=postgres is not taken for a host.
        var setting = rewriteSetting ?? ((_, value) => rewrite(value));
        foreach (var key in environment.Keys.ToArray()) if (environment[key] is { } value) environment[key] = setting(key, value);
        environment["TS_SERVICE"] = GuestDirectory;
        environment["TMPDIR"] = GuestDirectory + "/tmp";
        Directory.CreateDirectory(Path.Combine(directory, "tmp"));
        var data = DataPath(service, mapping.Kind);
        string At(int target) => ports.First(p => p.Target == target).Published.ToString(CultureInfo.InvariantCulture);
        string E(string key, string fallback) => environment.GetValueOrDefault(key) is { Length: > 0 } value ? value : fallback;
        string[] args;
        string initialize = "";
        string stop = "if [ -s \"$TS_SERVICE/pid\" ]; then kill -TERM \"$(cat \"$TS_SERVICE/pid\")\" 2>/dev/null || true; fi";
        string[] probe;
        int? uid = null;
        switch (mapping.Kind)
        {
            case NativeServiceKind.Postgres:
                uid = 70;
                environment["PGDATA"] = environment.GetValueOrDefault("PGDATA") ?? data;
                data = environment["PGDATA"]!;
                environment["POSTGRES_USER"] = E("POSTGRES_USER", "postgres");
                environment["POSTGRES_DB"] = E("POSTGRES_DB", environment["POSTGRES_USER"]!);
                environment["PGUSER"] = environment["POSTGRES_USER"];
                environment["PGDATABASE"] = environment["POSTGRES_DB"];
                environment["PGPASSWORD"] = environment.GetValueOrDefault("POSTGRES_PASSWORD");
                environment["PGHOST"] = "127.0.0.1";
                environment["PGPORT"] = At(5432);
                if (string.IsNullOrEmpty(environment.GetValueOrDefault("POSTGRES_PASSWORD")))
                    throw new ComposeException($"{service.Name}: POSTGRES_PASSWORD is required for the native PostgreSQL server.");
                var pgBin = mapping.Packages[0]["postgresql".Length..];
                environment["PATH"] = $"/usr/lib/postgresql{pgBin}/bin:$PATH";
                args = ["postgres", "-D", data, "-p", At(5432), "-h", "127.0.0.1", "-k", GuestDirectory + "/tmp", "-c", "shared_memory_type=mmap", "-c", "dynamic_shared_memory_type=posix", "-c", "max_connections=30", "-c", "shared_buffers=16MB"];
                initialize = PostgresInitialization;
                stop = "pg_ctl -D \"$PGDATA\" -m fast -w -t 10 stop";
                probe = ["psql", "-X", "-w", "-h", "127.0.0.1", "-p", At(5432), "-U", environment["POSTGRES_USER"]!, "-d", environment["POSTGRES_DB"]!, "-Atqc", "SELECT 1"];
                break;
            case NativeServiceKind.Redis or NativeServiceKind.Valkey:
                var cli = mapping.Kind == NativeServiceKind.Redis ? "redis-cli" : "valkey-cli";
                args = [mapping.Program, "--bind", "127.0.0.1", "--port", At(6379), "--dir", data, "--daemonize", "no"];
                var password = environment.GetValueOrDefault("REDIS_PASSWORD") ?? environment.GetValueOrDefault("VALKEY_PASSWORD");
                if (!string.IsNullOrEmpty(password)) { args = [.. args, "--requirepass", password]; environment["REDISCLI_AUTH"] = password; }
                stop = $"{cli} -h 127.0.0.1 -p {At(6379)} shutdown";
                probe = [cli, "-h", "127.0.0.1", "-p", At(6379), "ping"];
                break;
            case NativeServiceKind.MariaDb:
                environment["MYSQL_DATABASE"] = E("MARIADB_DATABASE", E("MYSQL_DATABASE", "app"));
                environment["MYSQL_USER"] = E("MARIADB_USER", E("MYSQL_USER", "app"));
                environment["MYSQL_PASSWORD"] = E("MARIADB_PASSWORD", E("MYSQL_PASSWORD", ""));
                environment["MYSQL_ROOT_PASSWORD"] = E("MARIADB_ROOT_PASSWORD", E("MYSQL_ROOT_PASSWORD", ""));
                // As the images do, an empty root password is allowed only when the Compose file says so explicitly.
                var allowEmpty = E("MARIADB_ALLOW_EMPTY_ROOT_PASSWORD", E("MYSQL_ALLOW_EMPTY_PASSWORD", "")) is { Length: > 0 } allow && allow is not ("0" or "false" or "no");
                if (string.IsNullOrEmpty(environment["MYSQL_ROOT_PASSWORD"]) && !allowEmpty) throw new ComposeException($"{service.Name}: MYSQL_ROOT_PASSWORD or MARIADB_ROOT_PASSWORD is required, or MYSQL_ALLOW_EMPTY_PASSWORD for an empty one.");
                environment["TS_DATA"] = data;
                initialize = MariaDbInitialization;
                args = [mapping.Program, "--user=root", "--datadir=" + data, "--bind-address=127.0.0.1", "--port=" + At(3306), "--socket=" + GuestDirectory + "/mysql.sock", "--pid-file=" + GuestDirectory + "/mysql.pid", "--log-error=/dev/stderr"];
                stop = "kill -TERM \"$(cat \"$TS_SERVICE/mysql.pid\")\"";
                probe = ["mariadb-admin", "--socket=" + GuestDirectory + "/mysql.sock", "ping"];
                break;
            case NativeServiceKind.MongoDb:
                args = [mapping.Program, "--bind_ip", "127.0.0.1", "--port", At(27017), "--dbpath", data];
                if (environment.Keys.Any(k => k.StartsWith("MONGO_INITDB_", StringComparison.Ordinal))) throw new ComposeException("MongoDB image initialization variables require a remote host; Alpine's server does not supply the image entrypoint.");
                probe = ["/bin/true"];
                break;
            case NativeServiceKind.RabbitMq:
                environment["RABBITMQ_MNESIA_BASE"] = data;
                environment["RABBITMQ_LOGS"] = "-";
                environment["RABBITMQ_SASL_LOGS"] = "-";
                environment["HOME"] = GuestDirectory;
                environment["RABBITMQ_NODENAME"] = "ts_" + service.Name.Replace('-', '_') + "@localhost";
                environment["RABBITMQ_NODE_PORT"] = At(5672);
                environment["ERL_EPMD_PORT"] = new LocalServiceNetwork().FreePort().ToString(CultureInfo.InvariantCulture);
                environment["ERL_EPMD_ADDRESS"] = "127.0.0.1";
                environment["RABBITMQ_DIST_PORT"] = new LocalServiceNetwork().FreePort().ToString(CultureInfo.InvariantCulture);
                environment["RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS"] = "+S 2:2 +sbwt none -kernel inet_dist_use_interface {127,0,0,1}";
                environment["RABBITMQ_CONFIG_FILE"] = GuestDirectory + "/rabbitmq";
                environment["RABBITMQ_ENABLED_PLUGINS_FILE"] = GuestDirectory + "/enabled_plugins";
                File.WriteAllText(Path.Combine(directory, "rabbitmq.conf"), $"listeners.tcp.1 = 127.0.0.1:{At(5672)}\n" + (ports.Any(p => p.Target == 15672) ? $"management.tcp.ip = 127.0.0.1\nmanagement.tcp.port = {At(15672)}\n" : ""));
                File.WriteAllText(Path.Combine(directory, "enabled_plugins"), ports.Any(p => p.Target == 15672) ? "[rabbitmq_management].\n" : "[].\n");
                if (environment.GetValueOrDefault("RABBITMQ_DEFAULT_USER") is { Length: > 0 } rabbitUser)
                {
                    var rabbitPassword = environment.GetValueOrDefault("RABBITMQ_DEFAULT_PASS") ?? "";
                    if (rabbitUser.Contains('\n') || rabbitPassword.Contains('\n')) throw new ComposeException("RabbitMQ credentials must be single-line values.");
                    File.AppendAllText(Path.Combine(directory, "rabbitmq.conf"), $"default_user = {rabbitUser}\ndefault_pass = {rabbitPassword}\n");
                }
                args = [mapping.Program];
                probe = ["rabbitmq-diagnostics", "-q", "check_running"];
                stop = "rabbitmqctl stop";
                break;
            case NativeServiceKind.Nginx:
                Directory.CreateDirectory(Path.Combine(directory, "www"));
                File.WriteAllText(Path.Combine(directory, "www", "index.html"), "TabStudio service is running.\n");
                var config = service.Volumes.FirstOrDefault(v => v.Target == "/etc/nginx/nginx.conf");
                var includes = service.Volumes.Any(v => v.Target.StartsWith("/etc/nginx/conf.d", StringComparison.Ordinal)) ? "include /etc/nginx/conf.d/*.conf;" : $"server {{ listen 127.0.0.1:{At(80)}; root {(service.Volumes.Any(v => v.Target == "/usr/share/nginx/html") ? "/usr/share/nginx/html" : GuestDirectory + "/www")}; }}";
                foreach (var mount in mounts.Skip(1))
                {
                    var configurations = File.Exists(mount.Source) && mount.Target.EndsWith(".conf", StringComparison.Ordinal) ? new[] { mount.Source }
                        : Directory.Exists(mount.Source) && mount.Target.StartsWith("/etc/nginx", StringComparison.Ordinal) ? Directory.EnumerateFiles(mount.Source, "*.conf", SearchOption.AllDirectories) : [];
                    foreach (var path in configurations)
                    {
                        var text = rewrite(File.ReadAllText(path));
                        text = Regex.Replace(text, @"\blisten\s+(?:(?:[0-9.]+|\[::\]):)?80(?=[\s;])", "listen 127.0.0.1:" + At(80), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                        File.WriteAllText(path, text);
                    }
                }
                File.WriteAllText(Path.Combine(directory, "nginx.conf"), $"pid {GuestDirectory}/nginx.pid; error_log /dev/stderr; events {{ worker_connections 128; }} http {{ access_log /dev/stdout; client_body_temp_path {GuestDirectory}/tmp; proxy_temp_path {GuestDirectory}/tmp; fastcgi_temp_path {GuestDirectory}/tmp; uwsgi_temp_path {GuestDirectory}/tmp; scgi_temp_path {GuestDirectory}/tmp; {includes} }}");
                args = [mapping.Program, "-p", GuestDirectory + "/", "-c", config is null ? GuestDirectory + "/nginx.conf" : config.Target, "-g", "daemon off;"];
                stop = "kill -QUIT \"$(cat \"$TS_SERVICE/nginx.pid\")\"";
                probe = ["/bin/true"];
                break;
            case NativeServiceKind.Mailpit:
                args = [mapping.Program, "--listen", "127.0.0.1:" + At(8025), "--smtp", "127.0.0.1:" + At(1025), "--database", data + "/mailpit.db"];
                probe = ["/bin/true"];
                break;
            case NativeServiceKind.Minio:
                args = [mapping.Program, "server", data, "--address", "127.0.0.1:" + At(9000), "--console-address", "127.0.0.1:" + At(9001)];
                probe = ["/bin/true"];
                break;
            default: throw new ArgumentOutOfRangeException(nameof(mapping));
        }
        if (service.Command is { Count: > 0 } command)
        {
            // An image's executable (or just its options) keeps the managed port/data flags. An explicit shell is run verbatim.
            var supplied = command.Select(rewrite).ToArray();
            if (Path.GetFileName(supplied[0]) == mapping.Program || supplied[0].StartsWith('-'))
            {
                var options = Path.GetFileName(supplied[0]) == mapping.Program ? supplied[1..] : supplied;
                args = [args[0], .. options, .. args.Skip(1)];
            }
            else if (mapping.Kind == NativeServiceKind.Minio && supplied[0] == "server")
            {
                var custom = new List<string> { mapping.Program, "server" };
                for (var i = 1; i < supplied.Length; i++)
                {
                    if (supplied[i] is "--address" or "--console-address") { i++; continue; }
                    if (supplied[i].StartsWith("--address=", StringComparison.Ordinal) || supplied[i].StartsWith("--console-address=", StringComparison.Ordinal)) continue;
                    custom.Add(supplied[i] == "/data" ? data : supplied[i]);
                }
                args = [.. custom, .. args.Skip(3)];
            }
            else
            {
                args = supplied;
            }
        }
        environment["TS_DATA"] = data;
        var script = "#!/bin/sh\nset -eu\numask 077\nmkdir -p \"$TS_DATA\" \"$TS_SERVICE/tmp\"\n" + initialize + "\n" +
            "rm -f \"$TS_SERVICE/pid\"\n\"$@\" &\nchild=$!\nprintf '%s\\n' \"$child\" > \"$TS_SERVICE/pid\"\ntrap 'kill -TERM \"$child\" 2>/dev/null || true; wait \"$child\" || true' TERM INT\nwait \"$child\"\n";
        File.WriteAllText(Path.Combine(directory, "run.sh"), script);
        var basis = new ProcessStartSpec { Program = "/bin/sh", WorkingDirectory = directory, Environment = environment, Mounts = mounts, UserId = uid };
        var start = basis with { Arguments = [GuestDirectory + "/run.sh", .. args] };
        var health = service.Healthcheck;
        string RewriteHealth(string text)
        {
            text = rewrite(text);
            foreach (var port in ports)
            {
                text = Regex.Replace(text, @"(?<=localhost:|127\.0\.0\.1:)" + port.Target + @"(?=\D|$)", port.Published.ToString(CultureInfo.InvariantCulture), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                text = Regex.Replace(text, @"(?<option>(?:^|\s)(?:-p|--port)[= ]+)" + port.Target + @"(?=\s|$)", m => m.Groups["option"].Value + port.Published, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            }
            if (mapping.Kind is NativeServiceKind.Redis or NativeServiceKind.Valkey)
                text = Regex.Replace(text, @"\b(redis-cli|valkey-cli)\b", "$1 -h 127.0.0.1 -p " + At(6379), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            return text;
        }

        if (health is { Disabled: false, Test.Count: > 0 })
        {
            probe = health.Test[0] switch
            {
                "CMD" => health.Test.Skip(1).Select(rewrite).ToArray(),
                "CMD-SHELL" => ["/bin/sh", "-c", RewriteHealth(string.Join(' ', health.Test.Skip(1)))],
                _ => throw new ComposeException($"{service.Name}: healthcheck must use CMD or CMD-SHELL."),
            };
        }
        if (health is { Disabled: false, Test.Count: > 1 } && health.Test[0] == "CMD")
        {
            var probeArgs = probe.ToList();
            for (var i = 1; i + 1 < probeArgs.Count; i++)
                if (probeArgs[i] is "-p" or "--port" && int.TryParse(probeArgs[i + 1], out var target) && ports.FirstOrDefault(p => p.Target == target) is { } port) probeArgs[i + 1] = port.Published.ToString(CultureInfo.InvariantCulture);
            if (Path.GetFileName(probeArgs[0]) is "redis-cli" or "valkey-cli") probeArgs.InsertRange(1, ["-h", "127.0.0.1", "-p", At(6379)]);
            probe = probeArgs.ToArray();
        }
        return new(start, basis with { Arguments = ["-c", stop] }, basis with { Program = probe[0], Arguments = probe.Skip(1).ToArray() }, ports, data);
    }

    private static string DataPath(ComposeService service, NativeServiceKind kind)
    {
        var expected = kind switch
        {
            NativeServiceKind.Postgres => "/var/lib/postgresql/data",
            NativeServiceKind.Redis or NativeServiceKind.Valkey or NativeServiceKind.Minio => "/data",
            NativeServiceKind.MariaDb => "/var/lib/mysql",
            NativeServiceKind.MongoDb => "/data/db",
            NativeServiceKind.RabbitMq => "/var/lib/rabbitmq",
            _ => GuestDirectory + "/data",
        };
        return service.Volumes.Any(v => expected == v.Target || expected.StartsWith(v.Target.TrimEnd('/') + "/", StringComparison.Ordinal)) ? expected : GuestDirectory + "/data";
    }

    private const string PostgresInitialization = """
        if [ ! -f "$TS_SERVICE/initialized" ]; then
          if [ -f "$PGDATA/PG_VERSION" ]; then
            echo 'A previous PostgreSQL initialization did not finish. Inspect Services logs and repair or move its data directory before retrying.' >&2
            exit 1
          fi
          printf '%s\n' "$POSTGRES_PASSWORD" > "$TS_SERVICE/password"
          trap 'rm -f "$TS_SERVICE/password"; pg_ctl -D "$PGDATA" -m immediate stop >/dev/null 2>&1 || true' EXIT
          initdb -D "$PGDATA" -U "$POSTGRES_USER" --pwfile="$TS_SERVICE/password" --auth-local=trust --auth-host=scram-sha-256 --encoding=UTF8 --locale=C
          rm -f "$TS_SERVICE/password"
          pg_ctl -D "$PGDATA" -o "-h '' -k $TS_SERVICE/tmp -p $PGPORT -c shared_memory_type=mmap -c dynamic_shared_memory_type=posix -c shared_buffers=16MB" -w -t 60 start
          export PGHOST="$TS_SERVICE/tmp"
          if [ "$POSTGRES_DB" != postgres ]; then
            createdb -w -U "$POSTGRES_USER" -- "$POSTGRES_DB"
          fi
          for f in /docker-entrypoint-initdb.d/*; do
            [ -f "$f" ] || continue
            case "$f" in
              *.sql) psql -X -w -v ON_ERROR_STOP=1 -f "$f" ;;
              *.sql.gz) gzip -dc "$f" > "$TS_SERVICE/init.sql"; psql -X -w -v ON_ERROR_STOP=1 -f "$TS_SERVICE/init.sql"; rm -f "$TS_SERVICE/init.sql" ;;
              *.sh) . "$f" ;;
              *) echo "Ignoring init script: $f" ;;
            esac
          done
          pg_ctl -D "$PGDATA" -m fast -w -t 30 stop
          export PGHOST=127.0.0.1
          touch "$TS_SERVICE/initialized"
          trap - EXIT
        fi
        """;

    // Initialization SQL is generated from environment variables by psql's MariaDB counterpart below;
    // escaping is done inside the shell, so passwords never appear in process arguments or logs.
    private const string MariaDbInitialization = """
        if [ ! -d "$TS_DATA/mysql" ]; then
          mariadb-install-db --user=root --datadir="$TS_DATA" --auth-root-authentication-method=normal --skip-test-db
          mariadbd --user=root --datadir="$TS_DATA" --skip-networking --socket="$TS_SERVICE/mysql.sock" --pid-file="$TS_SERVICE/mysql.pid" &
          initpid=$!
          trap 'kill -TERM "$initpid" 2>/dev/null || true; wait "$initpid" || true' EXIT
          n=0
          until mariadb-admin --socket="$TS_SERVICE/mysql.sock" ping >/dev/null 2>&1; do
            n=$((n+1)); [ "$n" -lt 60 ] || exit 1; sleep 1
          done
          escaped() { printf '%s' "$1" | sed "s/'/''/g"; }
          db=$(printf '%s' "$MYSQL_DATABASE" | sed 's/`/``/g')
          user=$(escaped "$MYSQL_USER")
          pass=$(escaped "$MYSQL_PASSWORD")
          rootpass=$(escaped "$MYSQL_ROOT_PASSWORD")
          printf "SET sql_mode='NO_BACKSLASH_ESCAPES'; CREATE DATABASE IF NOT EXISTS \140%s\140; CREATE USER IF NOT EXISTS '%s'@'127.0.0.1' IDENTIFIED BY '%s'; GRANT ALL ON \140%s\140.* TO '%s'@'127.0.0.1'; ALTER USER 'root'@'localhost' IDENTIFIED BY '%s';\n" "$db" "$user" "$pass" "$db" "$user" "$rootpass" | mariadb --socket="$TS_SERVICE/mysql.sock"
          kill -TERM "$initpid"
          wait "$initpid"
          trap - EXIT
        fi
        """;
}
