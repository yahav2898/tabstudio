using System.Globalization;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Swarm;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.ViewModels;

/// <summary>A layout finding as an Error List row: the finding, the row's diagnostic, and where its element is in the XAML.</summary>
/// <param name="finding">The finding, on every screen it shows on.</param>
/// <param name="diagnostic">The row's diagnostic: the check's code and help page, the message, and the XAML element's place.</param>
/// <param name="xaml">Where the element is written, if its XAML was found.</param>
public sealed class LayoutFindingRow(LayoutFinding finding, BuildDiagnostic diagnostic, XamlSourceLocation? xaml)
{
    /// <summary>The finding, on every screen it shows on.</summary>
    public LayoutFinding Finding { get; } = finding;

    /// <summary>The row's diagnostic.</summary>
    public BuildDiagnostic Diagnostic { get; } = diagnostic;

    /// <summary>Where the element is written, if its XAML was found.</summary>
    public XamlSourceLocation? Xaml { get; } = xaml;

    /// <summary>Where the element is written in a web app's page, for a finding on the web preview.</summary>
    public Swarm.Web.WebSourceLocation? WebSource { get; init; }

    /// <summary>The pictures of the screens it shows on, made the first time they are shown.</summary>
    public IReadOnlyList<LayoutScreenshotViewModel> Screenshots => field ??= [.. Finding.Screens.Select(s => new LayoutScreenshotViewModel(s))];
}

/// <summary>One screen's picture of a layout finding, as the Error List's screens button shows it.</summary>
public sealed class LayoutScreenshotViewModel
{
    /// <summary>How wide the picture is drawn, in device-independent pixels.</summary>
    public const double PictureWidth = 220;

    /// <summary>Creates the picture of one screen.</summary>
    /// <param name="screen">The screen's side of the finding.</param>
    public LayoutScreenshotViewModel(ScreenFinding screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        var profile = screen.Profile;
        Title = profile.Name;
        Details = ScreenDetails.Describe(profile);
        Message = screen.Issue.Message;
        var scale = profile.Width > 0 ? PictureWidth / profile.Width : 1;
        PictureHeight = Math.Round(profile.Height * scale);
        var bounds = screen.Issue.Bounds;
        Highlight = new Rect(bounds.X * scale, bounds.Y * scale, Math.Max(4, bounds.Width * scale), Math.Max(4, bounds.Height * scale));
        if (screen.Screenshot is { } png)
        {
            using var stream = new MemoryStream(png);
            Picture = new Bitmap(stream);
        }
    }

    /// <summary>The screen's name.</summary>
    public string Title { get; }

    /// <summary>The screen's size, kind, theme and direction.</summary>
    public string Details { get; }

    /// <summary>What is wrong on this screen.</summary>
    public string Message { get; }

    /// <summary>The screen as it was checked, if its picture could be taken.</summary>
    public Bitmap? Picture { get; }

    /// <summary>Whether there is a picture.</summary>
    public bool HasPicture => Picture is not null;

    /// <summary>How tall the picture is drawn.</summary>
    public double PictureHeight { get; }

    /// <summary>Where the element is in the picture, as drawn.</summary>
    public Rect Highlight { get; }

    /// <summary>The highlight's left edge, for a canvas.</summary>
    public double HighlightLeft => Highlight.X;

    /// <summary>The highlight's top edge, for a canvas.</summary>
    public double HighlightTop => Highlight.Y;
}

/// <summary>An Error List row that the layout checks found, with the screens it shows on.</summary>
public sealed partial record ErrorItemViewModel
{
    /// <summary>For a row from the layout checks, its finding; <see langword="null"/> for other rows.</summary>
    public LayoutFindingRow? Layout { get; init; }

    /// <summary>Whether the layout checks found it.</summary>
    public bool IsFromLayout => Layout is not null;

    /// <summary>The screens it shows on, for the row's badge, such as "Small phone +1".</summary>
    public string? LayoutScreens => Layout?.Finding.Screens is { Count: > 0 } screens
        ? screens.Count == 1 ? screens[0].Profile.Name : string.Create(CultureInfo.CurrentCulture, $"{screens[0].Profile.Name} +{screens.Count - 1}")
        : null;

    /// <summary>The badge's tooltip: every screen it shows on.</summary>
    public string? LayoutScreensToolTip => Layout is { } layout ? $"On {layout.Finding.ScreensText}. Tap to see the screens." : null;

    /// <summary>The pictures of the screens it shows on.</summary>
    public IReadOnlyList<LayoutScreenshotViewModel> LayoutScreenshots => Layout?.Screenshots ?? [];
}

/// <summary>
/// The layout checks' findings in the Error List: one row per element and problem, with the screens it shows on, a
/// code from SWRM001 to SWRM005 whose page explains it, and the XAML element as its location. Opening a row opens the
/// element in the editor and shows it in its screens.
/// </summary>
public sealed partial class ErrorListViewModel
{
    private IReadOnlyList<LayoutFindingRow> _layout = [];
    private Dictionary<BuildDiagnostic, LayoutFindingRow> _layoutByDiagnostic = new(ReferenceEqualityComparer.Instance);

    /// <summary>Raised when the user opens a layout row, so the workspace shows the element in its screens.</summary>
    public event EventHandler<LayoutFindingRow>? LayoutFindingOpened;

    /// <summary>Shows the layout checks' findings, replacing the previous ones.</summary>
    /// <param name="rows">The findings as rows; none clears them.</param>
    public void SetLayoutFindings(IReadOnlyList<LayoutFindingRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _layout = rows;
        _layoutByDiagnostic = new Dictionary<BuildDiagnostic, LayoutFindingRow>(ReferenceEqualityComparer.Instance);
        foreach (var row in rows)
        {
            _layoutByDiagnostic[row.Diagnostic] = row;
        }

        Refresh();
    }

    /// <summary>The layout finding behind a row's diagnostic, when the layout checks reported it.</summary>
    /// <param name="diagnostic">A diagnostic of a row.</param>
    public LayoutFindingRow? LayoutFindingOf(BuildDiagnostic diagnostic) =>
        diagnostic is not null && _layoutByDiagnostic.TryGetValue(diagnostic, out var row) ? row : null;

    /// <summary>The layout checks' diagnostics, listed with the others.</summary>
    private IEnumerable<BuildDiagnostic> LayoutDiagnostics => _layout.Select(r => r.Diagnostic);

    /// <summary>Opening a layout row also shows its element in its screens, even when its XAML was not found.</summary>
    private void OpenLayoutFinding(ErrorItemViewModel item)
    {
        if (item.Layout is { } layout)
        {
            LayoutFindingOpened?.Invoke(this, layout);
        }
    }

    /// <summary>Shows a layout row's element in its screens without opening its XAML, from the row's screens button.</summary>
    /// <param name="item">The row.</param>
    [RelayCommand]
    private void ShowLayoutScreens(ErrorItemViewModel? item)
    {
        if (item is not null)
        {
            OpenLayoutFinding(item);
        }
    }
}

/// <summary>How the IDE describes a screen in a few words.</summary>
public static class ScreenDetails
{
    /// <summary>A screen's size, kind, theme, direction and font scale, such as "360 × 640 · Phone · Dark · RTL".</summary>
    /// <param name="profile">The screen.</param>
    public static string Describe(ScreenProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var parts = new List<string>
        {
            Swarm.LayoutChecks.LayoutText.Dips(new Size(profile.Width, profile.Height)),
            profile.FormFactor switch
            {
                FormFactor.Phablet => "Large phone",
                _ => profile.FormFactor.ToString(),
            },
            profile.IsDark ? "Dark" : "Light",
        };
        if (profile.Direction == TextDirection.RightToLeft)
        {
            parts.Add("RTL");
        }

        if (Math.Abs(profile.FontScale - 1) > 0.01)
        {
            parts.Add(string.Create(CultureInfo.CurrentCulture, $"Text {profile.FontScale:0.##}×"));
        }

        return string.Join(" · ", parts);
    }
}
