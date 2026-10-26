using TabStudio.LanguageServers.Hosting;
namespace TabStudio.LanguageServers.Tests.Integration;

/// <summary>Requires Dart only in Flutter verification runs, not jobs installing other language servers.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DartServerFactAttribute : FactAttribute
{
    public DartServerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TABSTUDIO_FLUTTER_TESTS") != "1"
            && ProcessServerLauncher.Instance.Resolve("dart") is null)
        {
            Skip = "Install the Flutter/Dart SDK; TABSTUDIO_FLUTTER_TESTS=1 makes it required.";
        }
    }
}
