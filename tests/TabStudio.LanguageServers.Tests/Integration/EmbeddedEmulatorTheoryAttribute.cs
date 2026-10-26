namespace TabStudio.LanguageServers.Tests.Integration;

/// <summary>Runs hardware-free firmware debugging only when the dedicated emulator job explicitly enables it.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EmbeddedEmulatorTheoryAttribute : TheoryAttribute
{
    /// <summary>Creates an optional local test that is mandatory in the embedded emulator CI job.</summary>
    public EmbeddedEmulatorTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("TABSTUDIO_EMBEDDED_EMULATORS") != "1")
        {
            Skip = "Needs the embedded emulator toolchains; set TABSTUDIO_EMBEDDED_EMULATORS=1 to require all three real DAP sessions.";
        }
    }
}
