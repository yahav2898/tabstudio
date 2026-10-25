using System.Text.Json;
using TabStudio.WindowsCompatibility.Building;

namespace TabStudio.WindowsCompatibility.Tests.Building;

/// <summary>The runtime configuration a Windows desktop app starts with.</summary>
public sealed class RuntimeConfigTests
{
    [Fact]
    public void Names_the_windows_desktop_framework_of_the_apps_dotnet_version()
    {
        using var json = JsonDocument.Parse(RuntimeConfig.Create(10));
        var options = json.RootElement.GetProperty("runtimeOptions");

        Assert.Equal("net10.0", options.GetProperty("tfm").GetString());
        Assert.Equal("Microsoft.WindowsDesktop.App", options.GetProperty("framework").GetProperty("name").GetString());
        Assert.Equal("10.0.0", options.GetProperty("framework").GetProperty("version").GetString());
    }

    [Theory]
    [InlineData("net10.0-windows", 10)]
    [InlineData("net9.0-windows10.0.19041.0", 9)]
    [InlineData("net8.0", 8)]
    [InlineData("netcoreapp3.1", 10)]
    public void Reads_the_dotnet_version_of_a_target_framework(string framework, int major)
    {
        Assert.Equal(major, RuntimeConfig.MajorVersion(framework));
    }
}
