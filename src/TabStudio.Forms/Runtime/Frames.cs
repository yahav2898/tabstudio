using System.Drawing;
using System.Windows.Forms;
using TabStudio.Forms.Rendering;

namespace TabStudio.Forms.Runtime;

/// <summary>The frame a window is drawn in: its border, title bar and buttons.</summary>
/// <param name="Border">The border style.</param>
/// <param name="ControlBox">Whether the title bar has its buttons.</param>
/// <param name="MaximizeBox">Whether it has a maximize button.</param>
/// <param name="MinimizeBox">Whether it has a minimize button.</param>
internal sealed record WindowChrome(FormBorderStyle Border, bool ControlBox, bool MaximizeBox, bool MinimizeBox)
{
    /// <summary>Whether the window has a title bar and border.</summary>
    public bool HasFrame => Border != FormBorderStyle.None;

    /// <summary>Whether the user can resize the window.</summary>
    public bool IsSizable => Border is FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow;

    /// <summary>Whether it is a tool window, with a smaller title bar.</summary>
    public bool IsToolWindow => Border is FormBorderStyle.FixedToolWindow or FormBorderStyle.SizableToolWindow;
}

/// <summary>
/// What a window looks like now: everything the UI thread needs to show it. Frames are immutable, made on the Forms
/// thread after each batch of events and handed to the presenter.
/// </summary>
/// <param name="Id">The window's identifier.</param>
/// <param name="Title">The title.</param>
/// <param name="Frame">The visible window, title bar and border included, on the desktop.</param>
/// <param name="Client">The client area on the desktop, which <see cref="Content"/> fills.</param>
/// <param name="Chrome">The window's frame.</param>
/// <param name="IsMaximized">Whether the window fills the desktop.</param>
/// <param name="IsMinimized">Whether the window is minimized to a button at the bottom of the desktop.</param>
/// <param name="IsModal">Whether the window is a modal dialog, which blocks the windows below it.</param>
/// <param name="IsActive">Whether the window is the active one.</param>
/// <param name="IsBlocked">Whether a modal dialog above it blocks its input.</param>
/// <param name="Content">The client area's drawing.</param>
/// <param name="Popups">Open drop-downs and menus, drawn above everything.</param>
/// <param name="Editor">The text field being edited, shown as a native text box over its control.</param>
/// <param name="ToolTip">A tooltip to show, if any.</param>
internal sealed record WindowFrame(
    int Id,
    string Title,
    Rectangle Frame,
    Rectangle Client,
    WindowChrome Chrome,
    bool IsMaximized,
    bool IsMinimized,
    bool IsModal,
    bool IsActive,
    bool IsBlocked,
    DisplayList Content,
    IReadOnlyList<PopupFrame> Popups,
    EditorFrame? Editor,
    ToolTipFrame? ToolTip);

/// <summary>An open drop-down or menu, drawn above its window.</summary>
/// <param name="Bounds">Where it is, in the window's client coordinates; it may extend beyond the window.</param>
/// <param name="Content">Its drawing, in the window's client coordinates.</param>
internal sealed record PopupFrame(Rectangle Bounds, DisplayList Content);

/// <summary>A tooltip.</summary>
/// <param name="Text">The text.</param>
/// <param name="Bounds">Where it is, in the window's client coordinates.</param>
/// <param name="Content">Its drawing.</param>
internal sealed record ToolTipFrame(string Text, Rectangle Bounds, DisplayList Content);

/// <summary>
/// The text field with focus, which the UI shows as a native text box over its control so the device's keyboard,
/// selection handles and clipboard work. The model draws the field's text itself whenever it does not have focus.
/// </summary>
/// <param name="ControlId">The field's identifier.</param>
/// <param name="Bounds">Where the text goes, in the window's client coordinates.</param>
/// <param name="Text">The field's text.</param>
/// <param name="TextRevision">Changes when the app sets the text, which then replaces what the native box has.</param>
/// <param name="SelectionStart">The selection's start.</param>
/// <param name="SelectionLength">The selection's length.</param>
/// <param name="SelectionRevision">Changes when the app sets the selection.</param>
/// <param name="Font">The font.</param>
/// <param name="ForeColor">The text color.</param>
/// <param name="BackColor">The background color.</param>
/// <param name="Alignment">The text's horizontal alignment.</param>
/// <param name="Multiline">Whether the field has several lines.</param>
/// <param name="WordWrap">Whether lines wrap.</param>
/// <param name="ReadOnly">Whether the text cannot be changed.</param>
/// <param name="PasswordChar">The character shown for each character of a password, or <c>\0</c>.</param>
/// <param name="MaxLength">The most characters the field takes.</param>
/// <param name="ScrollBars">The scroll bars of a multiline field.</param>
/// <param name="AcceptsReturn">Whether Enter types a new line in a multiline field.</param>
/// <param name="AcceptsTab">Whether Tab types a tab.</param>
/// <param name="Casing">The case typed characters are changed to.</param>
internal sealed record EditorFrame(
    int ControlId,
    Rectangle Bounds,
    string Text,
    int TextRevision,
    int SelectionStart,
    int SelectionLength,
    int SelectionRevision,
    FontSpec Font,
    Color ForeColor,
    Color BackColor,
    HorizontalAlignment Alignment,
    bool Multiline,
    bool WordWrap,
    bool ReadOnly,
    char PasswordChar,
    int MaxLength,
    ScrollBars ScrollBars,
    bool AcceptsReturn,
    bool AcceptsTab,
    CharacterCasing Casing);

/// <summary>A file the app asks the user to pick, through the IDE's storage picker.</summary>
/// <param name="Save">Whether a file is saved rather than opened.</param>
/// <param name="Title">The dialog's title.</param>
/// <param name="Filter">The filter, as Windows Forms writes it: <c>Text files|*.txt|All files|*.*</c>.</param>
/// <param name="FilterIndex">The filter selected first, from 1.</param>
/// <param name="FileName">The suggested file name.</param>
/// <param name="Multiselect">Whether several files can be opened.</param>
/// <param name="Folder">Whether a folder is picked rather than files.</param>
/// <param name="InitialDirectory">The folder the dialog starts in, if any.</param>
internal sealed record FileRequest(bool Save, string Title, string Filter, int FilterIndex, string FileName, bool Multiselect, bool Folder = false, string InitialDirectory = "");

/// <summary>
/// Shows a running app's windows: the app preview implements it with Avalonia, tests with a recorder. The Forms
/// thread calls it; implementations hand work to their own thread.
/// </summary>
internal interface IFormsPresenter
{
    /// <summary>The size of the desktop the windows are shown on, read on the Forms thread.</summary>
    Size DesktopSize { get; }

    /// <summary>Shows windows as they look now, and removes closed ones.</summary>
    /// <param name="frames">The windows that changed, in z-order from the bottom.</param>
    /// <param name="closed">The windows that closed.</param>
    void Present(IReadOnlyList<WindowFrame> frames, IReadOnlyList<int> closed);

    /// <summary>Asks the user for files to open or a file to save.</summary>
    /// <param name="request">What to ask.</param>
    /// <returns>The chosen local paths, or <see langword="null"/> when the user cancelled.</returns>
    Task<string[]?> PickFilesAsync(FileRequest request);

    /// <summary>Copies text to the clipboard.</summary>
    /// <param name="text">The text.</param>
    Task SetClipboardTextAsync(string text);

    /// <summary>Reads text from the clipboard.</summary>
    Task<string?> GetClipboardTextAsync();
}
