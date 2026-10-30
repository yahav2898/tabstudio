using System.ComponentModel;
using System.Windows.Forms;

namespace Microsoft.VisualBasic.CompilerServices;

/// <summary>A host of Visual Basic code, which gives <c>MsgBox</c> and <c>InputBox</c> their owner window and default title.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IVbHost
{
    /// <summary>The window the dialogs are shown over.</summary>
    IWin32Window GetParentWindow();

    /// <summary>The dialogs' title when the code gives none.</summary>
    string GetWindowTitle();
}

/// <summary>The host of Visual Basic code, if one is set, for <c>MsgBox</c> and <c>InputBox</c>.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class HostServices
{
    /// <summary>Creates the object; its member is shared.</summary>
    public HostServices()
    {
    }

    /// <summary>The host, or <see langword="null"/>.</summary>
    public static IVbHost? VBHost { get; set; }
}
