using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class EmbeddedBox64Tests
{
    [Fact]
    public void SerialGuestLibraryIsCompiledForX64AndExplicitlyRoutedIntoGlibc()
    {
        var command = EmbeddedBox64Installation.SerialShimBuildCommand;
        Assert.Equal("x86_64-linux-gnu-gcc", command.Program);
        Assert.Contains("/opt/tabstudio/embedded/serial_posix.c", command.Arguments);
        Assert.Contains("/opt/tabstudio/embedded/serial-posix-x86_64.so", command.Arguments);
        Assert.Equal("1", command.Environment["TABSTUDIO_EMBEDDED_GLIBC"]);
    }

    [Fact]
    public void NativeBuildRemainsUnmarkedAndExplicitModeUsesLiteralWrapper()
    {
        var project = new EmbeddedProject(EmbeddedProjectKind.PlatformIO, "/project", "platformio.ini", [], null, null, []);
        var native = EmbeddedBuildPlanner.Plan(project, new EmbeddedBuildOptions());
        Assert.False(native.Build[0].Environment.ContainsKey(EmbeddedBox64Installation.Marker));
        var boxed = EmbeddedBuildPlanner.Plan(project, new EmbeddedBuildOptions { HostPackageMode = "boxed-x86_64" });
        var command = EmbeddedBox64Installation.Wrap(boxed.Build[0]);
        Assert.Equal("/opt/tabstudio/platformio/bin/python", command.Program);
        Assert.Equal(new[] { "-P", "/opt/tabstudio/embedded/platformio_box64.py", "--", "run", "--jobs", "2" }, command.Arguments);
        Assert.Equal("/project", command.WorkingDirectory);
    }

    [Fact]
    public void UnknownOrNonPlatformIoModesFailBeforeRunningAnything()
    {
        var project = new EmbeddedProject(EmbeddedProjectKind.Arduino, "/project", "example.ino", [], null, null, []);
        Assert.Throws<ArgumentException>(() => EmbeddedBuildPlanner.Plan(project, new EmbeddedBuildOptions { HostPackageMode = "x64" }));
        Assert.Throws<ArgumentException>(() => EmbeddedBuildPlanner.Plan(project, new EmbeddedBuildOptions { HostPackageMode = "boxed-x86_64" }));
    }

    [Fact]
    public void SerialWorkerKeepsOuterResourcesAndWrapsOnlyTheInnerPlatformIoCommand()
    {
        var command = new ProcessStartSpec
        {
            Program = "python3", Arguments = ["/opt/tabstudio/embedded/serial_bridge.py", "--port", "/private/tty", "--", "pio", "run"],
            Environment = new Dictionary<string, string?> { [EmbeddedBox64Installation.Marker] = "1" },
            InheritedDescriptors = new Dictionary<int, int> { [12] = 3 },
        };
        var wrapped = EmbeddedBox64Installation.Wrap(command);
        Assert.Equal("python3", wrapped.Program);
        Assert.Equal(3, wrapped.InheritedDescriptors[12]);
        Assert.Single(wrapped.InheritedDescriptors);
        Assert.Equal(new[] { "/opt/tabstudio/embedded/serial_bridge.py", "--port", "/private/tty", "--", "/opt/tabstudio/platformio/bin/python", "-P", "/opt/tabstudio/embedded/platformio_box64.py", "--", "run" }, wrapped.Arguments);
    }
}
