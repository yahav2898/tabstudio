using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels;

/// <summary>
/// A small in-app dialog that asks for one line of text, such as a file name. It is drawn inside the IDE
/// rather than as a system dialog, so it looks the same on every platform and works with touch.
/// </summary>
public sealed partial class PromptViewModel : ObservableObject
{
    private TaskCompletionSource<string?>? _answer;

    /// <summary>Whether the dialog is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>The dialog title, such as "New File".</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    /// <summary>A sentence under the title, such as where the file is created.</summary>
    [ObservableProperty]
    public partial string Message { get; private set; } = string.Empty;

    /// <summary>The confirm button label, such as "Create".</summary>
    [ObservableProperty]
    public partial string ConfirmText { get; private set; } = "OK";

    /// <summary>Whether the dialog asks for text; otherwise it only asks for confirmation.</summary>
    [ObservableProperty]
    public partial bool HasInput { get; private set; } = true;

    /// <summary>Whether confirming does something destructive, which colors the button red.</summary>
    [ObservableProperty]
    public partial bool IsDestructive { get; private set; }

    /// <summary>The text the user types.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>Shows the dialog and waits for the answer.</summary>
    /// <returns>The text, or <see langword="null"/> when the user cancelled.</returns>
    public Task<string?> AskAsync(string title, string message, string confirmText, string initialText = "") =>
        Open(title, message, confirmText, initialText, hasInput: true, destructive: false);

    /// <summary>Shows the dialog without a text box and waits for the user to confirm or cancel.</summary>
    /// <returns><see langword="true"/> when the user confirmed.</returns>
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false) =>
        await Open(title, message, confirmText, "confirmed", hasInput: false, destructive) is not null;

    private Task<string?> Open(string title, string message, string confirmText, string initialText, bool hasInput, bool destructive)
    {
        _answer?.TrySetResult(null);
        _answer = new TaskCompletionSource<string?>();
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        Text = initialText;
        HasInput = hasInput;
        IsDestructive = destructive;
        IsOpen = true;
        return _answer.Task;
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => Close(Text.Trim());

    [RelayCommand]
    private void Cancel() => Close(null);

    private bool CanConfirm() => !string.IsNullOrWhiteSpace(Text);

    private void Close(string? answer)
    {
        IsOpen = false;
        var pending = _answer;
        _answer = null;
        pending?.TrySetResult(answer);
    }
}
