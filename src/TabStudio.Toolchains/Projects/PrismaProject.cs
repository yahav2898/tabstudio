using System.Text.Json;
using System.Text.RegularExpressions;
using TabStudio.Toolchains.Linux;

namespace TabStudio.Toolchains.Projects;

/// <summary>
/// A Node.js project that uses Prisma: its schema, the database its <c>datasource</c> names, and the steps that make
/// it runnable. <c>@prisma/client</c> must be generated from the schema before the app can query (otherwise "@prisma/client
/// did not initialize yet. Please run 'prisma generate'"), which Prisma's own install script does only when nothing
/// goes wrong, and quietly otherwise; so after <c>npm install</c> TabStudio runs <c>prisma generate</c> itself, with the
/// engine for the tablet's platform, and shows what it does.
/// </summary>
/// <param name="Directory">The project folder, with <c>package.json</c>.</param>
/// <param name="Schema">The schema, relative to the folder, such as <c>prisma/schema.prisma</c>.</param>
/// <param name="Provider">The datasource's provider, such as <c>postgresql</c>, <c>mysql</c> or <c>sqlite</c>.</param>
/// <param name="UrlVariable">The environment variable the datasource's <c>url</c> reads, such as <c>DATABASE_URL</c>.</param>
/// <param name="HasMigrations">Whether the schema's folder has a <c>migrations</c> folder, which <c>prisma migrate deploy</c> applies.</param>
public sealed partial record PrismaProject(string Directory, string Schema, string Provider, string? UrlVariable, bool HasMigrations)
{
    private const int MaxFileSize = 1024 * 1024;

    /// <summary>The Compose image of the database a provider needs, which TabStudio's Services run natively; none for SQLite.</summary>
    public string? DatabaseImage => Provider switch
    {
        "postgresql" or "postgres" or "cockroachdb" => "postgres:16",
        "mysql" => "mysql:8",
        _ => null,
    };

    /// <summary>The provider's name for people, such as PostgreSQL.</summary>
    public string DatabaseName => Provider switch
    {
        "postgresql" or "postgres" => "PostgreSQL",
        "cockroachdb" => "CockroachDB (PostgreSQL protocol)",
        "mysql" => "MySQL",
        "sqlite" => "SQLite",
        "sqlserver" => "SQL Server",
        "mongodb" => "MongoDB",
        _ => Provider,
    };

    /// <summary>
    /// Finds Prisma in a Node.js project: a schema at <c>package.json</c>'s <c>prisma.schema</c>, or at
    /// <c>prisma/schema.prisma</c> or <c>schema.prisma</c>, in a project that depends on <c>prisma</c> or <c>@prisma/client</c>.
    /// </summary>
    /// <param name="directory">The project folder.</param>
    /// <returns>The project, or <see langword="null"/> when it does not use Prisma.</returns>
    public static PrismaProject? Detect(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        var manifest = Path.Combine(directory, "package.json");
        if (ReadText(manifest) is not { } json)
        {
            return null;
        }

        string? configured = null;
        var usesPrisma = false;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = document.RootElement;
            foreach (var section in new[] { "dependencies", "devDependencies", "optionalDependencies" })
            {
                if (root.TryGetProperty(section, out var dependencies) && dependencies.ValueKind == JsonValueKind.Object
                    && (dependencies.TryGetProperty("prisma", out _) || dependencies.TryGetProperty("@prisma/client", out _)))
                {
                    usesPrisma = true;
                }
            }

            if (root.TryGetProperty("prisma", out var prisma) && prisma.ValueKind == JsonValueKind.Object
                && prisma.TryGetProperty("schema", out var schema) && schema.ValueKind == JsonValueKind.String)
            {
                configured = schema.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        var candidates = configured is { Length: > 0 } ? [configured] : new[] { "prisma/schema.prisma", "schema.prisma" };
        var relative = candidates.FirstOrDefault(c => IsInside(directory, c) && File.Exists(Path.Combine(directory, c.Replace('/', Path.DirectorySeparatorChar))));
        if (relative is null || !usesPrisma)
        {
            return null;
        }

        var schemaPath = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
        var text = ReadText(schemaPath) ?? string.Empty;
        var datasource = Datasource().Match(text);
        var provider = datasource.Success && ProviderSetting().Match(datasource.Value) is { Success: true } p ? p.Groups["provider"].Value : "unknown";
        var url = datasource.Success && UrlEnvironment().Match(datasource.Value) is { Success: true } u ? u.Groups["variable"].Value : null;
        var migrations = System.IO.Directory.Exists(Path.Combine(Path.GetDirectoryName(schemaPath)!, "migrations"));
        return new PrismaProject(directory, relative.Replace('\\', '/'), provider, url, migrations);
    }

    /// <summary>
    /// The Prisma engine target of the device a project runs on, for <c>PRISMA_CLI_BINARY_TARGETS</c>: Alpine Linux's musl
    /// with OpenSSL 3 inside TabStudio's Linux environment on a tablet (<c>linux-musl-arm64-openssl-3.0.x</c> on ARM64),
    /// or <see langword="null"/> on a computer, where Prisma detects its own.
    /// </summary>
    /// <param name="platform">The device.</param>
    public static string? EngineTarget(ToolchainPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        if (!platform.UsesLinuxEnvironment)
        {
            return null;
        }

        return platform.Architecture switch
        {
            "aarch64" or "arm64" => "linux-musl-arm64-openssl-3.0.x",
            "x86_64" or "x64" => "linux-musl-openssl-3.0.x",
            _ => null,
        };
    }

    /// <summary>
    /// <c>npx --no-install prisma generate</c> with the project's schema: the project's own Prisma, never one downloaded on
    /// the fly.
    /// </summary>
    public IReadOnlyList<string> GenerateCommand => ["npx", "--no-install", "prisma", "generate", "--schema", Schema];

    /// <summary><c>npx --no-install prisma migrate deploy</c>, which creates the tables from the schema's migrations.</summary>
    public IReadOnlyList<string> MigrateCommand => ["npx", "--no-install", "prisma", "migrate", "deploy", "--schema", Schema];

    /// <summary>The environment Prisma's commands run with on a device: the engine target, and no update notices or telemetry.</summary>
    /// <param name="engineTarget">The device's engine target (see <see cref="EngineTarget"/>), or <see langword="null"/>.</param>
    public static IReadOnlyDictionary<string, string> Environment(string? engineTarget)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PRISMA_HIDE_UPDATE_MESSAGE"] = "1",
            ["CHECKPOINT_DISABLE"] = "1",
        };
        if (engineTarget is not null)
        {
            environment["PRISMA_CLI_BINARY_TARGETS"] = engineTarget;
        }

        return environment;
    }

    /// <summary>
    /// The connection URL for a database TabStudio runs for the project: PostgreSQL's or MySQL's on a local port, with
    /// the user, password and database the service is started with; SQLite's file beside the schema.
    /// </summary>
    /// <param name="port">The port the database listens on.</param>
    /// <param name="user">The database user.</param>
    /// <param name="password">The user's password.</param>
    /// <param name="database">The database.</param>
    public string ConnectionUrl(int port, string user, string password, string database) => Provider switch
    {
        "postgresql" or "postgres" or "cockroachdb" => $"postgresql://{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(password)}@127.0.0.1:{port}/{Uri.EscapeDataString(database)}?schema=public",
        "mysql" => $"mysql://{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(password)}@127.0.0.1:{port}/{Uri.EscapeDataString(database)}",
        "sqlite" => "file:./dev.db",
        _ => throw new NotSupportedException($"TabStudio does not run {DatabaseName} databases for Prisma."),
    };

    /// <summary>
    /// Writes the connection URL into the project's <c>.env</c> when it has no value there yet, as Prisma and the app
    /// read it; a value the project already has is kept, unless <paramref name="replaceable"/> says it is an earlier
    /// one TabStudio wrote.
    /// </summary>
    /// <param name="url">The connection URL.</param>
    /// <param name="replaceable">Whether a value already there may be replaced.</param>
    /// <returns>Whether the file was changed.</returns>
    public bool FillEnvironmentFile(string url, Func<string, bool>? replaceable = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        if (UrlVariable is not { } variable)
        {
            return false;
        }

        var path = Path.Combine(Directory, ".env");
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
        var index = lines.FindIndex(line => line.TrimStart().StartsWith(variable + "=", StringComparison.Ordinal));
        var current = index >= 0 ? lines[index].Trim()[(variable.Length + 1)..].Trim().Trim('"', '\'') : string.Empty;
        if (current == url || (current.Length > 0 && replaceable?.Invoke(current) != true))
        {
            return false;
        }

        var line = $"{variable}=\"{url}\"";
        if (index >= 0)
        {
            lines[index] = line;
        }
        else
        {
            lines.Add(line);
        }

        File.WriteAllLines(path, lines);
        return true;
    }

    /// <summary>The value the project's <c>.env</c> gives the datasource's variable, if any.</summary>
    public string? EnvironmentFileUrl()
    {
        if (UrlVariable is not { } variable || ReadText(Path.Combine(Directory, ".env")) is not { } text)
        {
            return null;
        }

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(variable + "=", StringComparison.Ordinal))
            {
                var value = trimmed[(variable.Length + 1)..].Trim().Trim('"', '\'');
                return value.Length > 0 ? value : null;
            }
        }

        return null;
    }

    private static bool IsInside(string directory, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(directory, relative));
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.Ordinal);
    }

    private static string? ReadText(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length <= MaxFileSize ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"datasource\s+\w+\s*\{[^}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex Datasource();

    [GeneratedRegex(@"provider\s*=\s*""(?<provider>[a-z]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex ProviderSetting();

    [GeneratedRegex(@"url\s*=\s*env\(\s*""(?<variable>[A-Za-z_][A-Za-z0-9_]*)""\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex UrlEnvironment();
}
