namespace System.Windows.Forms;

/// <summary>The buttons of a message box.</summary>
public enum MessageBoxButtons
{
    /// <summary>O k.</summary>
    OK = 0,

    /// <summary>O k cancel.</summary>
    OKCancel = 1,

    /// <summary>Abort retry ignore.</summary>
    AbortRetryIgnore = 2,

    /// <summary>Yes no cancel.</summary>
    YesNoCancel = 3,

    /// <summary>Yes no.</summary>
    YesNo = 4,

    /// <summary>Retry cancel.</summary>
    RetryCancel = 5,

    /// <summary>Cancel try continue.</summary>
    CancelTryContinue = 6,
}

/// <summary>The icon of a message box.</summary>
public enum MessageBoxIcon
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Hand.</summary>
    Hand = 16,

    /// <summary>Stop.</summary>
    Stop = 16,

    /// <summary>Error.</summary>
    Error = 16,

    /// <summary>Question.</summary>
    Question = 32,

    /// <summary>Exclamation.</summary>
    Exclamation = 48,

    /// <summary>Warning.</summary>
    Warning = 48,

    /// <summary>Asterisk.</summary>
    Asterisk = 64,

    /// <summary>Information.</summary>
    Information = 64,
}

/// <summary>The button of a message box that Enter presses.</summary>
public enum MessageBoxDefaultButton
{
    /// <summary>Button1.</summary>
    Button1 = 0,

    /// <summary>Button2.</summary>
    Button2 = 256,

    /// <summary>Button3.</summary>
    Button3 = 512,

    /// <summary>Button4.</summary>
    Button4 = 768,
}

/// <summary>Options of a message box. Kept for compatibility.</summary>
[Flags]
public enum MessageBoxOptions
{
    /// <summary>Service notification.</summary>
    ServiceNotification = 2097152,

    /// <summary>Default desktop only.</summary>
    DefaultDesktopOnly = 131072,

    /// <summary>Right align.</summary>
    RightAlign = 524288,

    /// <summary>Rtl reading.</summary>
    RtlReading = 1048576,
}
