using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Toolbox;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels.DesktopMode;

/// <summary>The companion's panes, of which a tablet in portrait shows one at a time.</summary>
public enum CompanionPane
{
    /// <summary>The Toolbox.</summary>
    Toolbox,

    /// <summary>The Properties panel.</summary>
    Properties,
}

/// <summary>
/// The tablet in desktop mode: a touch Toolbox and Properties panel for what is selected on the big screen, and big
/// buttons for Run, Stop, Build and the debugger's steps. It follows the workspace's active document and its caret:
/// in a XAML file the Properties panel shows the element at the caret, otherwise what the Solution Explorer selects.
/// </summary>
public sealed partial class DesktopCompanionViewModel : ObservableObject, IDisposable
{
    private readonly ToolboxCatalog _catalog;
    private DocumentViewModel? _document;
    private bool _refreshQueued;
    private bool _caretMoved;

    /// <summary>Creates the companion.</summary>
    /// <param name="catalog">The Toolbox's controls.</param>
    /// <param name="desktop">Desktop mode, which gets the keyboard back after the companion used it.</param>
    public DesktopCompanionViewModel(ToolboxCatalog catalog, DesktopModeViewModel desktop)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(desktop);
        _catalog = catalog;
        Desktop = desktop;
        Toolbox = new ToolboxViewModel(catalog);
        Element = new XamlPropertiesViewModel();
        Toolbox.Inserted += OnInserted;
        Element.EditFinished += OnEditFinished;
        Desktop.PropertyChanged += OnDesktopPropertyChanged;
    }

    /// <summary>Desktop mode, for the companion's header.</summary>
    public DesktopModeViewModel Desktop { get; }

    /// <summary>The open workspace, or <see langword="null"/> while the big screen shows the start screen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWorkspace), nameof(Selection), nameof(ShowsKeyboard))]
    public partial WorkspaceViewModel? Workspace { get; set; }

    /// <summary>Whether a workspace is open.</summary>
    public bool HasWorkspace => Workspace is not null;

    /// <summary>Whether Start and Build are offered: nothing is being debugged.</summary>
    public bool ShowsStart => Workspace is { IsDebugging: false };

    /// <summary>Whether Run (without debugging) is offered: no program is running.</summary>
    public bool ShowsRun => Workspace is { IsDebugging: false, IsRunning: false };

    /// <summary>Whether Continue is offered: the debugged program is paused.</summary>
    public bool ShowsContinue => Workspace is { IsPaused: true };

    /// <summary>Whether Break is offered: the debugged program is running.</summary>
    public bool ShowsBreak => Workspace is { IsDebuggeeRunning: true };

    /// <summary>Whether the steps and Restart are offered: a program is being debugged.</summary>
    public bool ShowsSteps => Workspace is { IsDebugging: true };

    /// <summary>Whether Stop is offered: a program runs, under the debugger or not.</summary>
    public bool ShowsStop => Workspace is { IsDebugging: true } or { IsRunning: true };

    /// <summary>The Toolbox of the active document's UI framework.</summary>
    public ToolboxViewModel Toolbox { get; }

    /// <summary>The properties of the XAML element at the caret.</summary>
    public XamlPropertiesViewModel Element { get; }

    /// <summary>The properties of what the Solution Explorer selects: a file, a project, the solution.</summary>
    public PropertiesViewModel? Selection => Workspace?.Properties;

    /// <summary>
    /// Whether the Properties panel shows the XAML element at the caret rather than the Solution Explorer's selection:
    /// whichever the user picked last on the big screen.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSelection))]
    public partial bool ShowsElement { get; private set; }

    /// <summary>Whether the Properties panel shows the Solution Explorer's selection.</summary>
    public bool ShowsSelection => !ShowsElement;

    /// <summary>The pane a narrow tablet shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToolboxPane), nameof(IsPropertiesPane))]
    public partial CompanionPane Pane { get; set; }

    /// <summary>Whether a narrow tablet shows the Toolbox.</summary>
    public bool IsToolboxPane => Pane == CompanionPane.Toolbox;

    /// <summary>Whether a narrow tablet shows the Properties panel.</summary>
    public bool IsPropertiesPane => Pane == CompanionPane.Properties;

    /// <inheritdoc />
    public void Dispose()
    {
        Workspace = null;
        Toolbox.Inserted -= OnInserted;
        Element.EditFinished -= OnEditFinished;
        Desktop.PropertyChanged -= OnDesktopPropertyChanged;
    }

    [RelayCommand]
    private void ShowToolbox() => Pane = CompanionPane.Toolbox;

    [RelayCommand]
    private void ShowProperties() => Pane = CompanionPane.Properties;

    partial void OnWorkspaceChanged(WorkspaceViewModel? oldValue, WorkspaceViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnWorkspacePropertyChanged;
            oldValue.Explorer.SelectionChanged -= OnExplorerSelectionChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnWorkspacePropertyChanged;
            newValue.Explorer.SelectionChanged += OnExplorerSelectionChanged;
        }

        Track(newValue?.ActiveDocument);
        NotifyRunState();
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkspaceViewModel.ActiveDocument))
        {
            Track(Workspace?.ActiveDocument);
        }
        else if (e.PropertyName == nameof(WorkspaceViewModel.State))
        {
            NotifyRunState();
        }
    }

    private void NotifyRunState()
    {
        OnPropertyChanged(nameof(ShowsStart));
        OnPropertyChanged(nameof(ShowsRun));
        OnPropertyChanged(nameof(ShowsContinue));
        OnPropertyChanged(nameof(ShowsBreak));
        OnPropertyChanged(nameof(ShowsSteps));
        OnPropertyChanged(nameof(ShowsStop));
    }

    private void OnExplorerSelectionChanged(object? sender, WorkspaceNode? node) => ShowsElement = false;

    /// <summary>Follows another document: the Toolbox of its framework, and the element at its caret.</summary>
    private void Track(DocumentViewModel? document)
    {
        if (_document is not null)
        {
            _document.PropertyChanged -= OnDocumentPropertyChanged;
            _document.Document.TextChanged -= OnTextChanged;
        }

        _document = document;
        if (document is not null)
        {
            document.PropertyChanged += OnDocumentPropertyChanged;
            document.Document.TextChanged += OnTextChanged;
        }

        Toolbox.Track(document);
        Element.Track(document, _catalog.ForFile(document?.FilePath));
        ShowsElement = Element.HasElement;
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentViewModel.CaretLine) or nameof(DocumentViewModel.CaretColumn))
        {
            _caretMoved = true;
            QueueRefresh();
        }
    }

    private void OnTextChanged(object? sender, EventArgs e) => QueueRefresh();

    /// <summary>Reads the element at the caret again once the editor has settled, at most once per batch of changes.</summary>
    private void QueueRefresh()
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _refreshQueued = false;
                Element.Refresh();
                if (_caretMoved || !Element.HasElement)
                {
                    ShowsElement = Element.HasElement;
                }

                _caretMoved = false;
            },
            DispatcherPriority.Background);
    }

    private void OnInserted(object? sender, EventArgs e)
    {
        _caretMoved = true;
        QueueRefresh();
        Desktop.ReturnKeyboardToDisplay();
    }

    private void OnEditFinished(object? sender, EventArgs e) => Desktop.ReturnKeyboardToDisplay();
}
