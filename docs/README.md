# TabStudio documentation

Start with the [README](../README.md) for what TabStudio is and how to install it.

## Project

| Document | What it covers |
| --- | --- |
| [Architecture](ARCHITECTURE.md) | How the app is built: the libraries, run hosts, toolchains, language services and the rules Android sets |
| [Roadmap](ROADMAP.md) | Every shipped release and what comes next |
| [Release notes](releases/) | What changed in each version, and what CI verified before it was published |
| [Contributing](../CONTRIBUTING.md) | Setting up, code style, tests, CI, adding a language, releasing |

## Features

| Guide | What it covers |
| --- | --- |
| [Database Explorer](features/database.md) | Connecting to SQLite, PostgreSQL and SQL Server, running SQL, editing data, EF Core migrations |
| [Remote development over SSH](features/remote-ssh.md) | Opening a folder on your server, and publishing ASP.NET Core apps to it |
| [TabStudio Live](features/live.md) | Coding together in real time on the same Wi-Fi: sharing, following, shared debugging, classrooms and the security model |
| [Mobile and web servers](features/mobile.md) | Server previews, Android builds on the tablet, iOS builds in the cloud |
| [Microchip PIC](features/pic.md) | XC8 and XC32 builds on the tablet, flashing over USB, debugging in gpsim |
| [Embedded firmware](features/embedded.md) | Native firmware projects, permissioned USB bridges, upload/debug, backups, memory/register analysis and explicit verification limits |
| [Port to Avalonia](features/porting.md) | Converting WPF and Windows Forms projects into Avalonia projects |
| [Time-travel debugging](features/time-travel.md) | Recording a C# run and stepping back through its timeline |
| [Layout checks](layout-checks.md) | SWRM001 to SWRM005: what Swarm checks on every screen, and how to fix each finding |
| [Containers without root](features/containers.md) | Real OCI images under PRoot, Compose/F5, docker terminal commands, measurements and limits |
| [Backing services](features/services.md) | PostgreSQL, Redis and other Compose services run natively, without Docker |
| [PHP and Laravel](features/laravel.md) | Composer setup, Laravel's `.env`, migrations, Vite and databases, Xdebug, PHPUnit, Symfony, Slim and plain sites |
| [Visual Basic Windows Forms designer](features/vb-winforms-designer.md) | Designing `Form1.vb` by touch, `Form1.Designer.vb` as Visual Studio writes it, `Handles` handlers and the Application Framework |
| [Swift](features/swift.md) | The experimental Swift toolchain in the glibc environment: what works, what CI verified, and its limits |
| [Flutter and Dart](features/flutter.md) | The experimental Flutter SDK environment, its verification status and limits |
| [On-device AI](features/local-ai.md) | The llama.cpp runtime: measurements, model choices and limits |

## Design

| Document | What it covers |
| --- | --- |
| [Design system](design/README.md) | How every view looks and behaves: layout, sizes, touch, typography |
| [Color tokens](design/tokens.md) | Every color of both themes, and its contrast |
| [Stage 2: the shell](design/stage-2.md) | The activity bar, unified search and the adaptive bars |
| [Audit of 0.8 and 0.9](design/audit.md) | The design problems found before the redesign, and how each was fixed |

Screenshots used by these pages live in [images/](images/), one folder per feature.

