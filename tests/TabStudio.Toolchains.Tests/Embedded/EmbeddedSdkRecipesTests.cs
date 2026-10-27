using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Embedded.Installation;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class EmbeddedSdkRecipesTests
{
    [Theory]
    [InlineData("pico-sdk", "2.1.1", "bddd20f928ce76142793bef434d4f75f4af6e433", "PICO_SDK_PATH")]
    [InlineData("zephyr", "4.1.0", "7823374e872145b5bd018bfe447839eb36042611", "ZEPHYR_BASE")]
    [InlineData("esp-idf", "5.4.1", "4c2820d377d1375e787bcef612f0c32c1427d183", "IDF_PATH")]
    public void Source_pins_and_environment_are_available_to_the_production_installer(string id, string version, string revision, string sourceVariable)
    {
        var recipe = Assert.IsType<EmbeddedSdkRecipe>(EmbeddedSdkBootstrap.Recipes.Get(id));
        Assert.Equal(revision, recipe.Revision);
        Assert.Equal("https", recipe.Repository.Scheme);
        Assert.Equal(version, ToolchainCatalog.Default.Find(id)!.PinnedVersion);
        Assert.Equal(recipe.SourceDirectory, recipe.Environment[sourceVariable]);
        Assert.NotEmpty(recipe.Commands);
        Assert.DoesNotContain("PYTHONSAFEPATH", EmbeddedSdkBootstrap.Environment(id).Keys);
        Assert.All(recipe.Files, file => Assert.DoesNotContain("PYTHONSAFEPATH", file.Content, StringComparison.Ordinal));
        Assert.All(recipe.RequiredArtifacts, path => Assert.StartsWith("/opt/tabstudio/", path, StringComparison.Ordinal));
    }

    [Fact]
    public void Zephyr_install_uses_a_complete_hash_lock_and_its_venv_for_cmake_children()
    {
        var recipe = EmbeddedSdkBootstrap.Recipes.Get("zephyr")!;
        var requirements = Assert.Single(recipe.Files, file => file.Path.EndsWith("/requirements.txt", StringComparison.Ordinal));
        var logicalLines = requirements.Content.Replace("\\\n", "", StringComparison.Ordinal).Split('\n')
            .Select(line => line.Trim()).Where(line => line.Length != 0 && !line.StartsWith('#')).ToArray();
        Assert.Equal(31, logicalLines.Length);
        Assert.All(logicalLines, line => Assert.Matches(@"^[A-Za-z0-9_.-]+==[A-Za-z0-9_.+-]+\s+(--hash=sha256:[0-9a-f]{64}\s*)+$", line));
        Assert.Contains(logicalLines, line => line.StartsWith("west==1.3.0 ", StringComparison.Ordinal));
        var pipCommands = recipe.Commands.Where(command => command.Arguments.Contains("pip", StringComparer.Ordinal)).ToArray();
        Assert.Equal(2, pipCommands.Length);
        Assert.All(pipCommands, command => Assert.Contains("--require-hashes", command.Arguments));
        Assert.Contains("--no-build-isolation", pipCommands[1].Arguments);
        Assert.Equal("/opt/tabstudio/zephyr-python", recipe.Environment["VIRTUAL_ENV"]);
        Assert.StartsWith("/opt/tabstudio/zephyr-python/bin:", recipe.Environment["PATH"], StringComparison.Ordinal);
        Assert.False(recipe.Submodules);
        Assert.Contains("/opt/tabstudio/zephyr-python", recipe.Removes);
    }

    [Fact]
    public void An_unknown_sdk_is_not_offered_an_incomplete_install_recipe()
    {
        Assert.Null(EmbeddedSdkBootstrap.Recipes.Get("unknown"));
    }

    [Fact]
    public void Esp_python_install_has_no_rolling_constraints_or_unhashed_build_environment()
    {
        var recipe = EmbeddedSdkBootstrap.Recipes.Get("esp-idf")!;
        var requirements = Assert.Single(recipe.Files, file => file.Path.EndsWith("/requirements.txt", StringComparison.Ordinal));
        var logicalLines = requirements.Content.Replace("\\\n", "", StringComparison.Ordinal).Split('\n')
            .Select(line => line.Trim()).Where(line => line.Length != 0 && !line.StartsWith('#')).ToArray();
        Assert.Equal(58, logicalLines.Length);
        Assert.All(logicalLines, line => Assert.Matches(@"^[A-Za-z0-9_.-]+==[A-Za-z0-9_.+-]+\s+(--hash=sha256:[0-9a-f]{64}\s*)+$", line));
        Assert.DoesNotContain(recipe.Commands, command => command.Arguments.Contains("install-python-env", StringComparer.Ordinal));
        var pipInstalls = recipe.Commands.Where(command => command.Arguments.Contains("pip", StringComparer.Ordinal) && command.Arguments.Contains("install", StringComparer.Ordinal)).ToArray();
        Assert.Equal(2, pipInstalls.Length);
        Assert.All(pipInstalls, command => Assert.Contains("--require-hashes", command.Arguments));
        Assert.Contains("--only-binary=:all:", pipInstalls[1].Arguments);
        Assert.Contains("--no-binary=esptool", pipInstalls[1].Arguments);
        Assert.Contains("--no-build-isolation", pipInstalls[1].Arguments);
        Assert.Contains(recipe.Commands, command => command.Arguments.Contains("check-python-dependencies", StringComparer.Ordinal));
        Assert.Contains(recipe.Files, file => file.Path.EndsWith("/idf_version.txt", StringComparison.Ordinal) && file.Content == "5.4\n");
        Assert.Contains(recipe.Files, file => file.Path.EndsWith("/espidf.constraints.v5.4.txt", StringComparison.Ordinal) && file.Content.Contains("cryptography>=2.1.4,<45", StringComparison.Ordinal));
        Assert.Equal("/opt/tabstudio/esp-idf-tools/python_env/idf5.4_py3.12_env", recipe.Environment["IDF_PYTHON_ENV_PATH"]);
    }

    [Fact]
    public void Rust_uses_native_verified_components_and_bare_metal_libraries_without_a_bootstrap_download_script()
    {
        var recipe = EmbeddedSdkBootstrap.Recipes.Get("embedded-rust")!;
        Assert.False(recipe.CheckoutSource);
        Assert.Equal("2d8144b7880597b6e6d3dfd63a9a9efae3f533d3", recipe.Revision);
        Assert.Equal("1.97.0", ToolchainCatalog.Default.Find("embedded-rust")!.PinnedVersion);
        Assert.False(ToolchainCatalog.Default.Find("embedded-rust")!.Auxiliary);
        Assert.Equal(23, recipe.Setup.Count);
        Assert.Equal(10, recipe.Setup.Count(step => step.Archive!.Url.AbsolutePath.Contains("rust-std-1.97.0-", StringComparison.Ordinal) && step.Architectures.Count == 0));
        Assert.All(recipe.Setup, step =>
        {
            Assert.Equal("static.rust-lang.org", step.Archive!.Url.Host);
            Assert.Contains("/2026-07-09/", step.Archive.Url.AbsolutePath, StringComparison.Ordinal);
            Assert.Matches("^[0-9a-f]{64}$", step.Archive.Sha256);
            Assert.Contains("--disable-ldconfig", step.Command);
            Assert.Contains("--prefix=/opt/tabstudio/embedded-rust", step.Command);
        });
        foreach (var architecture in new[] { "x86_64", "aarch64" })
        {
            Assert.Equal(17, recipe.Setup.Count(step => step.Architectures.Count == 0 || step.Architectures.Contains(architecture)));
        }
        Assert.Contains(recipe.Files, file => file.Path == "/usr/local/bin/rust-analyzer" && file.Executable);
        Assert.Contains(recipe.RequiredArtifacts, path => path.EndsWith("/library/core/Cargo.toml", StringComparison.Ordinal));
        Assert.Equal("true", EmbeddedSdkBootstrap.Environment("embedded-rust")["CARGO_NET_GIT_FETCH_WITH_CLI"]);
        Assert.All(recipe.Files.Where(file => file.Executable), file =>
            Assert.Contains("CARGO_NET_GIT_FETCH_WITH_CLI", file.Content, StringComparison.Ordinal));
    }
}
