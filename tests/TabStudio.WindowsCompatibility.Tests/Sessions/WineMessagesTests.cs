using TabStudio.WindowsCompatibility.Sessions;

namespace TabStudio.WindowsCompatibility.Tests.Sessions;

/// <summary>Telling Wine's debug messages from an app's console output.</summary>
public sealed class WineMessagesTests
{
    [Theory]
    [InlineData("0024:fixme:ole:CoInitializeSecurity stub", true)]
    [InlineData("00f4:err:module:import_dll Library foo.dll not found", true)]
    [InlineData("0100:warn:seh:dispatch_exception", true)]
    [InlineData("Hello from WinForms", false)]
    [InlineData("12:30:00 tick", false)]
    [InlineData("wine: Unhandled page fault on read access to 0000000000000000", false)]
    public void Only_wines_debug_channels_are_noise(string line, bool noise)
    {
        Assert.Equal(noise, WineMessages.IsNoise(line));
    }
}
