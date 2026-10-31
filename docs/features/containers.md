# Containers without root

TabStudio runs Linux OCI images under its bundled PRoot, using the Android app's own UID. No Docker daemon,
root permission, kernel namespace or companion app is involved. **View > Containers** manages images and processes;
**View > Services** starts a repository's Compose dependencies before F5 and gives the application their local
connection settings.

## Images and processes

- Pull Docker Hub names (`postgres:17-alpine`) or another v2 OCI registry (`ghcr.io/team/image:tag`), including a
  `@sha256:…` manifest pin. Private registries use **Containers > Details > Registry sign-in**. Credentials remain
  in memory for this session; the password box clears after use. HTTPS is required except for actual loopback hosts.
- Multi-platform selection prefers Linux ARM64, compatible ARM/v7 when explicitly supported, then amd64. The
  Android host currently leaves 32-bit ARM disabled rather than assuming every ARM64 tablet has 32-bit execution.
  An amd64 glibc adapter reuses the APK's Box64 and an already prepared native glibc runtime from the PIC/embedded
  toolchains. Missing runtime files and amd64 musl images receive a specific reason. Cross-architecture container
  execution has not been qualified end to end in the container suite; use native ARM64 images where available.
- Manifests, configuration, compressed layers and uncompressed layer diff IDs are checked against SHA-256.
  Cached blobs are checked again, including same-length corruption. The content-addressed store keeps a layer once,
  across tags and images. Pulls serialize store commits so two tags resolving to one image cannot race unpacking.
  Resolving, downloading and unpacking appear in the long-operations UI, with cancellation for a direct pull.
- Entrypoint, command, environment, user and working directory come from the image, with run/Compose overrides.
  Each container has an independent writable filesystem copy. Explicit binds, named/anonymous volumes and image
  `VOLUME` declarations are supported. Stop/restart preserve that data; removing a container removes its writable
  copy, while named volumes survive. Image removal refuses images still referenced by a container.
- Logs are streamed and bounded (2,000 recent entries; rotated files of about 8 MiB each). Known password/token
  environment values are redacted. The Containers window offers start, stop, restart, logs, port preview links and
  an interactive `/bin/sh` exec terminal. Stops signal the main process and then terminate its PRoot process tree.

The writable copy is a full copy, not an OverlayFS layer: budget for the image, compressed blobs, each container
copy and its data. PRoot adds syscall overhead. Android can stop the app or its children under memory/process
pressure; this is a development runtime, not an always-on server.

## Networking and Android limits

Each process tree gets a distinct `127.42.x.y` address. The bundled PRoot's `--net-map` extension rewrites bind and
connect addresses and published TCP ports. Containers can use the same guest port without colliding. Explicit host
port conflicts fail before launch; an automatic or privileged host port receives a free unprivileged port. The
actual port is shown in the UI. Declared image TCP ports also receive loopback mappings even without `-p`,
because IPv6-only listeners cannot have independent IPv6 loopback addresses without a network namespace.
`-P` requests random host ports; otherwise a free matching host port is preferred. This is an explicit difference
from Docker's unpublished-port behavior. Compose names resolve through per-container hosts files; peer routing updates as
containers start and stop. By default published ports stay on device loopback.

Unix sockets have a 108-byte kernel path limit. `/run` is bound to a short private directory shared by the main
process and independent exec/health-check processes; a long image-store path otherwise makes PostgreSQL's Unix
socket unreachable from `pg_isready`. No Android permission is added.

The PRoot extension reports success for a denied `setgroups` within fake-root execution. It never changes the
kernel UID, grants a capability or expands Android's app sandbox. Root inside the image is simulated. There are
no cgroups, PID namespaces, network isolation, privileged containers, kernel modules, iptables or systemd as PID 1.
Other apps' private files remain protected by Android. Read-only mounts and ownership are advisory under PRoot;
this is not a security boundary between mutually untrusted workloads in the same app. Only TCP publication is
supported in this version. gzip and plain tar layers work; zstd layers receive an explicit unsupported reason.

## Compose and F5

The existing Compose reader handles `.env`, `env_file`, interpolation, anchors, overrides, ports, volumes,
dependencies, profiles, commands and health checks. It now also preserves `working_dir`, `user`, `platform` and
`restart`. Opening a repository never pulls or starts its service images. F5 starts only the dependencies of its
application service or services referenced by its configuration. Services use their real image before attempting
the existing native Alpine mapping; a failed image is stopped first, and a native fallback is named in the row and
logs with its reason. Unknown images without a mapping remain failed with their own reason. Image and native
fallback data directories are separate; no data migration is attempted.

Build-only application services are run by the IDE with F5. Dockerfile image builds, reopening the workspace in a
development container, and the Docker Engine HTTP/Unix-socket API are deferred. Reading a Compose reference from
`devcontainer.json` does not imply development-container execution. Advanced Compose settings such as privileged
mode, devices, extra capabilities, host/PID/IPC namespaces, secrets/configs, external volumes and `extends` remain
unsupported and visible rather than silently ignored.

## Terminal

The local Alpine terminal receives a `docker` command when its shell starts. It talks to a private authenticated
loopback command bridge in the IDE; this is not the Docker Engine API. Arguments use base64 framing and remain
argument vectors. The BusyBox socket client frames stdin and EOF without requiring named pipes, which Android's
guest environment cannot create. Output streams and shell exit codes are preserved. The supported subset is:

```sh
docker pull postgres:17-alpine
docker run -d --name db -e POSTGRES_PASSWORD=development -p 15432:5432 -v data:/var/lib/postgresql/data postgres:17-alpine
docker ps -a
docker images
docker logs --tail 100 db
docker logs -f db
docker exec db psql -U postgres -Atqc 'SELECT 1'
docker exec -i db sh
docker stop db
docker restart db
docker rm db
docker compose up -d
docker compose down
```

`run` also accepts `--platform`, `--entrypoint`, `-w`, `-u`, `-P` and `--rm`; unknown flags fail with a reason.
Interactive run is available through the tool window's exec terminal. Terminal exec streams stdin with `-i`;
use the tool window when a program needs a PTY. Compose commands discover the current directory's default files;
file/profile/project-selection flags and attached Compose multiplexing are not implemented.

## Verification and measurements

`.github/workflows/containers.yml` runs fake-registry, digest, layer traversal/whiteout/link, Compose, argument
parsing and network-allocation tests, followed by real images on x64 and ARM64 PRoot. The image test authenticates
to PostgreSQL with Npgsql, verifies the mounted initialization script, queries after restart, executes Redis PING,
and checks nginx HTTP. The Android `containers` self-test shard uses the APK's bundled PRoot as the app UID,
queries PostgreSQL, connects to both published database/cache ports, renders nginx in the WebView and invokes the
terminal shim. The headless IDE tests capture the Containers window and a real repository's first route.

| Measurement | Environment | Observed result |
| --- | --- | --- |
| Cold pull, unpack, initialize and start all three images | Ubuntu ARM64 CI, commit `97325408` | 8.969 s |
| Same three-image startup | Ubuntu x64 CI, same commit | 11.024 s |
| Registry/layer/Compose and real image suite | Same ARM64 and x64 runs | 27/27 passed on each architecture, including shell bridge and same-guest-port Redis peers |
| Unpacked image sizes | ARM64 run | PostgreSQL 291,097,276 B; Redis 39,678,355 B; nginx 62,687,539 B |
| Physical Android tablets | No attached physical device | Not measured; CI emulator results are tracked separately |

The observations are from [run 41148448512](https://github.com/yahav2898/tabstudio/actions/runs/41148448512).
The expanded suite passed 28/28 on each architecture in both push and PR runs at `48b6023b`, and its two IDE
tests passed, including the pinned repository route below. The Android emulator release gate requires PostgreSQL
query/TCP success, Redis PONG, nginx text read from the rendered browser and a captured PNG, all as the app UID.
The PR records the final push/PR run links and emulator verdict rather than treating Linux results as device proof.
These numbers concern the OCI suite; the IDE/emulator/repository checks are separate release gates.
Its artifact records the exact platform manifest digests. These timings include first image initialization and
depend on network/cache state; they are not a native-vs-PRoot performance comparison.

The original trial sweep is preserved on `trial/mega-4` at
`56a50fcd8637b31e3343f9a09b0af3cc5a9ccf2f`. Its `fastapi/full-stack-fastapi-template` entry at
`f4bcfaba7c85be3c6332cc74a20819f586dcdbc6` is retained in `build/ci/trial-sweep.json`, including its original
environment. The regression opens `backend`, reproduces the missing `DATABASE_URL`, and requires F5 to discover
the parent repository's unchanged `compose.yml` and `compose.override.yml`, start their real `postgres:18` image,
forward the backend's Compose environment, and serve `/docs`. It then runs the upstream prestart commands explicitly
(Alembic migrations and initial data), logs in, and queries `/api/v1/items/` against that database. TabStudio does
not automatically run arbitrary repository prestart scripts. The frontend is built using the upstream Bun command;
no Dockerfile build is involved. Only `db`, the backend's dependency, starts through F5.

An additional regression uses `gothinkster/node-express-realworld-example-app` at
`30b68e1e881462b2f4164ea09ab4c4f5699c7b0b`. That upstream has no Compose file, so the test supplies the checked-in
`build/ci/container-trial/compose.yaml` fixture. It reproduces Prisma's database-connection failure without a database,
then requires F5 to start PostgreSQL, run migrations, and receive HTTP 200 containing `"tags"` at `/api/tags`.
The fixture is separate from the original sweep's upstream Compose regression.

Reproduce Linux checks with:

```sh
bash build/ci/containers-proot.sh /tmp/tabstudio-container-runtime
export TABSTUDIO_CONTAINER_PROOT=/tmp/tabstudio-container-runtime/proot-5.1.107.101/src/proot
export TABSTUDIO_CONTAINER_LOADER=/tmp/tabstudio-container-runtime/proot-5.1.107.101/src/loader/loader
dotnet test tests/TabStudio.Containers.Tests -c Release -warnaserror
```
