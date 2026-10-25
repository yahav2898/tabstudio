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

/// <summary>Shows the IDE in a theme until disposed, then goes back to the dark theme every other test expects.</summary>
internal sealed class ThemeScope : IDisposable
{
    private ThemeScope(AppTheme theme)
    {
        AppearanceService.Preview(theme);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Shows the light theme.</summary>
    public static ThemeScope Light() => new(AppTheme.Light);

    public void Dispose()
    {
        AppearanceService.Preview(AppTheme.Dark);
        Dispatcher.UIThread.RunJobs();
    }
}
