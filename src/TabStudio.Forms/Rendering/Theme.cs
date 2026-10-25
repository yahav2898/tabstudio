using System.Drawing;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// The colors of the Windows 10 and 11 visual styles that Windows Forms controls are drawn with, so an app looks
/// in the app preview as it does on a PC.
/// </summary>
internal static class Theme
{
    /// <summary>The face of a button.</summary>
    public static Color ButtonFace { get; } = Color.FromArgb(225, 225, 225);

    /// <summary>The border of a button.</summary>
    public static Color ButtonBorder { get; } = Color.FromArgb(173, 173, 173);

    /// <summary>The face of a button under the pointer.</summary>
    public static Color ButtonHotFace { get; } = Color.FromArgb(229, 241, 251);

    /// <summary>The accent: borders of hot, focused and default buttons, and selections.</summary>
    public static Color Accent { get; } = Color.FromArgb(0, 120, 215);

    /// <summary>The face of a pressed button.</summary>
    public static Color ButtonPressedFace { get; } = Color.FromArgb(204, 228, 247);

    /// <summary>The border of a pressed button.</summary>
    public static Color ButtonPressedBorder { get; } = Color.FromArgb(0, 84, 153);

    /// <summary>The face of a disabled button.</summary>
    public static Color ButtonDisabledFace { get; } = Color.FromArgb(204, 204, 204);

    /// <summary>The border of a disabled button.</summary>
    public static Color ButtonDisabledBorder { get; } = Color.FromArgb(191, 191, 191);

    /// <summary>The text of disabled controls.</summary>
    public static Color DisabledText { get; } = Color.FromArgb(131, 131, 131);

    /// <summary>The border of text boxes and lists.</summary>
    public static Color EditBorder { get; } = Color.FromArgb(122, 122, 122);

    /// <summary>The border of a text box under the pointer.</summary>
    public static Color EditHotBorder { get; } = Color.FromArgb(23, 23, 23);

    /// <summary>The border of a disabled text box.</summary>
    public static Color EditDisabledBorder { get; } = Color.FromArgb(204, 204, 204);

    /// <summary>The border of check boxes and radio buttons.</summary>
    public static Color GlyphBorder { get; } = Color.FromArgb(51, 51, 51);

    /// <summary>The check mark and radio dot.</summary>
    public static Color Glyph { get; } = Color.FromArgb(51, 51, 51);

    /// <summary>The line of a group box.</summary>
    public static Color GroupBorder { get; } = Color.FromArgb(220, 220, 220);

    /// <summary>The track of a progress bar.</summary>
    public static Color ProgressTrack { get; } = Color.FromArgb(230, 230, 230);

    /// <summary>The border of a progress bar.</summary>
    public static Color ProgressBorder { get; } = Color.FromArgb(188, 188, 188);

    /// <summary>The fill of a progress bar.</summary>
    public static Color ProgressFill { get; } = Color.FromArgb(6, 176, 37);

    /// <summary>The track of a track bar.</summary>
    public static Color TrackBarTrack { get; } = Color.FromArgb(231, 234, 234);

    /// <summary>The border of a track bar's track.</summary>
    public static Color TrackBarTrackBorder { get; } = Color.FromArgb(214, 214, 214);

    /// <summary>The tick marks of a track bar.</summary>
    public static Color TrackBarTick { get; } = Color.FromArgb(196, 196, 196);

    /// <summary>The background of a scroll bar.</summary>
    public static Color ScrollTrack { get; } = Color.FromArgb(240, 240, 240);

    /// <summary>The thumb of a scroll bar.</summary>
    public static Color ScrollThumb { get; } = Color.FromArgb(205, 205, 205);

    /// <summary>The thumb of a scroll bar being dragged.</summary>
    public static Color ScrollThumbPressed { get; } = Color.FromArgb(96, 96, 96);

    /// <summary>The arrows of a scroll bar.</summary>
    public static Color ScrollArrow { get; } = Color.FromArgb(96, 96, 96);

    /// <summary>The width of a vertical scroll bar and the height of a horizontal one.</summary>
    public const int ScrollBarSize = 17;

    /// <summary>The background of menus and tool strips.</summary>
    public static Color StripBackground { get; } = Color.FromArgb(240, 240, 240);

    /// <summary>The background of drop-down menus.</summary>
    public static Color MenuBackground { get; } = Color.FromArgb(249, 249, 249);

    /// <summary>The border of drop-down menus and lists.</summary>
    public static Color MenuBorder { get; } = Color.FromArgb(204, 204, 204);

    /// <summary>The highlight of a menu item or tool button under the pointer.</summary>
    public static Color ItemHot { get; } = Color.FromArgb(204, 232, 255);

    /// <summary>The border of a menu item or tool button under the pointer.</summary>
    public static Color ItemHotBorder { get; } = Color.FromArgb(153, 209, 255);

    /// <summary>The separators of menus and tool strips.</summary>
    public static Color Separator { get; } = Color.FromArgb(215, 215, 215);

    /// <summary>The border of tabs.</summary>
    public static Color TabBorder { get; } = Color.FromArgb(217, 217, 217);

    /// <summary>The face of a tab under the pointer.</summary>
    public static Color TabHot { get; } = Color.FromArgb(216, 234, 249);

    /// <summary>The background of a data grid view outside its cells.</summary>
    public static Color GridBackground { get; } = Color.FromArgb(171, 171, 171);

    /// <summary>The lines between the cells of a data grid view.</summary>
    public static Color GridLines { get; } = Color.FromArgb(160, 160, 160);

    /// <summary>The background of column and row headers.</summary>
    public static Color HeaderBackground { get; } = Color.FromArgb(255, 255, 255);

    /// <summary>The separators between column headers.</summary>
    public static Color HeaderSeparator { get; } = Color.FromArgb(229, 229, 229);

    /// <summary>The selection of items and cells.</summary>
    public static Color Selection { get; } = Color.FromArgb(0, 120, 215);

    /// <summary>The text of selected items and cells.</summary>
    public static Color SelectionText { get; } = Color.White;

    /// <summary>The background of text boxes, lists and grids.</summary>
    public static Color Window { get; } = Color.White;

    /// <summary>The background of tooltips.</summary>
    public static Color ToolTipBackground { get; } = Color.FromArgb(255, 255, 255);

    /// <summary>The border of tooltips.</summary>
    public static Color ToolTipBorder { get; } = Color.FromArgb(118, 118, 118);
}
