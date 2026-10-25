using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using TabStudio.App.ViewModels.Agent;

namespace TabStudio.App.Views;

/// <summary>The agent tool window. Ctrl+Enter sends; the conversation follows the newest message.</summary>
public sealed partial class AgentPanelView : UserControl
{
    private AgentPanelViewModel? _agent;

    /// <summary>Creates the view.</summary>
    public AgentPanelView()
    {
        InitializeComponent();
        Input.KeyDown += OnInputKeyDown;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_agent is not null)
        {
            _agent.ContentGrew -= OnContentGrew;
            _agent.FocusRequested -= OnFocusRequested;
        }

        _agent = DataContext as AgentPanelViewModel;
        if (_agent is not null)
        {
            _agent.ContentGrew += OnContentGrew;
            _agent.FocusRequested += OnFocusRequested;
        }
    }

    private void OnContentGrew(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => Messages.ScrollToEnd(), DispatcherPriority.Background);

    private void OnFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => Input.Focus(), DispatcherPriority.Background);

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control) && _agent is { } agent && agent.SendCommand.CanExecute(null))
        {
            agent.SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}
