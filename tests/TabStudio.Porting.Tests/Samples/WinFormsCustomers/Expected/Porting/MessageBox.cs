namespace CustomerEntry.Porting;

/// <summary>The buttons of a message box, as in Windows Forms.</summary>
public enum MessageBoxButtons
{
    /// <summary>OK.</summary>
    OK,

    /// <summary>OK and Cancel.</summary>
    OKCancel,

    /// <summary>Abort, Retry and Ignore.</summary>
    AbortRetryIgnore,

    /// <summary>Yes, No and Cancel.</summary>
    YesNoCancel,

    /// <summary>Yes and No.</summary>
    YesNo,

    /// <summary>Retry and Cancel.</summary>
    RetryCancel,
}

/// <summary>The icon of a message box, as in Windows Forms.</summary>
public enum MessageBoxIcon
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

/// <summary>
/// Windows Forms' message box, ported: a dialog of the app, on the desktop and on phones and tablets. It does not
/// block, so <c>MessageBox.Show</c> became <c>await MessageBox.ShowAsync</c>.
/// </summary>
public static class MessageBox
{
    /// <summary>Shows a message and waits for the answer.</summary>
    /// <param name="text">The message.</param>
    /// <param name="caption">The title.</param>
    /// <param name="buttons">The buttons.</param>
    /// <param name="icon">The icon.</param>
    public static Task<DialogResult> ShowAsync(string text, string caption = "", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None) =>
        ShowAsync(null, text, caption, buttons, icon);

    /// <summary>Shows a message over a form and waits for the answer.</summary>
    /// <param name="owner">The form.</param>
    /// <param name="text">The message.</param>
    /// <param name="caption">The title.</param>
    /// <param name="buttons">The buttons.</param>
    /// <param name="icon">The icon.</param>
    public static async Task<DialogResult> ShowAsync(WindowView? owner, string text, string caption = "", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
    {
        DialogResult[] answers = buttons switch
        {
            MessageBoxButtons.OKCancel => [DialogResult.OK, DialogResult.Cancel],
            MessageBoxButtons.AbortRetryIgnore => [DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore],
            MessageBoxButtons.YesNoCancel => [DialogResult.Yes, DialogResult.No, DialogResult.Cancel],
            MessageBoxButtons.YesNo => [DialogResult.Yes, DialogResult.No],
            MessageBoxButtons.RetryCancel => [DialogResult.Retry, DialogResult.Cancel],
            _ => [DialogResult.OK],
        };

        var cancel = Array.IndexOf(answers, DialogResult.Cancel) >= 0 ? DialogResult.Cancel
            : buttons == MessageBoxButtons.OK ? DialogResult.OK
            : buttons == MessageBoxButtons.AbortRetryIgnore ? DialogResult.Abort
            : DialogResult.No;
        var list = answers
            .Select((a, i) => new MessageButton(a.ToString(), (int)a, IsDefault: i == 0, IsCancel: a == cancel))
            .ToList();
        var shown = icon switch
        {
            MessageBoxIcon.Error => MessageIcon.Error,
            MessageBoxIcon.Question => MessageIcon.Question,
            MessageBoxIcon.Warning => MessageIcon.Warning,
            MessageBoxIcon.Information => MessageIcon.Information,
            _ => MessageIcon.None,
        };

        return (DialogResult)await MessageDialog.ShowAsync(owner, text, caption, list, shown, (int)cancel);
    }
}
