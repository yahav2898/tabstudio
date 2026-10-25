using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Agent;
using TabStudio.App.Previews;
using TabStudio.App.Services.Agent;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>How the XAML designer splits a XAML file's tab, as Visual Studio's Design, split and XAML buttons do.</summary>
public enum XamlDesignerMode
{
    /// <summary>The preview and the XAML side by side, or one above the other.</summary>
    Split,

    /// <summary>Only the preview.</summary>
    Design,

    /// <summary>Only the XAML.</summary>
    Xaml,
}

/// <summary>
/// The XAML designer of an Avalonia view: the XAML being edited, rendered live beside it, with the app's own
/// types, theme and design-time data. It renders a moment after typing stops, against the project's last build,
/// and builds the project again in the background when its code is saved.
/// </summary>
public sealed partial class XamlDesignerViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan TypingPause = TimeSpan.FromMilliseconds(350);

    private readonly DocumentViewModel _document;
    private readonly ProjectInfo _project;
    private readonly XamlDesignBuilds _builds;
    private readonly string _path;
    private readonly DispatcherTimer _typing;
    private XamlDesignBuild? _build;
    private AppTheme? _theme;
    private bool _disposed;

    internal XamlDesignerViewModel(DocumentViewModel document, ProjectInfo project, XamlDesignBuilds builds, SketchConverter sketches, Action openAgentSettings)
    {
        _document = document;
        _project = project;
        _builds = builds;
        Sketch = new SketchViewModel(sketches, openAgentSettings);
        Sketch.Converted += OnSketchConverted;
        Sketch.PropertyChanged += OnSketchPropertyChanged;
        _path = "/" + Path.GetRelativePath(project.DirectoryPath, document.FilePath).Replace('\\', '/');
        _typing = new DispatcherTimer { Interval = TypingPause };
        _typing.Tick += OnTypingPaused;
        _document.Document.TextChanged += OnTextChanged;
        _builds.Changed += OnBuildsChanged;
        _ = LoadBuildAsync();
    }

    /// <summary>Whether the preview and the XAML are both shown, or only one of them.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSplit), nameof(IsDesignOnly), nameof(IsXamlOnly))]
    public partial XamlDesignerMode Mode { get; set; }

    /// <summary>Whether the split puts the preview beside the XAML rather than above it.</summary>
    [ObservableProperty]
    public partial bool IsSideBySide { get; set; } = true;

    /// <summary>Whether a preview with a design size is scaled to fit the pane rather than drawn at its real size.</summary>
    [ObservableProperty]
    public partial bool FitToPane { get; set; } = true;

    /// <summary>Why the XAML could not be rendered; the last good preview stays visible under it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(ErrorLocation))]
    public partial string? Error { get; private set; }

    /// <summary>The line of <see cref="Error"/>, if known.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorLocation))]
    [NotifyCanExecuteChangedFor(nameof(GoToErrorCommand))]
    public partial int? ErrorLine { get; private set; }

    /// <summary>How the preview differs from the running app, when it does.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    public partial string? Note { get; private set; }

    /// <summary>What the designer is doing: building, rendered in how long, or the build's problem.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Building the project for the designer";

    /// <summary>Whether the project is being built for the designer.</summary>
    [ObservableProperty]
    public partial bool IsBuilding { get; private set; } = true;

    /// <summary>The width the XAML asks for at design time (<c>d:DesignWidth</c>), or NaN to fill the pane.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDesignSize))]
    public partial double DesignWidth { get; private set; } = double.NaN;

    /// <summary>The height the XAML asks for at design time (<c>d:DesignHeight</c>), or NaN to fill the pane.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDesignSize))]
    public partial double DesignHeight { get; private set; } = double.NaN;

    /// <summary>What Claude said about the last sketch it turned into XAML, until dismissed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSketchNotes))]
    public partial string? SketchNotes { get; private set; }

    /// <summary>Sketch to XAML: drawing a screen that becomes this view's XAML.</summary>
    public SketchViewModel Sketch { get; }

    /// <summary>Whether there are notes about a sketch to show.</summary>
    public bool HasSketchNotes => SketchNotes is not null;

    /// <summary>The rendered XAML inside the app's theme, or <see langword="null"/> before the first preview.</summary>
    public Control? Preview => _theme?.Root;

    /// <summary>Whether the code can be edited now: it is shown, and the sketch pad does not cover it.</summary>
    public bool ShowsCode => Mode != XamlDesignerMode.Design && !Sketch.IsOpen;

    /// <summary>Whether both panes are shown.</summary>
    public bool IsSplit => Mode == XamlDesignerMode.Split;

    /// <summary>Whether only the preview is shown.</summary>
    public bool IsDesignOnly => Mode == XamlDesignerMode.Design;

    /// <summary>Whether only the XAML is shown.</summary>
    public bool IsXamlOnly => Mode == XamlDesignerMode.Xaml;

    /// <summary>Whether there is an error to show over the preview.</summary>
    public bool HasError => Error is not null;

    /// <summary>"Line 12", for the error banner.</summary>
    public string? ErrorLocation => ErrorLine is { } line ? string.Create(CultureInfo.CurrentCulture, $"Line {line}") : null;

    /// <summary>Whether there is a note to show under the preview.</summary>
    public bool HasNote => Note is not null;

    /// <summary>Whether the XAML asks for a design size, which the zoom button then scales.</summary>
    public bool HasDesignSize => !double.IsNaN(DesignWidth) || !double.IsNaN(DesignHeight);

    private int? ErrorColumn { get; set; }

    /// <summary>Renders the XAML again now.</summary>
    [RelayCommand]
    public void Refresh()
    {
        _typing.Stop();
        Render();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _typing.Stop();
        _typing.Tick -= OnTypingPaused;
        _document.Document.TextChanged -= OnTextChanged;
        _builds.Changed -= OnBuildsChanged;
        Sketch.Converted -= OnSketchConverted;
        Sketch.PropertyChanged -= OnSketchPropertyChanged;
        Sketch.Dispose();
        if (_theme is not null)
        {
            _theme.Content = null;
        }
    }

    [RelayCommand]
    private void ShowSplit() => Mode = XamlDesignerMode.Split;

    [RelayCommand]
    private void ShowDesign() => Mode = XamlDesignerMode.Design;

    [RelayCommand]
    private void ShowXaml() => Mode = XamlDesignerMode.Xaml;

    [RelayCommand]
    private void ToggleOrientation() => IsSideBySide = !IsSideBySide;

    [RelayCommand]
    private void ToggleFit() => FitToPane = !FitToPane;

    /// <summary>Moves the caret to the error in the XAML.</summary>
    [RelayCommand(CanExecute = nameof(CanGoToError))]
    private void GoToError()
    {
        if (ErrorLine is { } line)
        {
            if (Mode == XamlDesignerMode.Design)
            {
                Mode = XamlDesignerMode.Split;
            }

            _document.NavigateTo(line, ErrorColumn ?? 1);
        }
    }

    private bool CanGoToError() => ErrorLine is not null;

    [RelayCommand]
    private void DismissSketchNotes() => SketchNotes = null;

    partial void OnModeChanged(XamlDesignerMode value) => CodeVisibilityChanged();

    private void OnSketchPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SketchViewModel.IsOpen))
        {
            CodeVisibilityChanged();
        }
    }

    private void CodeVisibilityChanged()
    {
        OnPropertyChanged(nameof(ShowsCode));
        _document.OnDesignerLayoutChanged();
    }

    /// <summary>
    /// Puts the XAML Claude wrote for a sketch in as the view's content, as one edit that Undo takes back, and
    /// previews it at once.
    /// </summary>
    private void OnSketchConverted(object? sender, SketchResult result)
    {
        if (XamlContentEdit.ReplaceContent(_document.Document.Text, result.Xaml) is not { } change)
        {
            SketchNotes = "The sketch's XAML could not be placed: this file has no root element to put it in.";
            return;
        }

        _document.Document.Replace(change.Offset, change.Length, change.Text);
        SketchNotes = string.IsNullOrWhiteSpace(result.Notes)
            ? "Claude wrote your sketch as this view's content. Undo brings the previous XAML back."
            : $"{result.Notes} Undo brings the previous XAML back.";
        if (Mode == XamlDesignerMode.Xaml)
        {
            Mode = XamlDesignerMode.Split;
        }

        Refresh();
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        // Rendered once typing pauses, so every keystroke does not compile XAML.
        _typing.Stop();
        _typing.Start();
    }

    private void OnTypingPaused(object? sender, EventArgs e)
    {
        _typing.Stop();
        Render();
    }

    private void OnBuildsChanged(object? sender, string projectFilePath)
    {
        if (string.Equals(projectFilePath, _project.FilePath, StringComparison.Ordinal))
        {
            _ = LoadBuildAsync();
        }
    }

    /// <summary>Gets the project's latest build, then renders with it and the app's theme.</summary>
    private async Task LoadBuildAsync()
    {
        IsBuilding = true;
        Status = $"Building {_project.Name} for the designer";
        var build = await _builds.GetAsync(_project);
        if (_disposed)
        {
            return;
        }

        IsBuilding = false;
        if (!ReferenceEquals(build, _build) || _theme is null)
        {
            _build = build;
            if (_theme is not null)
            {
                _theme.Content = null;
            }

            _theme = new AppTheme(build?.CreateApp());
            OnPropertyChanged(nameof(Preview));
        }

        Render();
    }

    private void Render()
    {
        if (_disposed || _theme is null)
        {
            return;
        }

        var clock = Stopwatch.StartNew();
        var preview = XamlPreviewRenderer.Render(_document.Document.Text, _path, _build);
        Error = preview.Error;
        ErrorLine = preview.ErrorLine;
        ErrorColumn = preview.ErrorColumn;
        if (preview.Error is null)
        {
            // An error keeps the last good preview, as Visual Studio's designer does.
            _theme.Content = preview.View;
            Note = preview.Note;
            DesignWidth = preview.DesignWidth ?? double.NaN;
            DesignHeight = preview.DesignHeight ?? double.NaN;
        }

        Status = StatusAfter(preview, clock.ElapsedMilliseconds);
    }

    /// <summary>What the status line says after a render: the error, the build's problem, or how long it took.</summary>
    private string StatusAfter(XamlPreview preview, long milliseconds)
    {
        if (preview.Error is not null)
        {
            return "The XAML has an error; the preview shows it as it last rendered";
        }

        if (_builds.BuildError(_project) is { } buildError)
        {
            return _build is null
                ? $"{_project.Name} does not build yet, so the preview has only Avalonia's own types: {FirstLine(buildError)}"
                : $"{_project.Name} has build errors; the preview uses its last build: {FirstLine(buildError)}";
        }

        return _build is null
            ? "Previewing without a build of the project"
            : string.Create(CultureInfo.CurrentCulture, $"Rendered in {milliseconds} ms");
    }

    private static string FirstLine(string text) => text.Split('\n', 2)[0].Trim();
}
