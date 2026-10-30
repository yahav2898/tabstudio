using System.Text;
using System.Text.RegularExpressions;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Installation;

namespace TabStudio.Toolchains.Tests.Catalog;

/// <summary>The catalog that ships with TabStudio, and reading catalogs in general.</summary>
public sealed class ToolchainCatalogTests
{
    private static readonly ToolchainCatalog Catalog = ToolchainCatalog.Default;

    [Fact]
    public void Describes_languages_flutter_and_the_separately_licensed_pic_compilers()
    {
        Assert.Equal(["flutter", "python", "node", "cpp", "rust", "go", "java", "php", "kotlin", "swift", "android", "xc8", "xc32", "embedded-arm", "embedded-riscv", "embedded-avr", "embedded-support", "platformio", "arduino", "esp-idf", "zephyr", "pico-sdk", "micropython", "embedded-import", "embedded-debug", "probe-rs", "embedded-esptool", "embedded-picotool", "embedded-bossac", "embedded-teensy", "embedded-wchisp", "embedded-mspdebug", "embedded-rust", "embedded-ch32fun", "embedded-nrfutil", "embedded-wlink"], Catalog.Toolchains.Select(t => t.Id));
        var languages = Catalog.Toolchains.SelectMany(t => t.Languages).ToHashSet();
        Assert.Superset(new HashSet<string> { "Python", "JavaScript", "TypeScript", "C", "C++", "Rust", "Go", "Java", "Kotlin", "Dart", "PHP", "Swift" }, languages);
    }

    [Fact]
    public void The_store_lists_the_finished_toolchains()
    {
        Assert.Equal(["flutter", "python", "node", "cpp", "rust", "go", "java", "php", "kotlin", "swift", "android", "xc8", "xc32", "embedded-arm", "embedded-riscv", "embedded-avr", "embedded-support", "platformio", "arduino", "esp-idf", "zephyr", "pico-sdk", "micropython", "embedded-debug", "probe-rs", "embedded-esptool", "embedded-picotool", "embedded-bossac", "embedded-teensy", "embedded-wchisp", "embedded-mspdebug", "embedded-rust", "embedded-ch32fun", "embedded-nrfutil"], Catalog.Listed.Select(t => t.Id));
        Assert.Equal("jdtls", Catalog.Find("android")!.LanguageServer!.Id);
        Assert.Contains("kotlin-language-server", Catalog.Find("android")!.AdditionalToolIds);
    }

    [Fact]
    public void Every_toolchain_says_what_it_installs_and_how_it_runs()
    {
        foreach (var toolchain in Catalog.Toolchains)
        {
            Assert.False(string.IsNullOrWhiteSpace(toolchain.Name), toolchain.Id);
            Assert.EndsWith(".", toolchain.Description, StringComparison.Ordinal);
            if (toolchain.Auxiliary)
            {
                Assert.NotEmpty(toolchain.Packages);
                Assert.NotEmpty(toolchain.Host!.Commands);
                Assert.Empty(toolchain.FileExtensions);
                Assert.Empty(toolchain.ProjectFiles);
                Assert.Null(toolchain.Run);
                continue;
            }
            var sdk = EmbeddedSdkBootstrap.Recipes.Get(toolchain.Id);
            if (sdk is not null)
            {
                // Native firmware SDKs are configured by the production installer and build planner;
                // they must never invent a host command that executes the cross-compiled ELF.
                Assert.True(EmbeddedGlibcToolchainInstaller.Handles(toolchain.Id));
                Assert.Matches("^[0-9a-f]{40}$", sdk.Revision);
                Assert.Equal("https", sdk.Repository.Scheme);
                Assert.NotEmpty(sdk.Packages);
                Assert.NotEmpty(sdk.Commands);
                Assert.All(sdk.Commands, command => Assert.False(string.IsNullOrWhiteSpace(command.Program)));
                Assert.Equal("2", EmbeddedSdkBootstrap.Environment(toolchain.Id)[EmbeddedSdkBootstrap.JobsVariable]);
                Assert.False(toolchain.SourceFolderIsProject);
                if (!sdk.CheckoutSource)
                {
                    var archives = sdk.Setup.Select(step => step.Archive).OfType<ArchiveDefinition>().ToArray();
                    Assert.NotEmpty(archives);
                    Assert.All(archives, archive =>
                    {
                        Assert.Equal("https", archive.Url.Scheme);
                        Assert.Matches("^[0-9a-f]{64}$", archive.Sha256);
                    });
                    Assert.NotEmpty(sdk.RequiredArtifacts);
                    Assert.Empty(toolchain.Run!.File);
                    Assert.Empty(toolchain.Run.Entries);
                }
            }
            else if (toolchain.Id != "flutter" && !(toolchain.Glibc is { Packages.Count: > 0 } && toolchain.Packages.Count == 0))
            {
                // Flutter, and Swift, install into their own Ubuntu environment instead of from Alpine packages.
                Assert.NotEmpty(toolchain.Packages);
                Assert.True(toolchain.PinnedVersion is not null || toolchain.Packages.Contains(toolchain.VersionPackage!));
            }
            Assert.NotEmpty(toolchain.FileExtensions);
            Assert.All(toolchain.FileExtensions, e => Assert.StartsWith(".", e, StringComparison.Ordinal));
            Assert.NotNull(toolchain.Run);
            if (sdk is null)
            {
                Assert.NotEmpty(toolchain.ProjectFiles);
                Assert.NotEmpty(toolchain.Run.File);
                Assert.Contains(toolchain.Run.File, a => a.Contains("{file}", StringComparison.Ordinal));
                Assert.NotEmpty(toolchain.Run.Entries);
                Assert.All(toolchain.Run.Entries, e => Assert.NotEmpty(e.Command));
                Assert.All(toolchain.Run.Entries.SelectMany(e => e.Contains.Concat(e.Excludes)), pattern => _ = new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant));
            }
            Assert.NotNull(toolchain.Host);
            Assert.NotEmpty(toolchain.Host.Commands);
            Assert.NotNull(toolchain.LanguageServer);
            Assert.NotEmpty(toolchain.LanguageServer.Command);
            Assert.NotEmpty(toolchain.LanguageServer.Languages);
            Assert.NotEmpty(toolchain.ProblemPatterns);
            Assert.True(toolchain.EstimatedDownloadSize > 0 && toolchain.EstimatedInstalledSize > toolchain.EstimatedDownloadSize, toolchain.Id);
        }
    }

    [Fact]
    public void General_purpose_language_toolchains_have_a_debug_adapter()
    {
        foreach (var toolchain in Catalog.Listed.Where(t => !t.Auxiliary && t.Id is not ("android" or "xc32" or "embedded-arm" or "embedded-riscv" or "embedded-avr" or "embedded-support" or "platformio" or "arduino" or "micropython" or "embedded-debug" or "embedded-ch32fun" or "zephyr" or "pico-sdk" or "esp-idf" or "embedded-rust")))
        {
            Assert.NotNull(toolchain.DebugAdapter);
            Assert.NotEmpty(toolchain.DebugAdapter.Command);
            Assert.NotNull(toolchain.DebugAdapter.Type);
            if (toolchain.Id == "flutter")
            {
                Assert.NotEmpty(toolchain.Dependencies);
            }
        }
    }

    [Fact]
    public void Its_patterns_are_valid_and_name_their_groups()
    {
        foreach (var toolchain in Catalog.Toolchains)
        {
            foreach (var pattern in toolchain.ProblemPatterns)
            {
                var regex = new Regex(pattern.Pattern);
                Assert.Contains("file", regex.GetGroupNames());
                Assert.Contains("line", regex.GetGroupNames());
            }

            if (toolchain.ErrorPattern is { } error)
            {
                Assert.Contains("message", new Regex(error).GetGroupNames());
            }

            if (toolchain.Host?.VersionPattern is { } version)
            {
                Assert.Contains("version", new Regex(version).GetGroupNames());
            }
        }
    }

    [Fact]
    public void Archives_are_pinned_by_their_hash()
    {
        var archives = Catalog.Toolchains.SelectMany(t => t.Setup.Concat(t.Glibc?.Setup ?? [])).Select(s => s.Archive).OfType<ArchiveDefinition>().ToList();

        Assert.NotEmpty(archives);
        Assert.All(archives, a =>
        {
            Assert.Equal("https", a.Url.Scheme);
            Assert.Matches("^[0-9a-f]{64}$", a.Sha256);
            Assert.True(a.Size > 0);
            Assert.StartsWith("/opt/tabstudio", a.Destination, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void An_archive_is_unpacked_into_its_own_folder_never_over_another_toolchains()
    {
        // Kotlin's compiler was unpacked into /opt/tabstudio itself, which the installer empties first: installing
        // Kotlin deleted debugpy, js-debug, java-debug, Phpactor and the Android SDK, and removing it did the same.
        var destinations = Catalog.Toolchains
            .SelectMany(t => t.Setup.Concat(t.Glibc?.Setup ?? []).Select(s => s.Archive).OfType<ArchiveDefinition>().Select(a => (Toolchain: t.Id, Folder: a.Destination.TrimEnd('/'))))
            .Distinct()
            .ToList();

        Assert.All(destinations, d => Assert.True(d.Folder.Split('/', StringSplitOptions.RemoveEmptyEntries).Length >= 3, $"{d.Toolchain} unpacks into {d.Folder}"));
        foreach (var (toolchain, folder) in destinations)
        {
            Assert.DoesNotContain(destinations, other => other.Toolchain != toolchain
                && (other.Folder == folder || other.Folder.StartsWith(folder + "/", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void Pins_alpine_linux_for_every_android_architecture()
    {
        var linux = Catalog.Linux;

        Assert.Equal("Alpine Linux", linux.Name);
        Assert.Equal(["main", "community"], linux.Repositories);
        Assert.Equal(new HashSet<string> { "aarch64", "armv7", "x86_64" }, linux.Rootfs.Keys.ToHashSet());
        Assert.All(linux.Rootfs, image =>
        {
            Assert.Matches("^[0-9a-f]{64}$", image.Value.Sha256);
            Assert.Contains(linux.Version, image.Value.File, StringComparison.Ordinal);
            Assert.Contains(image.Key, image.Value.File, StringComparison.Ordinal);
            Assert.True(image.Value.Size > 1_000_000);
        });
        Assert.Equal("Alpine Linux 3.24", linux.DisplayName);
        Assert.Equal("https://dl-cdn.alpinelinux.org/alpine/v3.24/main/aarch64/", linux.RepositoryAddress("main", "aarch64").AbsoluteUri);
        Assert.Equal(
            "https://dl-cdn.alpinelinux.org/alpine/v3.24/releases/armv7/alpine-minirootfs-3.24.3-armv7.tar.gz",
            linux.RootfsAddress("armv7", linux.Rootfs["armv7"]).AbsoluteUri);
    }

    [Theory]
    [InlineData("main.py", "python")]
    [InlineData("tool.PYW", "python")]
    [InlineData("server.js", "node")]
    [InlineData("index.ts", "node")]
    [InlineData("module.mjs", "node")]
    [InlineData("main.cpp", "cpp")]
    [InlineData("lib.rs", "rust")]
    [InlineData("main.go", "go")]
    [InlineData("Main.java", "java")]
    [InlineData("App.kt", "kotlin")]
    [InlineData("index.php", "php")]
    [InlineData("main.swift", "swift")]
    public void Finds_the_toolchain_for_a_file(string file, string id)
    {
        Assert.Equal(id, Catalog.ForFile(file)?.Id);
    }

    [Fact]
    public void Android_catalog_order_does_not_take_over_standalone_Java_or_Kotlin_files()
    {
        var android = Catalog.Find("android")!;
        var androidFirst = Catalog with { Toolchains = [android, .. Catalog.Toolchains.Where(t => t.Id != "android")] };

        Assert.Equal("java", androidFirst.ForFile("Main.java")?.Id);
        Assert.Equal("kotlin", androidFirst.ForFile("App.kt")?.Id);
    }

    [Fact]
    public void Finds_nothing_for_files_no_toolchain_runs()
    {
        Assert.Null(Catalog.ForFile("Program.cs"));
        Assert.Null(Catalog.ForFile("README.md"));
        Assert.Equal("Python", Catalog.Find("python")?.Name);
        Assert.Null(Catalog.Find("cobol"));
    }

    [Fact]
    public void Refuses_a_catalog_from_a_newer_version_of_tabstudio()
    {
        var json = """{ "schemaVersion": 2, "linux": { "name": "Alpine Linux", "version": "9", "branch": "v9", "mirror": "https://example.org" }, "toolchains": [] }""";

        var error = Assert.Throws<InvalidDataException>(() => ToolchainCatalog.Load(new MemoryStream(Encoding.UTF8.GetBytes(json))));

        Assert.Contains("format 2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_toolchain_listed_twice()
    {
        var json = """
            { "schemaVersion": 1, "linux": { "name": "Alpine Linux", "version": "9", "branch": "v9", "mirror": "https://example.org" },
              "toolchains": [ { "id": "a", "name": "A", "description": "A." }, { "id": "a", "name": "B", "description": "B." } ] }
            """;

        var error = Assert.Throws<InvalidDataException>(() => ToolchainCatalog.Load(new MemoryStream(Encoding.UTF8.GetBytes(json))));

        Assert.Contains("'a'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Language_servers_and_debug_adapters_start_from_the_catalog()
    {
        var python = Catalog.Find("python")!;
        var node = Catalog.Find("node")!;

        var server = python.LanguageServer!.ToStartSpec("/projects/app");
        var adapter = node.DebugAdapter!.ToStartSpec("/projects/web", port: 4711);

        Assert.Equal(("pyright-langserver", "/projects/app"), (server.Program, server.WorkingDirectory));
        Assert.Equal(["--stdio"], server.Arguments);
        Assert.True(node.DebugAdapter.UsesTcp);
        Assert.Equal(("js-debug-adapter", "4711"), (adapter.Program, adapter.Arguments[0]));
        Assert.Equal("/opt/tabstudio/python/debugpy", python.DebugAdapter!.ToStartSpec().Environment["PYTHONPATH"]);
        Assert.Throws<InvalidOperationException>(() => node.DebugAdapter.ToStartSpec());
    }

    [Theory]
    [InlineData("pyright", "python")]
    [InlineData("debugpy", "python")]
    [InlineData("typescript-language-server", "node")]
    [InlineData("js-debug", "node")]
    [InlineData("clangd", "cpp")]
    [InlineData("rust-analyzer", "rust")]
    [InlineData("gopls", "go")]
    [InlineData("delve", "go")]
    [InlineData("jdtls", "java")]
    [InlineData("vscode-html-language-server", "node")]
    [InlineData("vscode-css-language-server", "node")]
    [InlineData("vscode-json-language-server", "node")]
    [InlineData("kotlin-language-server", "android")]
    [InlineData("phpactor", "php")]
    [InlineData("php-debug", "php")]
    [InlineData("kotlin-lsp", "kotlin")]
    [InlineData("sourcekit-lsp", "swift")]
    public void Finds_the_toolchain_that_installs_a_tool_by_the_id_its_clients_use(string tool, string toolchain)
    {
        Assert.Equal(toolchain, Catalog.ForTool(tool)?.Id);
    }

    [Fact]
    public void Listed_toolchains_install_their_tools_under_the_commands_they_name()
    {
        // A tool's command is a program on PATH that a setup step or a package installs, so clients can find it by name.
        var installed = new Dictionary<string, string[]>
        {
            ["pyright-langserver"] = ["/usr/local/bin/pyright-langserver"],
            ["js-debug-adapter"] = ["/usr/local/bin/js-debug-adapter"],
        };
        foreach (var toolchain in Catalog.Listed)
        {
            foreach (var tool in new[] { toolchain.LanguageServer, toolchain.DebugAdapter }.OfType<ToolCommandDefinition>())
            {
                Assert.NotNull(tool.Id);
                // Shared tools (for example MicroPython's Pyright) are installed through their registered provider.
                var provider = Catalog.ForTool(tool.Id!) ?? toolchain;
                var paths = provider.Setup.SelectMany(s => s.Removes.Concat(s.File is { } file ? [file.Path] : []));
                if (installed.TryGetValue(tool.Command[0], out var expected))
                {
                    Assert.Contains(expected[0], paths);
                }
            }
        }

        var script = Catalog.Find("node")!.Setup.Single(s => s.File is not null).File!;
        Assert.True(script.Executable);
        Assert.StartsWith("#!/bin/sh\n", script.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_json_that_is_not_a_catalog()
    {
        Assert.Throws<InvalidDataException>(() => ToolchainCatalog.Load(new MemoryStream("[1, 2"u8.ToArray())));
    }
}
