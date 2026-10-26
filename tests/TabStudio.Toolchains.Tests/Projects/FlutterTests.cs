using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Projects;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Projects;

public sealed class FlutterTests
{
    [Theory]
    [InlineData("sdk: flutter", true)]
    [InlineData("  sdk: 'flutter' # framework", true)]
    [InlineData("# sdk: flutter\nname: flutter_tools", false)]
    [InlineData("environment:\n  sdk: '>=3.10.0 <4.0.0'", false)]
    public void Dart_and_Flutter_packages_get_the_right_runtime(string spec, bool flutter)
    {
        using var temp = new TempDirectory();
        temp.Write("pubspec.yaml", spec);
        temp.Write("lib/main.dart", "void main() {}\n");
        var detected = Assert.Single(ToolchainProjectDetector.Detect(temp.Path, ToolchainCatalog.Default));
        Assert.Equal("flutter", detected.Toolchain.Id);
        var run = RunPlanner.PlanProject(detected)!;
        Assert.Equal(flutter ? "flutter" : "dart", run.Run.Program);
        Assert.Equal(temp.Path, run.Run.WorkingDirectory);
        if (flutter) Assert.Contains("web-server", run.Run.Arguments);
    }

    [Fact]
    public void Pub_get_is_offered_until_a_package_configuration_exists()
    {
        using var temp = new TempDirectory();
        temp.Write("pubspec.yaml", "name: demo\ndependencies:\n  flutter:\n    sdk: flutter\n");
        var project = Assert.Single(ToolchainProjectDetector.Detect(temp.Path, ToolchainCatalog.Default));
        var dependency = RunPlanner.DependenciesOf(project)!;
        Assert.Equal(["flutter", "pub", "get"], dependency.Command);
        Assert.True(RunPlanner.IsPending(project, dependency));
        temp.Write(".dart_tool/package_config.json", "{}");
        Assert.False(RunPlanner.IsPending(project, dependency));
    }

    [Theory]
    [InlineData("aarch64", "arm64")]
    [InlineData("x86_64", "amd64")]
    public void Glibc_images_are_architecture_specific_and_pinned(string arch, string file)
    {
        var image = FlutterToolchainInstaller.RootImage(arch)!.Value;
        Assert.Contains(file, image.Address.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(64, image.Sha256.Length);
        Assert.Null(FlutterToolchainInstaller.RootImage("armv7"));
    }

    [Theory]
    [InlineData("flutter", true)]
    [InlineData("dart", true)]
    [InlineData("/opt/flutter/bin/cache/dart-sdk/bin/dart", true)]
    [InlineData("node", false)]
    [InlineData("/usr/bin/python3", false)]
    public void Runtime_routing_preserves_Alpine_for_other_toolchains(string command, bool expected) =>
        Assert.Equal(expected, FlutterRuntime.IsFlutterCommand(command));
}
