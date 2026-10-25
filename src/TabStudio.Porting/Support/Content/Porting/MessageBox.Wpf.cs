namespace $rootnamespace$.Porting;

/// <summary>The buttons of a message box, as in WPF.</summary>
public enum MessageBoxButton
{
    /// <summary>OK.</summary>
    OK = 0,

    /// <summary>OK and Cancel.</summary>
    OKCancel = 1,

    /// <summary>Yes, No and Cancel.</summary>
    YesNoCancel = 3,

    /// <summary>Yes and No.</summary>
    YesNo = 4,
}

/// <summary>The icon of a message box, as in WPF.</summary>
public enum MessageBoxImage
{
    /// <summary>No icon.</summary>
    None = 0,

    /// <summary>An error.</summary>
    Error = 16,

    /// <summary>An error.</summary>
    Hand = 16,

    /// <summary>An error.</summary>
    Stop = 16,

    /// <summary>A question.</summary>
    Question = 32,

    /// <summary>A warning.</summary>
    Warning = 48,

    /// <summary>A warning.</summary>
    Exclamation = 48,

    /// <summary>Information.</summary>
    Information = 64,

    /// <summary>Information.</summary>
    Asterisk = 64,
}

/// <summary>The answer of a message box, as in WPF.</summary>
public enum MessageBoxResult
{
    /// <summary>No answer.</summary>
    None = 0,

    /// <summary>OK.</summary>
    OK = 1,

    /// <summary>Cancel.</summary>
    Cancel = 2,

    /// <summary>Yes.</summary>
    Yes = 6,

    /// <summary>No.</summary>
    No = 7,
}

/// <summary>
/// WPF's message box, ported: a dialog of the app, on the desktop and on phones and tablets. It does not block,
/// so <c>MessageBox.Show</c> became <c>await MessageBox.ShowAsync</c>.
/// </summary>
public static class MessageBox
{
    /// <summary>Shows a message and waits for the answer.</summary>
    /// <param name="messageBoxText">The message.</param>
    /// <param name="caption">The title.</param>
    /// <param name="button">The buttons.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="defaultResult">The button Enter presses.</param>
    public static Task<MessageBoxResult> ShowAsync(string messageBoxText, string caption = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None) =>
        ShowAsync(null, messageBoxText, caption, button, icon, defaultResult);

    /// <summary>Shows a message over a window and waits for the answer.</summary>
    /// <param name="owner">The window.</param>
    /// <param name="messageBoxText">The message.</param>
    /// <param name="caption">The title.</param>
    /// <param name="button">The buttons.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="defaultResult">The button Enter presses.</param>
    public static async Task<MessageBoxResult> ShowAsync(WindowView? owner, string messageBoxText, string caption = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        MessageBoxResult[] answers = button switch
        {
            MessageBoxButton.OKCancel => [MessageBoxResult.OK, MessageBoxResult.Cancel],
            MessageBoxButton.YesNo => [MessageBoxResult.Yes, MessageBoxResult.No],
            MessageBoxButton.YesNoCancel => [MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel],
            _ => [MessageBoxResult.OK],
        };

        var first = defaultResult == MessageBoxResult.None ? answers[0] : defaultResult;
        var cancel = Array.IndexOf(answers, MessageBoxResult.Cancel) >= 0 ? MessageBoxResult.Cancel
            : button == MessageBoxButton.OK ? MessageBoxResult.OK
            : MessageBoxResult.No;
        var buttons = answers
            .Select(a => new MessageButton(a.ToString(), (int)a, IsDefault: a == first, IsCancel: a == cancel))
            .ToList();
        var shown = icon switch
        {
            MessageBoxImage.Error => MessageIcon.Error,
            MessageBoxImage.Question => MessageIcon.Question,
            MessageBoxImage.Warning => MessageIcon.Warning,
            MessageBoxImage.Information => MessageIcon.Information,
            _ => MessageIcon.None,
        };

        return (MessageBoxResult)await MessageDialog.ShowAsync(owner, messageBoxText, caption, buttons, shown, (int)cancel);
    }
}
