using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels;

/// <summary>
/// A small in-app dialog that asks for one line of text, such as a file name, a choice from a short list, such as which
/// APK to install, or a confirmation. It is drawn inside the IDE rather than as a system dialog, so it looks the same on
/// every platform and works with touch.
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

    /// <summary>What the user chooses from, when the dialog asks for a choice rather than text.</summary>
    public ObservableCollection<string> Choices { get; } = [];

    /// <summary>Whether the dialog asks for a choice from <see cref="Choices"/>.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool HasChoices { get; private set; }

    /// <summary>The chosen item's index in <see cref="Choices"/>, or -1.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>Shows the dialog and waits for the answer.</summary>
    /// <returns>The text, or <see langword="null"/> when the user cancelled.</returns>
    public Task<string?> AskAsync(string title, string message, string confirmText, string initialText = "") =>
        Open(title, message, confirmText, initialText, hasInput: true, destructive: false);

    /// <summary>Shows the dialog without a text box and waits for the user to confirm or cancel.</summary>
    /// <returns><see langword="true"/> when the user confirmed.</returns>
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false) =>
        await Open(title, message, confirmText, string.Empty, hasInput: false, destructive) is not null;

    /// <summary>
    /// Shows the dialog with a list to choose from, rather than a number to type, and waits for the choice.
    /// </summary>
    /// <param name="title">The title, such as "Choose an APK".</param>
    /// <param name="message">What the choice is for.</param>
    /// <param name="confirmText">The confirm button's label, such as "Install".</param>
    /// <param name="choices">What to choose from, as the user reads them.</param>
    /// <param name="selected">The index chosen when the dialog opens.</param>
    /// <returns>The chosen index, or <see langword="null"/> when the user cancelled.</returns>
    public async Task<int?> ChooseAsync(string title, string message, string confirmText, IReadOnlyList<string> choices, int selected = 0)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var answer = await Open(title, message, confirmText, string.Empty, hasInput: false, destructive: false, choices, selected);
        return int.TryParse(answer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : null;
    }

    private Task<string?> Open(string title, string message, string confirmText, string initialText, bool hasInput, bool destructive, IReadOnlyList<string>? choices = null, int selected = -1)
    {
        _answer?.TrySetResult(null);
        _answer = new TaskCompletionSource<string?>();
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        Text = initialText;
        HasInput = hasInput;
        IsDestructive = destructive;
        Choices.Clear();
        foreach (var choice in choices ?? [])
        {
            Choices.Add(choice);
        }

        HasChoices = Choices.Count > 0;
        SelectedIndex = HasChoices ? Math.Clamp(selected, 0, Choices.Count - 1) : -1;
        ConfirmCommand.NotifyCanExecuteChanged();
        IsOpen = true;
        return _answer.Task;
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => Close(HasChoices ? SelectedIndex.ToString(CultureInfo.InvariantCulture) : HasInput ? Text.Trim() : "confirmed");

    [RelayCommand]
    private void Cancel() => Close(null);

    private bool CanConfirm() => HasChoices ? SelectedIndex >= 0 && SelectedIndex < Choices.Count : !HasInput || !string.IsNullOrWhiteSpace(Text);

    private void Close(string? answer)
    {
        IsOpen = false;
        var pending = _answer;
        _answer = null;
        pending?.TrySetResult(answer);
    }
}
