using TabStudio.WindowsCompatibility.Sessions;

namespace TabStudio.WindowsCompatibility.Tests.Sessions;

/// <summary>The registry settings a Windows session runs with.</summary>
public sealed class WindowsPrefixTests
{
    [Fact]
    public void Every_app_joins_one_virtual_desktop_of_the_sessions_size()
    {
        var registry = WindowsPrefix.Registry(new WindowsSessionOptions { Width = 1024, Height = 720 });

        Assert.StartsWith("REGEDIT4\n", registry, StringComparison.Ordinal);
        Assert.Contains("[HKEY_CURRENT_USER\\Software\\Wine\\Explorer]\n\"Desktop\"=\"TabStudio\"", registry, StringComparison.Ordinal);
        Assert.Contains("\"TabStudio\"=\"1024x720\"", registry, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1.0, "00000060")]
    [InlineData(1.5, "00000090")]
    [InlineData(2.0, "000000c0")]
    [InlineData(1.6, "00000090")]
    public void The_display_scale_becomes_windows_dots_per_inch_in_quarter_steps(double scale, string dword)
    {
        var registry = WindowsPrefix.Registry(new WindowsSessionOptions { Scale = scale });

        Assert.Contains($"\"LogPixels\"=dword:{dword}", registry, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_draws_in_software_unless_hardware_rendering_is_asked_for()
    {
        Assert.Contains("\"DisableHWAcceleration\"=dword:00000001", WindowsPrefix.Registry(new WindowsSessionOptions()), StringComparison.Ordinal);
        Assert.Contains("\"DisableHWAcceleration\"=dword:00000000", WindowsPrefix.Registry(new WindowsSessionOptions { WpfRendering = WpfRendering.Hardware }), StringComparison.Ordinal);
    }

    [Fact]
    public void A_crash_ends_the_app_instead_of_starting_wines_debugger()
    {
        var registry = WindowsPrefix.Registry(new WindowsSessionOptions());

        Assert.Contains("AeDebug]\n\"Debugger\"=\"\"", registry, StringComparison.Ordinal);
        Assert.DoesNotContain("\"mscoree\"", registry, StringComparison.Ordinal);
    }
}
