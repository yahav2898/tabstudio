using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The IDE shell: menu bar, toolbar, document tabs and editors, the bottom dock, the side bar with the activity bar,
/// and the status bar. Code here is view-only: creating editors, the shell's key bindings and keyboard focus. The
/// layout for the screen's size is in WorkspaceView.Layout.cs, the toolbar's run targets in WorkspaceView.Toolbar.cs
/// and the floating touch bar in WorkspaceView.TouchBar.cs.
/// </summary>
public sealed partial class WorkspaceView : UserControl
{
    private WorkspaceViewModel? _viewModel;
    private KeyBinding? _searchBinding;

    /// <summary>Creates the view.</summary>
    public WorkspaceView()
    {
        InitializeComponent();
        Editors.ViewFactory = document => document switch
        {
            DocumentViewModel { PullRequest: { } pullRequest } => new PullRequests.PullRequestView { DataContext = pullRequest },
            DocumentViewModel { IsComparison: true } comparison => new DiffView(comparison),
            DocumentViewModel { IsNotebook: true } notebook => new Notebooks.NotebookView(notebook),
            DocumentViewModel { DatabaseQuery: not null } query => new Database.DatabaseQueryView(query),
            DocumentViewModel { IsCiLog: true } log => new Actions.CiLogView(log),
            DocumentViewModel { PortSummary: { } summary } => new PortSummaryView { DataContext = summary },
            DocumentViewModel { DeviceMatrix: { } matrix } => new Swarm.DeviceMatrixView { DataContext = matrix },
            DocumentViewModel { Designer: { } designer } xaml => new XamlDesignerView(xaml, designer),
            DocumentViewModel { FormsDesigner: { } forms } form => new FormsDesignerView(form, forms),
            _ => (Control)new CodeEditorView((DocumentViewModel)document),
        };
        PromptBox.KeyDown += OnPromptKeyDown;
        InitializeKeyboard();
        PromptChoices.KeyDown += OnPromptKeyDown;
        PromptChoices.DoubleTapped += OnPromptChoiceDoubleTapped;
        InitializeKeyChords();
        InitializeTestExplorer();
        InitializeExtensions();
        InitializeLayout();
        InitializeToolbar();
        InitializeTouchBar();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Prompt.PropertyChanged -= OnPromptPropertyChanged;
            _viewModel.EditorRebuildRequested -= OnEditorRebuildRequested;
            DetachLayout(_viewModel);
            DetachToolbar(_viewModel);
        }

        if (_searchBinding is not null)
        {
            KeyBindings.Remove(_searchBinding);
            _searchBinding = null;
        }

        _viewModel = DataContext as WorkspaceViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Prompt.PropertyChanged += OnPromptPropertyChanged;
            _viewModel.EditorRebuildRequested += OnEditorRebuildRequested;

            // Ctrl+Q opens the search over the IDE, or closes it, as in Visual Studio.
            _searchBinding = new KeyBinding { Gesture = new KeyGesture(Key.Q, KeyModifiers.Control), Command = _viewModel.Search.ToggleCommand };
            KeyBindings.Add(_searchBinding);

            // Once the menus' bindings have the new data context, which they take after this view does.
            var viewModel = _viewModel;
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () =>
                {
                    if (ReferenceEquals(_viewModel, viewModel))
                    {
                        RegisterCommands(viewModel);
                    }
                },
                Avalonia.Threading.DispatcherPriority.Background);
            AttachLayout(_viewModel);
            AttachToolbar(_viewModel);
            UpdateBottomPanel();
            TrackActiveEditorSoon();
        }
    }

    /// <summary>
    /// Gives the search every command of the menus, with its menu path and key gesture, and the gestures of the key
    /// bindings, once per workspace: the menus stay where the commands are defined.
    /// </summary>
    private void RegisterCommands(WorkspaceViewModel viewModel)
    {
        if (viewModel.Commands.Commands.Count > 0)
        {
            return;
        }

        viewModel.Commands.AddMenuItems(TopMenuItems(), null);
        viewModel.Commands.AddKeyBindings(KeyBindings);
    }

    private void OnEditorRebuildRequested(object? sender, DocumentViewModel document)
    {
        Editors.Rebuild(document);
        TrackActiveEditorSoon();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WorkspaceViewModel.IsSideBarVisible):
                UpdateSideBar();
                break;
            case nameof(WorkspaceViewModel.IsBottomPanelVisible):
            case nameof(WorkspaceViewModel.HasRecording):
                UpdateBottomPanel();
                break;
            case nameof(WorkspaceViewModel.ShowsSideBarProperties):
                UpdateProperties();
                break;
            case nameof(WorkspaceViewModel.ActiveDocument):
                OnActiveDocumentShown();
                TrackActiveEditorSoon();
                break;
            case nameof(WorkspaceViewModel.IsDebugging) or nameof(WorkspaceViewModel.IsTouchBarCollapsed) or nameof(WorkspaceViewModel.ShowsEditorTouchBar):
                ScheduleTouchBarPlacement();
                break;
            case nameof(WorkspaceViewModel.IsTouchInput) when _viewModel?.IsTouchInput == true:
                TouchBarFader.Opacity = 1;
                break;
        }
    }

    private void UpdateBottomPanel()
    {
        var visible = _viewModel?.IsBottomPanelVisible ?? true;
        var row = EditorArea.RowDefinitions[4];
        if (!visible && row.Height.Value > 0)
        {
            _bottomPanelHeight = row.Height;
        }

        row.Height = visible
            ? new GridLength(Math.Max(_bottomPanelHeight.Value, _viewModel?.HasRecording == true ? 320 : 0))
            : new GridLength(0);
        BottomDock.IsVisible = visible;
        BottomSplitter.IsVisible = visible;
    }

    private void OnPromptPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PromptViewModel.IsOpen) && _viewModel?.Prompt.IsOpen == true && _viewModel.Prompt.HasInput)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                PromptBox.Focus();
                PromptBox.SelectAll();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(PromptViewModel.IsOpen) && _viewModel?.Prompt is { IsOpen: true, HasChoices: true })
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => PromptChoices.Focus(), Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    /// <summary>A double tap on a choice chooses it, as Enter does.</summary>
    private void OnPromptChoiceDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel?.Prompt is { HasChoices: true } prompt && prompt.ConfirmCommand.CanExecute(null))
        {
            prompt.ConfirmCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel?.Prompt is not { } prompt)
        {
            return;
        }

        if (e.Key == Key.Enter && prompt.ConfirmCommand.CanExecute(null))
        {
            prompt.ConfirmCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            prompt.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }
}
