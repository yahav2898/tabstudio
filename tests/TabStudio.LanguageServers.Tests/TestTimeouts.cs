namespace TabStudio.LanguageServers.Tests;

/// <summary>How long tests wait for something that should happen at once, generous for a busy CI machine.</summary>
internal static class TestTimeouts
{
    /// <summary>For scripted servers and adapters in the same process.</summary>
    public static TimeSpan Default { get; } = TimeSpan.FromSeconds(20);

    /// <summary>For real servers and adapters, which start slower.</summary>
    public static TimeSpan RealServer { get; } = TimeSpan.FromSeconds(90);
}
