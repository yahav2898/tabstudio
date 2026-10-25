using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The IDE shell: menu bar, toolbar, document tabs and editors, the bottom dock, the Solution Explorer and
/// the status bar. Code here is view-only: creating editors, sizing panels and keyboard focus.
/// </summary>
public sealed partial class WorkspaceView : UserControl
{
    private const double SideBarWidth = 330;
    private const double BottomPanelHeight = 200;

    private WorkspaceViewModel? _viewModel;
    private GridLength _sideBarWidth = new(SideBarWidth);
    private GridLength _bottomPanelHeight = new(BottomPanelHeight);

    /// <summary>Creates the view.</summary>
    public WorkspaceView()
    {
        InitializeComponent();
        Editors.ViewFactory = document => document switch
        {
            DocumentViewModel { IsComparison: true } comparison => new DiffView(comparison),
            DocumentViewModel { IsNotebook: true } notebook => new Notebooks.NotebookView(notebook),
            DocumentViewModel { Designer: { } designer } xaml => new XamlDesignerView(xaml, designer),
            DocumentViewModel { FormsDesigner: { } forms } form => new FormsDesignerView(form, forms),
            _ => (Control)new CodeEditorView((DocumentViewModel)document),
        };
        QuickOpenBox.KeyDown += OnQuickOpenKeyDown;
        PromptBox.KeyDown += OnPromptKeyDown;
        QuickOpenList.AddHandler(TappedEvent, OnQuickOpenTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Q, KeyModifiers.Control), Command = new FocusCommand(() => QuickOpenBox.Focus()) });
        InitializeKeyChords();
        InitializeTestExplorer();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Prompt.PropertyChanged -= OnPromptPropertyChanged;
        }

        _viewModel = DataContext as WorkspaceViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Prompt.PropertyChanged += OnPromptPropertyChanged;
            UpdateSideBar();
            UpdateBottomPanel();
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        // On a narrow screen (a phone, or a tablet in portrait) the editor gets the width.
        const double NarrowWidth = 900;
        var wide = e.NewSize.Width >= NarrowWidth;
        var firstLayout = e.PreviousSize.Width <= 0;
        if (_viewModel is not null && e.WidthChanged && (firstLayout || (e.PreviousSize.Width >= NarrowWidth) != wide))
        {
            _viewModel.IsSideBarVisible = wide;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WorkspaceViewModel.IsSideBarVisible):
                UpdateSideBar();
                break;
            case nameof(WorkspaceViewModel.IsBottomPanelVisible):
                UpdateBottomPanel();
                break;
        }
    }

    private void UpdateSideBar()
    {
        var visible = _viewModel?.IsSideBarVisible ?? true;
        var column = MainArea.ColumnDefinitions[2];
        if (!visible && column.Width.Value > 0)
        {
            _sideBarWidth = column.Width;
        }

        column.Width = visible ? _sideBarWidth : new GridLength(0);
        SideBar.IsVisible = visible;
        SideSplitter.IsVisible = visible;
    }

    private void UpdateBottomPanel()
    {
        var visible = _viewModel?.IsBottomPanelVisible ?? true;
        var row = EditorArea.RowDefinitions[4];
        if (!visible && row.Height.Value > 0)
        {
            _bottomPanelHeight = row.Height;
        }

        row.Height = visible ? _bottomPanelHeight : new GridLength(0);
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

    private void OnQuickOpenKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                _viewModel.OpenQuickOpenItemCommand.Execute(QuickOpenList.SelectedItem as QuickOpenItem);
                e.Handled = true;
                break;
            case Key.Down when _viewModel.QuickOpenResults.Count > 0:
                QuickOpenList.SelectedIndex = Math.Min(QuickOpenList.SelectedIndex + 1, _viewModel.QuickOpenResults.Count - 1);
                e.Handled = true;
                break;
            case Key.Up when QuickOpenList.SelectedIndex > 0:
                QuickOpenList.SelectedIndex--;
                e.Handled = true;
                break;
            case Key.Escape:
                _viewModel.QuickOpenText = string.Empty;
                e.Handled = true;
                break;
        }
    }

    private void OnQuickOpenTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is QuickOpenItem item)
        {
            _viewModel?.OpenQuickOpenItemCommand.Execute(item);
        }
    }

    /// <summary>A command that moves keyboard focus, for shortcuts that are view-only.</summary>
    private sealed class FocusCommand(Action focus) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => focus();
    }
}
