using System.ComponentModel;
using Avalonia.Threading;

namespace WinFormsApp1.Porting;

/// <summary>
/// A Windows Forms form, ported: the same <c>Text</c>, <c>Load</c>, <c>FormClosing</c>, <c>Close</c> and
/// <c>DialogResult</c>, on the desktop and on phones and tablets. Avalonia's dialogs do not block, so
/// <c>ShowDialog()</c> became <see cref="ShowDialogAsync"/>, awaited.
/// </summary>
public class AppForm : WindowView
{
    private DialogResult _dialogResult;
    private bool _loaded;
    private bool _closingApproved;
    private bool _closePending;

    /// <summary>Awaited closing handlers, including confirmation dialogs.</summary>
    public event Func<object?, FormClosingEventArgs, Task>? FormClosingAsync;

    /// <summary>Raised before the form is shown for the first time.</summary>
    public event EventHandler? Load;

    /// <summary>Raised once the form has been shown for the first time.</summary>
    public event EventHandler? Shown;

    /// <summary>Raised when the form is about to close; set <see cref="CancelEventArgs.Cancel"/> to keep it open.</summary>
    public event FormClosingEventHandler? FormClosing;

    /// <summary>Raised after the form closed.</summary>
    public event FormClosedEventHandler? FormClosed;

    /// <summary>The app's main form, the one <c>Application.Run</c> started.</summary>
    public static AppForm? Main => MainWindow as AppForm;

    /// <summary>The form's title.</summary>
    public string Text
    {
        get => Title ?? string.Empty;
        set => Title = value;
    }

    /// <summary>The dialog's answer. Setting it on an open dialog closes the dialog, as in Windows Forms.</summary>
    public DialogResult DialogResult
    {
        get => _dialogResult;
        set
        {
            _dialogResult = value;
            if (IsDialog && value != DialogResult.None)
            {
                Close();
            }
        }
    }

    /// <summary>Ends the app, as <c>Application.Exit</c> did.</summary>
    public static void Exit() => Shutdown();

    /// <summary>Opens the form as a dialog and waits until it closes.</summary>
    /// <returns>The <see cref="DialogResult"/>; <see cref="DialogResult.Cancel"/> when the form closed without one, as in Windows Forms.</returns>
    public async Task<DialogResult> ShowDialogAsync()
    {
        _dialogResult = DialogResult.None;
        await ShowModalAsync();
        return _dialogResult == DialogResult.None ? DialogResult.Cancel : _dialogResult;
    }

    /// <summary>Raises <see cref="Load"/>.</summary>
    /// <param name="e">Empty.</param>
    protected virtual void OnLoad(EventArgs e) => Load?.Invoke(this, e);

    /// <summary>Raises <see cref="Shown"/>.</summary>
    /// <param name="e">Empty.</param>
    protected virtual void OnShown(EventArgs e) => Shown?.Invoke(this, e);

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        var closing = new FormClosingEventArgs(CloseReason.UserClosing, e.Cancel);
        FormClosing?.Invoke(this, closing);
        e.Cancel = closing.Cancel;
        if (!_closingApproved && !e.Cancel && FormClosingAsync is { } handlers)
        {
            e.Cancel = true;
            if (!_closePending)
            {
                _closePending = true;
                CompleteCloseAsync(handlers, closing);
            }
        }
    }

    private async void CompleteCloseAsync(Func<object?, FormClosingEventArgs, Task> handlers, FormClosingEventArgs args)
    {
        try
        {
            foreach (var handler in handlers.GetInvocationList().Cast<Func<object?, FormClosingEventArgs, Task>>())
            {
                await handler(this, args);
            }

            if (!args.Cancel)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _closingApproved = true;
                    Close();
                    _closingApproved = false;
                });
            }
        }
        finally
        {
            _closePending = false;
        }
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        FormClosed?.Invoke(this, new FormClosedEventArgs(CloseReason.UserClosing));
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_loaded)
        {
            _loaded = true;
            OnLoad(EventArgs.Empty);
            Dispatcher.UIThread.Post(() => OnShown(EventArgs.Empty), DispatcherPriority.Background);
        }
    }
}

/// <summary>The answer of a dialog or a message box, as in Windows Forms.</summary>
public enum DialogResult
{
    /// <summary>No answer yet.</summary>
    None,

    /// <summary>OK.</summary>
    OK,

    /// <summary>Cancel.</summary>
    Cancel,

    /// <summary>Abort.</summary>
    Abort,

    /// <summary>Retry.</summary>
    Retry,

    /// <summary>Ignore.</summary>
    Ignore,

    /// <summary>Yes.</summary>
    Yes,

    /// <summary>No.</summary>
    No,

    /// <summary>Try again.</summary>
    TryAgain,

    /// <summary>Continue.</summary>
    Continue,
}

/// <summary>Why a form closes.</summary>
public enum CloseReason
{
    /// <summary>Unknown.</summary>
    None,

    /// <summary>The user or the app closed it.</summary>
    UserClosing,

    /// <summary>The app is ending.</summary>
    ApplicationExitCall,
}

/// <summary>Handles <see cref="AppForm.FormClosing"/>.</summary>
/// <param name="sender">The form.</param>
/// <param name="e">Set <see cref="CancelEventArgs.Cancel"/> to keep the form open.</param>
public delegate void FormClosingEventHandler(object? sender, FormClosingEventArgs e);

/// <summary>Handles <see cref="AppForm.FormClosed"/>.</summary>
/// <param name="sender">The form.</param>
/// <param name="e">Why it closed.</param>
public delegate void FormClosedEventHandler(object? sender, FormClosedEventArgs e);

/// <summary>A form is about to close.</summary>
/// <param name="closeReason">Why.</param>
/// <param name="cancel">Whether it already stays open.</param>
public class FormClosingEventArgs(CloseReason closeReason, bool cancel) : CancelEventArgs(cancel)
{
    /// <summary>Why the form closes.</summary>
    public CloseReason CloseReason { get; } = closeReason;
}

/// <summary>A form closed.</summary>
/// <param name="closeReason">Why.</param>
public class FormClosedEventArgs(CloseReason closeReason) : EventArgs
{
    /// <summary>Why the form closed.</summary>
    public CloseReason CloseReason { get; } = closeReason;
}

/// <summary>Bulk insertion into Avalonia's observable items without replacing its collection.</summary>
public static class FormsItems
{
    /// <summary>Adds every value, preserving the order used by Windows Forms.</summary>
    public static void AddRange(Avalonia.Controls.ItemCollection items, System.Collections.IEnumerable values)
    {
        foreach (var value in values)
        {
            items.Add(value);
        }
    }
}
