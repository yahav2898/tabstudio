# Actual app starter qualification

`TemplateBuildCheck.csproj` compiles the app's canonical `ProjectTemplates.Embedded.cs` directly.
The small factory supplies only its metadata shape and an entry point; it contains no duplicated
firmware, board definitions, configuration or project-writing logic. Every generated PlatformIO
starter occurs in exactly one group. The harness refuses missing or duplicate coverage.

The normal run installs pinned PlatformIO Core through `EmbeddedGlibcToolchainInstaller`, using
its verified Ubuntu root, signed prerequisite packages, actual USB support installation and
readiness check. Builds run through the production `EmbeddedBuildPlanner` and `IToolchainRuntime`.
`auto` qualifies the app's native-first fallback policy: native ARM64 artifacts remain preferred,
and missing or incorrectly labelled native executables can use unchanged x64 compilers through Box64.

Each job starts with an empty guest and cache. `PLATFORMIO_CACHE_DIR` shares first-verified package
archives between the job's sequential builds, while each template keeps its installed package choices
in its private Core directory. No cross-run cache is restored. Core 6.2.0 verifies registry SHA-256 on
first download; subsequent cache hits trust those bytes. Extracted packages remain independent.

Evidence records the original generated file hashes, complete build logs, actual ELF load ranges and
firmware hashes, compiler Error List diagnostics, and whether stack-usage files/rows really appeared.
It also dry-plans every generated upload profile using the real firmware and an explicitly synthetic
probe identity when a probe selection is required. ESP profiles therefore feed
actual resolved bootloader/partition/application/OTA initializer metadata into `EspFlashManifestReader`;
no merged filler image is substituted. The harness never executes upload commands or opens USB devices.
A missing automatic upload profile is reported as such, not counted as a verified flashing path.
The nRF52840 DK starter uses Zephyr's exact board definition and is qualified by the native SDK harness,
not the PlatformIO Arduino core's incompatible nRF52832 linker/variant. There are fifteen canonical
PlatformIO starters, three Pico SDK variants, one Zephyr starter and the separate CH32V003 ch32fun starter.
The same SDK harness builds all three canonical Pico variants and retains their ELF/UF2 artifacts.
Pico 2 and Pico 2 W dry-plan the addressed ELF because their SDK-generated UF2 can contain an RP2350 A2
E10 workaround block that erases additional flash. The upstream parser inspects that UF2 artifact;
successful packaging does not qualify the storage upload path. Native picotool is the ordinary existing
Store package, with the archive's original CMake exports and SHA-256 verification, rather than a source fetch.

Run without the guest to inspect exact generated files:

```sh
dotnet run --project build/embedded/template-build-check -- generate /tmp/generated-starters all
```

Run a production installer/build group on a native Linux host:

```sh
bash build/embedded/templates.sh /tmp/template-proof ci-out/template-proof arm-legacy
```

CI exercises all groups on native x64 and ARM64 runners with at most two jobs in parallel.
Successful compilation does not establish Android USB behavior, physical board identity, board pin
mapping, bootloader entry, firmware execution, or physical flash/debug verification.
