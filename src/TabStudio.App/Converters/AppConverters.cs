using Avalonia.Data.Converters;
using Avalonia.Media;
using TabStudio.App.Editing;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Converters;

/// <summary>Value converters used by the views. Each looks up its brushes and icons in the app's theme.</summary>
public static class AppConverters
{
    /// <summary>The icon geometry for a Solution Explorer row.</summary>
    public static IValueConverter ExplorerIconGeometry { get; } = new FuncValueConverter<ExplorerIcon, Geometry?>(icon => Find<Geometry>(icon switch
    {
        ExplorerIcon.Solution => "Icon.Solution",
        ExplorerIcon.Folder => "Icon.Folder",
        ExplorerIcon.AppProject => "Icon.Globe",
        ExplorerIcon.LibraryProject => "Icon.Cube",
        ExplorerIcon.TestProject => "Icon.Flask",
        ExplorerIcon.Dependencies => "Icon.Cube",
        ExplorerIcon.Package => "Icon.Package",
        ExplorerIcon.ProjectReference => "Icon.Link",
        _ => "Icon.File",
    }));

    /// <summary>The icon color for a Solution Explorer row.</summary>
    public static IValueConverter ExplorerIconBrush { get; } = new FuncValueConverter<ExplorerIcon, IBrush?>(icon => Find<IBrush>(icon switch
    {
        ExplorerIcon.Solution => "Brush.Solution",
        ExplorerIcon.Folder => "Brush.Folder",
        ExplorerIcon.AppProject => "Brush.ProjectApp",
        ExplorerIcon.LibraryProject => "Brush.ProjectLibrary",
        ExplorerIcon.TestProject => "Brush.ProjectTest",
        _ => "Brush.TextMuted",
    }));

    /// <summary>The text color of a file badge.</summary>
    public static IValueConverter BadgeForeground { get; } = new FuncValueConverter<BadgeKind, IBrush?>(kind => Find<IBrush>(kind switch
    {
        BadgeKind.CSharp => "Brush.Badge.CSharp",
        BadgeKind.Data => "Brush.Badge.Data",
        BadgeKind.Markup => "Brush.Badge.Markup",
        BadgeKind.Script => "Brush.Badge.Script",
        BadgeKind.Notebook => "Brush.Badge.Notebook",
        _ => "Brush.Badge.Text",
    }));

    /// <summary>
    /// The background of a file badge: only C# and notebooks have a filled badge, as in Visual Studio's file icons.
    /// </summary>
    public static IValueConverter BadgeBackground { get; } = new FuncValueConverter<BadgeKind, IBrush?>(kind => kind switch
    {
        BadgeKind.CSharp => Find<IBrush>("Brush.Badge.CSharpBackground"),
        BadgeKind.Notebook => Find<IBrush>("Brush.Badge.NotebookBackground"),
        _ => Brushes.Transparent,
    });

    /// <summary>The status bar icon for the IDE state.</summary>
    public static IValueConverter StateIcon { get; } = new FuncValueConverter<IdeState, Geometry?>(state => Find<Geometry>(state switch
    {
        IdeState.Busy => "Icon.Build",
        IdeState.Running => "Icon.Play",
        IdeState.Failed => "Icon.Error",
        IdeState.Debugging => "Icon.Bug",
        IdeState.Paused => "Icon.Pause",
        _ => "Icon.Check",
    }));

    private static T? Find<T>(string key)
        where T : class =>
        ThemeBrushes.Find<T>(key);
}
