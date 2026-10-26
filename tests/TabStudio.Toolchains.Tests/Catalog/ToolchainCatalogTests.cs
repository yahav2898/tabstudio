using System.Text;
using System.Text.RegularExpressions;
using TabStudio.Toolchains.Catalog;

namespace TabStudio.Toolchains.Tests.Catalog;

/// <summary>The catalog that ships with TabStudio, and reading catalogs in general.</summary>
public sealed class ToolchainCatalogTests
{
    private static readonly ToolchainCatalog Catalog = ToolchainCatalog.Default;

    [Fact]
    public void Describes_all_seven_languages()
    {
        Assert.Equal(["python", "node", "cpp", "rust", "go", "java", "kotlin"], Catalog.Toolchains.Select(t => t.Id));
        var languages = Catalog.Toolchains.SelectMany(t => t.Languages).ToHashSet();
        Assert.Superset(new HashSet<string> { "Python", "JavaScript", "TypeScript", "C", "C++", "Rust", "Go", "Java", "Kotlin" }, languages);
    }

    [Fact]
    public void The_store_lists_the_finished_toolchains()
    {
        Assert.Equal(["python", "node", "cpp", "rust", "go", "java"], Catalog.Listed.Select(t => t.Id));
    }

    [Fact]
    public void Every_toolchain_says_what_it_installs_and_how_it_runs()
    {
        foreach (var toolchain in Catalog.Toolchains)
        {
            Assert.False(string.IsNullOrWhiteSpace(toolchain.Name), toolchain.Id);
            Assert.EndsWith(".", toolchain.Description, StringComparison.Ordinal);
            Assert.NotEmpty(toolchain.Packages);
            Assert.Contains(toolchain.VersionPackage, toolchain.Packages);
            Assert.NotEmpty(toolchain.FileExtensions);
            Assert.All(toolchain.FileExtensions, e => Assert.StartsWith(".", e, StringComparison.Ordinal));
            Assert.NotEmpty(toolchain.ProjectFiles);
            Assert.NotNull(toolchain.Run);
            Assert.NotEmpty(toolchain.Run.File);
            Assert.Contains(toolchain.Run.File, a => a.Contains("{file}", StringComparison.Ordinal));
            Assert.NotEmpty(toolchain.Run.Entries);
            Assert.All(toolchain.Run.Entries, e => Assert.NotEmpty(e.Command));
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
    public void Listed_toolchains_have_a_debug_adapter()
    {
        foreach (var toolchain in Catalog.Listed)
        {
            Assert.NotNull(toolchain.DebugAdapter);
            Assert.NotEmpty(toolchain.DebugAdapter.Command);
            Assert.NotNull(toolchain.DebugAdapter.Type);

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
        var archives = Catalog.Toolchains.SelectMany(t => t.Setup).Select(s => s.Archive).OfType<ArchiveDefinition>().ToList();

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
    public void Finds_the_toolchain_for_a_file(string file, string id)
    {
        Assert.Equal(id, Catalog.ForFile(file)?.Id);
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
            foreach (var tool in new[] { toolchain.LanguageServer!, toolchain.DebugAdapter! })
            {
                Assert.NotNull(tool.Id);
                var paths = toolchain.Setup.SelectMany(s => s.Removes.Concat(s.File is { } file ? [file.Path] : []));
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
