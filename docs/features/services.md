# Services

Open **View > Services**, or search **Services** in Ctrl+Q. Opening a repository only reads its configuration: nothing
is downloaded or started. The window offers each service with **Start**; Start all installs the mapped packages and
starts the servers, and F5 starts only the services the run points at (see [F5 and connections](#f5-and-connections)).
Every service has start, stop, restart, state, ports and its own bounded log. A port opens in the web preview. Reload
stops the group and rereads Compose; data remains.

Errors stay on their service. A setting one service uses that TabStudio cannot run, such as a `tmpfs` volume or an
invalid port, marks that service **Not supported here** with the reason, and the others are listed and run as usual.
A service that fails to start shows its error on its row, with a summary over the list; it never stops the services
that do not depend on it, and a service that needs it says so. "The Compose file could not be read" means only that:
invalid YAML, or a file that cannot be read.

## Images and packages

| Compose image | Alpine package | Notes |
| --- | --- | --- |
| `postgres:<major>` | `postgresql<major>`, `postgresql<major>-client` | Major and requested version checked; unavailable versions require an explicit Compose change or remote host. |
| `redis` | `redis`, or `valkey` when Redis is not packaged | The substitution and version difference are displayed. |
| `valkey/valkey` | `valkey` | Redis protocol. |
| `mysql`, `mariadb` | `mariadb`, `mariadb-client` | MySQL is provided by MariaDB; differences are reported. |
| `mongo`, `mongodb/mongodb-community-server` | `mongodb`, `mongodb-tools` | Unsupported when absent from the selected Alpine release/architecture. Mongo image auth/init variables require a remote host. |
| `rabbitmq` (including `-management`) | `rabbitmq-server` | Own node name, data, config and management port. |
| `nginx` | `nginx` | Foreground server; generated default site or copied custom configuration. |
| `axllent/mailpit` | `mailpit` | SMTP and web UI, persistent message database. |
| `minio/minio`, `quay.io/minio/minio` | `minio` | Object API and console with independent ports and data. |

`docker.io/library/` official aliases are accepted. Arbitrary registries/custom images and pinned image digests are
not treated as equivalent to official packages. Mapping is checked against the signed package index for the current
architecture before install. Apart from PostgreSQL, versions unavailable in that index use its packaged version with
an explicit log note; image tags cannot always be reproduced by an Alpine package. No repository or rootfs is added
to obtain an obsolete version. A required unsupported dependency blocks F5 with its reason; unsupported unrelated
images remain visible and do not block supported services. The remote-host action opens the existing bridge pane and
explains the available Link/SSH route; this feature does not provision a remote Docker host.

## Compose and devcontainers

Discovery prefers `compose.yaml`, `compose.yml`, then `docker-compose.yml` / `docker-compose.yaml`, and reads the
matching `.override` file. `.devcontainer/devcontainer.json` can name one or several `dockerComposeFile` paths relative
to that JSON file; comments/trailing commas, `service` and `runServices` are accepted. Paths in the merged model are
relative to the first Compose file. Scalar/command overrides replace, environment/health/dependency maps merge,
volumes replace by target, and port/env-file sequences append.

Supported inputs include map/list environment, `.env` and `env_file`, `${NAME}`, defaults/required/alternative forms,
`$$` shell escaping, YAML anchors and merge keys, TCP short/long ports and bounded ranges, named/anonymous/bind
volumes, command argv or quoted command strings, short/long `depends_on`, and CMD/CMD-SHELL health checks. Shell
commands execute only when explicitly requested by the Compose command/healthcheck or an initialization `.sh` file.
A normal command string is tokenized as argv, not evaluated as shell code.

No kernel namespaces or Docker network exist. All services bind loopback. Their listen flags use their published
ports; implicit conflicts and ports below 1024 choose an available unprivileged port, while an explicit occupied port
fails clearly. Different services can use the same target port because their actual listen ports differ. UDP,
container privileges/devices/capabilities, container network modes, secrets/configs, custom entrypoints, `extends`,
`include` and external container dependencies require a remote host. Docker build services run through the IDE as
the startup project. Compose profiles are excluded from Start all unless a dependency explicitly requests them.
MySQL's `MYSQL_ALLOW_EMPTY_PASSWORD` (MariaDB's `MARIADB_ALLOW_EMPTY_ROOT_PASSWORD`) allows an empty root password, as
the images do; otherwise a root password is required.

Health combines TCP readiness with the Compose check or the mapping's native query/ping. Checks have individual
timeouts, retry counts and a start grace period. Startup failure/cancellation rolls back only processes started by
that operation. Long dependency conditions include `service_completed_successfully` for explicit one-shot commands.
Stopping a dependency first stops its running dependents. Health is monitored while services are running.

## Prisma apps without a Compose file

A Node.js project that depends on `prisma` or `@prisma/client` and has a schema (`package.json`'s `prisma.schema`,
`prisma/schema.prisma` or `schema.prisma`) whose datasource reads its URL from a variable gets that database in
Services when the repository has no Compose file: PostgreSQL (`postgres:16`) for `postgresql` and `cockroachdb`, MySQL
(`mysql:8`) for `mysql`, named `db`, with TabStudio's own user and password. SQLite needs none. F5 starts it and gives
the run the variable, such as `DATABASE_URL=postgresql://tabstudio:tabstudio@127.0.0.1:5432/<project>?schema=public`;
when the project's `.env` has no value for it, the URL is written there too, for Prisma's own commands, and a later F5
updates a URL TabStudio wrote if the port changed. With a `migrations` folder beside the schema, F5 runs `prisma
migrate deploy` before the app starts. A project whose `.env` already names a database, on this device or the network,
is left alone; the migrations of one on this device are still applied, as the project's README asks.

After `npm install`, which runs packages' lifecycle scripts (`postinstall`, `prepare`) as on a computer, TabStudio runs
`prisma generate` with the project's own Prisma (`npx --no-install`) and, on a tablet, the engine for its Linux
environment (`PRISMA_CLI_BINARY_TARGETS=linux-musl-arm64-openssl-3.0.x`). Packages whose client was never generated
count as not installed, so the setup runs again.

## Data and PostgreSQL initialization

The stable absolute workspace path selects a private `Services/<hash>` folder in app storage. Each service gets its
own directory and mount list; named volumes share data only inside that workspace. A bind mount copies its source
into app storage once. `:ro` seeds are refreshed before each start. A bind source that does not exist is created as
an empty folder when its service starts, as Docker does, for the short syntax and for the long syntax unless it sets
`bind: {create_host_path: false}`, which fails that service with its reason instead. Only folders inside the
workspace are created. The original repository path is never a server write destination. These are private copies, not live host bind mounts; PRoot cannot enforce Docker read-only mount
semantics. Avoid using a data directory across PostgreSQL majors. No service operation deletes data.

PostgreSQL uses PRoot identity 70, `initdb` with `POSTGRES_USER`, required `POSTGRES_PASSWORD`, and `POSTGRES_DB`
(defaulting to the user). Local bootstrap sockets are separate from the final loopback TCP listener. Host connections
use SCRAM password authentication. Passwords are not command arguments. Sorted `.sql`, `.sql.gz` and `.sh` files in
`/docker-entrypoint-initdb.d` run on first initialization; SQL errors stop startup. A completion marker prevents reruns,
and an incomplete initialization requires inspecting/repairing or moving that data rather than pretending it worked.
Graceful stops use `pg_ctl -m fast`; Redis saves through its shutdown command. A process that cannot stop is killed
with its descendants. Logs keep at most 1,000 lines per service and redact secret environment values.

## F5 and connections

The Compose application is selected by the devcontainer `service`, otherwise by the closest matching `build.context`
containing the startup project. F5 starts its dependency closure. With no matching application it uses devcontainer
`runServices`, and otherwise only the services the run's configuration points at: a connection string or URL in the
run's environment, `.env`, `.flaskenv`, `appsettings.json` and `appsettings.{Environment}.json`, or the Spring Boot
settings of the active profiles (`application.properties` or `.yml`, and `application-{profile}` for each profile in
`SPRING_PROFILES_ACTIVE` or `spring.profiles.active`, with `${NAME:default}` resolved). A value points at a service by
its name as the host (`Host=db`, `redis://cache:6379`) or by `localhost` and a port it publishes, the scheme's default
port included (`jdbc:postgresql://localhost/petclinic` is PostgreSQL's 5432). spring-petclinic, whose Compose file lists
MySQL and PostgreSQL, runs on in-memory H2 by default and starts neither; with the `postgres` profile, F5 starts
PostgreSQL alone. Compose application environment and matching appsettings values
are rewritten to `127.0.0.1` and the chosen published ports. Connection strings, URI hosts and Redis endpoints are
handled without replacing matching passwords. The original configuration files are untouched.

The app also receives `ConnectionStrings__<compose-service>` for PostgreSQL/MariaDB/Redis and
`<SERVICE>_HOST` / `<SERVICE>_PORT` (uppercase, `-`/`.` become `_`). ASP.NET Core reads double-underscore configuration
keys normally; external toolchain processes get the same environment. In-process runs share process environment:
variables are set only while the app owns the existing console lease and are restored afterward. Backing services
remain running when an app stops, until stopped in Services or the workspace closes. Database Explorer is optional:
combining `agent/database` automatically enables its PostgreSQL bridge. The action selects a transient connection
with the current localhost port and connects to browse its schema, without saving service credentials. Other hosts
can supply `IServiceDatabaseExplorer`; the button appears only for supported, running database services.

## Verification

```sh
# Parser, mapping, dependency, data and connection tests (no external tools required)
dotnet test tests/TabStudio.Services.Tests

# Real PostgreSQL/Redis, with the tablet's PRoot fork, on x64 or native ARM64 Linux
bash build/ci/services-alpine.sh /tmp/tabstudio-services
export TABSTUDIO_SERVICE_ROOTFS=/tmp/tabstudio-services/rootfs
export TABSTUDIO_SERVICE_PROOT=/tmp/tabstudio-services/proot-5.1.107.101/src/proot
export TABSTUDIO_SERVICE_LOADER=/tmp/tabstudio-services/proot-5.1.107.101/src/loader/loader
dotnet test tests/TabStudio.Services.Tests
dotnet test tests/TabStudio.App.Tests --filter FullyQualifiedName~BackingServices
```

CI has native x64 and ARM64 jobs. The App job runs the Npgsql ASP.NET Core F5 test and screenshots in both themes.
The Android emulator self-test installs PostgreSQL through the verified downloader, runs init SQL and a SELECT over
localhost, then stops the server. A failed run is retried once, with `FLAKY.txt` when only the retry passes.

Specifications: [Compose](https://compose-spec.github.io/compose-spec/spec.html),
[devcontainer](https://devcontainers.github.io/implementors/json_reference/),
[Alpine package index](https://pkgs.alpinelinux.org/),
[PostgreSQL initdb](https://www.postgresql.org/docs/current/app-initdb.html).
