using Avalonia.Threading;
using TabStudio.App.Services.Appearance;

namespace TabStudio.App.Tests.Theming;

/// <summary>
/// The IDE's theme is app-wide, so tests that switch it run one at a time, after the tests that run in parallel.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppearanceTestGroup
{
    /// <summary>The collection's name.</summary>
    public const string Name = "Appearance";
}

/// <summary>The theme the tests run in: dark, or light when <c>TABSTUDIO_THEME</c> is <c>Light</c>.</summary>
internal static class TestTheme
{
    /// <summary>The theme every test starts in and goes back to.</summary>
    public static IdeTheme Baseline { get; } =
        string.Equals(Environment.GetEnvironmentVariable("TABSTUDIO_THEME"), "Light", StringComparison.OrdinalIgnoreCase) ? IdeTheme.Light : IdeTheme.Dark;
}

/// <summary>Shows the IDE in a theme until disposed, then goes back to the theme every other test expects.</summary>
internal sealed class ThemeScope : IDisposable
{
    private ThemeScope(IdeTheme theme)
    {
        AppearanceService.Preview(theme);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Shows the light theme.</summary>
    public static ThemeScope Light() => new(IdeTheme.Light);

    public void Dispose()
    {
        AppearanceService.Preview(TestTheme.Baseline);
        Dispatcher.UIThread.RunJobs();
    }
}
