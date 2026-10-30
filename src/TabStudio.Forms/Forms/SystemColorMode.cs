namespace System.Windows.Forms;

/// <summary>
/// The color mode an app asks for: classic, the system's, or dark. TabStudio draws forms in Windows 10's classic
/// colors whatever it is.
/// </summary>
public enum SystemColorMode
{
    /// <summary>Classic, light colors.</summary>
    Classic = 0,

    /// <summary>The colors the system uses.</summary>
    System = 1,

    /// <summary>Dark colors.</summary>
    Dark = 2,
}
