using Avalonia.Threading;

namespace WpfApp1.Porting;

/// <summary>
/// A WPF window, ported: the same <c>Show</c>, <c>Close</c>, <c>Owner</c> and <c>DialogResult</c>, on the desktop
/// and on phones and tablets. Avalonia's dialogs do not block, so <c>ShowDialog()</c> became
/// <see cref="ShowDialogAsync"/>, awaited.
/// </summary>
public class AppWindow : WindowView
{
    private bool? _dialogResult;
    private bool _rendered;

    /// <summary>Raised once the window's content is shown for the first time.</summary>
    public event EventHandler? ContentRendered;

    /// <summary>The app's main window, the one <c>StartupUri</c> named.</summary>
    public static AppWindow? Main => MainWindow as AppWindow;

    /// <summary>The dialog's answer. Setting it on an open dialog closes the dialog, as in WPF.</summary>
    public bool? DialogResult
    {
        get => _dialogResult;
        set
        {
            _dialogResult = value;
            if (IsDialog && value is not null)
            {
                Close();
            }
        }
    }

    /// <summary>The window an element is in, as WPF's <c>Window.GetWindow</c>.</summary>
    /// <param name="element">The element.</param>
    public static AppWindow? GetWindow(Avalonia.StyledElement? element) => Of(element) as AppWindow;

    /// <summary>Opens the window as a dialog and waits until it closes.</summary>
    /// <returns>The <see cref="DialogResult"/>; <see langword="false"/> when the window closed without one, as in WPF.</returns>
    public async Task<bool?> ShowDialogAsync()
    {
        _dialogResult = null;
        await ShowModalAsync();
        return _dialogResult ?? false;
    }

    /// <summary>Raises <see cref="ContentRendered"/>.</summary>
    /// <param name="e">Empty.</param>
    protected virtual void OnContentRendered(EventArgs e) => ContentRendered?.Invoke(this, e);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_rendered)
        {
            _rendered = true;
            Dispatcher.UIThread.Post(() => OnContentRendered(EventArgs.Empty), DispatcherPriority.Background);
        }
    }

    /// <summary>A cancel button closes a dialog with <see langword="false"/>, as in WPF.</summary>
    protected override void OnCancelButtonClick()
    {
        if (IsDialog)
        {
            DialogResult = false;
        }
    }
}
