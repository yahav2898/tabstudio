namespace System.Windows.Forms;

/// <summary>The border and title bar of a form.</summary>
public enum FormBorderStyle
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Fixed single.</summary>
    FixedSingle = 1,

    /// <summary>Fixed3 d.</summary>
    Fixed3D = 2,

    /// <summary>Fixed dialog.</summary>
    FixedDialog = 3,

    /// <summary>Sizable.</summary>
    Sizable = 4,

    /// <summary>Fixed tool window.</summary>
    FixedToolWindow = 5,

    /// <summary>Sizable tool window.</summary>
    SizableToolWindow = 6,
}

/// <summary>Where a form appears when it is first shown.</summary>
public enum FormStartPosition
{
    /// <summary>Manual.</summary>
    Manual = 0,

    /// <summary>Center screen.</summary>
    CenterScreen = 1,

    /// <summary>Windows default location.</summary>
    WindowsDefaultLocation = 2,

    /// <summary>Windows default bounds.</summary>
    WindowsDefaultBounds = 3,

    /// <summary>Center parent.</summary>
    CenterParent = 4,
}

/// <summary>Whether a form is normal, minimized or maximized.</summary>
public enum FormWindowState
{
    /// <summary>Normal.</summary>
    Normal = 0,

    /// <summary>Minimized.</summary>
    Minimized = 1,

    /// <summary>Maximized.</summary>
    Maximized = 2,
}

/// <summary>Why a form is closing.</summary>
public enum CloseReason
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Windows shut down.</summary>
    WindowsShutDown = 1,

    /// <summary>Mdi form closing.</summary>
    MdiFormClosing = 2,

    /// <summary>User closing.</summary>
    UserClosing = 3,

    /// <summary>Task manager closing.</summary>
    TaskManagerClosing = 4,

    /// <summary>Form owner closing.</summary>
    FormOwnerClosing = 5,

    /// <summary>Application exit call.</summary>
    ApplicationExitCall = 6,
}

/// <summary>The result of a dialog box: the button that closed it.</summary>
public enum DialogResult
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>O k.</summary>
    OK = 1,

    /// <summary>Cancel.</summary>
    Cancel = 2,

    /// <summary>Abort.</summary>
    Abort = 3,

    /// <summary>Retry.</summary>
    Retry = 4,

    /// <summary>Ignore.</summary>
    Ignore = 5,

    /// <summary>Yes.</summary>
    Yes = 6,

    /// <summary>No.</summary>
    No = 7,

    /// <summary>Try again.</summary>
    TryAgain = 10,

    /// <summary>Continue.</summary>
    Continue = 11,
}

/// <summary>How an application handles high resolutions. Kept for compatibility: TabStudio scales the preview itself.</summary>
public enum HighDpiMode
{
    /// <summary>Dpi unaware.</summary>
    DpiUnaware = 0,

    /// <summary>System aware.</summary>
    SystemAware = 1,

    /// <summary>Per monitor.</summary>
    PerMonitor = 2,

    /// <summary>Per monitor v2.</summary>
    PerMonitorV2 = 3,

    /// <summary>Dpi unaware gdi scaled.</summary>
    DpiUnawareGdiScaled = 4,
}

/// <summary>How a container validates its controls when focus moves.</summary>
public enum AutoValidate
{
    /// <summary>Inherit.</summary>
    Inherit = -1,

    /// <summary>Disable.</summary>
    Disable = 0,

    /// <summary>Enable prevent focus change.</summary>
    EnablePreventFocusChange = 1,

    /// <summary>Enable allow focus change.</summary>
    EnableAllowFocusChange = 2,
}

/// <summary>Whether a form shows a size grip.</summary>
public enum SizeGripStyle
{
    /// <summary>Auto.</summary>
    Auto = 0,

    /// <summary>Show.</summary>
    Show = 1,

    /// <summary>Hide.</summary>
    Hide = 2,
}
