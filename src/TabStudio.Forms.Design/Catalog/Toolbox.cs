using System.Windows.Forms;

namespace TabStudio.Forms.Design.Catalog;

/// <summary>A tool of the Toolbox: a control or component type the designer can add.</summary>
/// <param name="Type">The type.</param>
/// <param name="Category">Its Toolbox tab, such as <c>Common Controls</c>.</param>
/// <param name="Icon">Its icon, as stroke path data on a 24-pixel grid.</param>
internal sealed record ToolboxItem(Type Type, string Category, string Icon)
{
    /// <summary>The name the Toolbox shows, such as <c>Button</c>.</summary>
    public string Name => Type.Name;
}

/// <summary>
/// The Toolbox's tools, in Visual Studio's tabs and order: common controls, containers, menus and toolbars, data,
/// components and dialogs, as far as TabStudio's Forms layer has them.
/// </summary>
internal static class Toolbox
{
    /// <summary>The tabs, in order.</summary>
    public static IReadOnlyList<string> Categories { get; } = ["Common Controls", "Containers", "Menus & Toolbars", "Data", "Components", "Dialogs"];

    /// <summary>The tools, by tab.</summary>
    public static IReadOnlyList<ToolboxItem> Items { get; } =
    [
        new(typeof(Button), "Common Controls", "M3 7 Q3 6 4 6 L20 6 Q21 6 21 7 L21 17 Q21 18 20 18 L4 18 Q3 18 3 17 Z M8 12 L16 12"),
        new(typeof(CheckBox), "Common Controls", "M4 4 L20 4 L20 20 L4 20 Z M8 12 L11 15 L16 9"),
        new(typeof(CheckedListBox), "Common Controls", "M3 3 L21 3 L21 21 L3 21 Z M6 7 L9 7 L9 10 L6 10 Z M12 8.5 L18 8.5 M6 14 L9 14 L9 17 L6 17 Z M12 15.5 L18 15.5"),
        new(typeof(ComboBox), "Common Controls", "M2 7 L22 7 L22 17 L2 17 Z M16 7 L16 17 M17.5 11 L19 12.5 L20.5 11 M5 12 L12 12"),
        new(typeof(DateTimePicker), "Common Controls", "M3 5 L21 5 L21 20 L3 20 Z M3 9 L21 9 M8 3 L8 7 M16 3 L16 7 M7 13 L9 13 M11 13 L13 13 M15 13 L17 13 M7 16.5 L9 16.5 M11 16.5 L13 16.5"),
        new(typeof(Label), "Common Controls", "M5 19 L12 4 L19 19 M7.8 13 L16.2 13"),
        new(typeof(LinkLabel), "Common Controls", "M5 16 L10 4 L15 16 M7 12 L13 12 M4 20 L20 20"),
        new(typeof(ListBox), "Common Controls", "M3 3 L21 3 L21 21 L3 21 Z M6 7 L18 7 M6 11 L18 11 M6 15 L14 15"),
        new(typeof(MonthCalendar), "Common Controls", "M3 4 L21 4 L21 20 L3 20 Z M3 8 L21 8 M7 11 L8 11 M11.5 11 L12.5 11 M16 11 L17 11 M7 14 L8 14 M11.5 14 L12.5 14 M16 14 L17 14 M7 17 L8 17 M11.5 17 L12.5 17"),
        new(typeof(NumericUpDown), "Common Controls", "M2 7 L22 7 L22 17 L2 17 Z M16 7 L16 17 M17.5 10.5 L19 9 L20.5 10.5 M17.5 13.5 L19 15 L20.5 13.5 M5 12 L10 12"),
        new(typeof(PictureBox), "Common Controls", "M3 4 L21 4 L21 20 L3 20 Z M3 17 L9 11 L13 15 L16 12 L21 17 M15.5 8.5 A1.5 1.5 0 1 0 18.5 8.5 A1.5 1.5 0 1 0 15.5 8.5"),
        new(typeof(ProgressBar), "Common Controls", "M2 9 L22 9 L22 15 L2 15 Z M4 11 L14 11 L14 13 L4 13 Z"),
        new(typeof(RadioButton), "Common Controls", "M4 12 A8 8 0 1 0 20 12 A8 8 0 1 0 4 12 M10 12 A2 2 0 1 0 14 12 A2 2 0 1 0 10 12"),
        new(typeof(RichTextBox), "Common Controls", "M3 3 L21 3 L21 21 L3 21 Z M6 7 L18 7 M6 11 L18 11 M6 15 L12 15 M15 13 L15 18"),
        new(typeof(TextBox), "Common Controls", "M2 7 L22 7 L22 17 L2 17 Z M6 9.5 L6 14.5 M5 9.5 L7 9.5 M5 14.5 L7 14.5"),
        new(typeof(TrackBar), "Common Controls", "M3 12 L21 12 M10 7 L14 7 L14 15 L12 17 L10 15 Z"),
        new(typeof(FlowLayoutPanel), "Containers", "M3 4 L21 4 L21 20 L3 20 Z M6 8 L10 8 L10 11 L6 11 Z M12 8 L18 8 L18 11 L12 11 Z M6 14 L13 14 L13 17 L6 17 Z"),
        new(typeof(GroupBox), "Containers", "M8 5 L3 5 L3 20 L21 20 L21 5 L16 5 M9 5 L15 5"),
        new(typeof(Panel), "Containers", "M3 4 L21 4 L21 20 L3 20 Z"),
        new(typeof(SplitContainer), "Containers", "M3 4 L21 4 L21 20 L3 20 Z M11 4 L11 20 M13 4 L13 20"),
        new(typeof(TabControl), "Containers", "M3 9 L21 9 L21 20 L3 20 Z M3 9 L3 5 L10 5 L10 9 M10 6 L16 6 L16 9"),
        new(typeof(TableLayoutPanel), "Containers", "M3 4 L21 4 L21 20 L3 20 Z M3 12 L21 12 M12 4 L12 20"),
        new(typeof(ContextMenuStrip), "Menus & Toolbars", "M5 3 L19 3 L19 21 L5 21 Z M8 7 L16 7 M8 11 L16 11 M5 14 L19 14 M8 17.5 L14 17.5"),
        new(typeof(MenuStrip), "Menus & Toolbars", "M2 4 L22 4 L22 9 L2 9 Z M4.5 6.5 L7.5 6.5 M10 6.5 L13 6.5 M9 9 L9 20 L18 20 L18 9 M11 13 L16 13 M11 16.5 L16 16.5"),
        new(typeof(StatusStrip), "Menus & Toolbars", "M2 15 L22 15 L22 20 L2 20 Z M4.5 17.5 L10 17.5 M2 4 L22 4 L22 15"),
        new(typeof(ToolStrip), "Menus & Toolbars", "M2 7 L22 7 L22 17 L2 17 Z M5 10 L9 10 L9 14 L5 14 Z M11 10 L15 10 L15 14 L11 14 Z M18 9.5 L18 14.5"),
        new(typeof(BindingSource), "Data", "M4 6 A8 3 0 1 0 20 6 A8 3 0 1 0 4 6 M4 6 L4 18 A8 3 0 0 0 20 18 L20 6 M4 12 A8 3 0 0 0 20 12"),
        new(typeof(DataGridView), "Data", "M3 4 L21 4 L21 20 L3 20 Z M3 8 L21 8 M3 12 L21 12 M3 16 L21 16 M9 4 L9 20 M15 4 L15 20"),
        new(typeof(ImageList), "Components", "M5 7 L19 7 L19 19 L5 19 Z M8 4 L22 4 L22 16 M5 16 L9 12 L12 15 L14 13 L19 18"),
        new(typeof(System.Windows.Forms.Timer), "Components", "M4 13 A8 8 0 1 0 20 13 A8 8 0 1 0 4 13 M12 13 L12 9 M12 13 L15 15 M10 3 L14 3"),
        new(typeof(ToolTip), "Components", "M3 4 L21 4 L21 15 L11 15 L7 19 L7 15 L3 15 Z M7 8 L17 8 M7 11 L13 11"),
        new(typeof(ColorDialog), "Dialogs", "M12 3 A9 9 0 1 0 12 21 Q14 21 14 19 Q14 17 16 17 L18 17 Q21 17 21 13 A9 9 0 0 0 12 3 M7.5 11 L8.5 11 M10.5 7.5 L11.5 7.5 M15 8.5 L16 8.5"),
        new(typeof(FolderBrowserDialog), "Dialogs", "M3 6 L3 19 L21 19 L21 8 L11 8 L9 6 Z"),
        new(typeof(FontDialog), "Dialogs", "M4 19 L10 5 L16 19 M6.5 14 L13.5 14 M16 11 Q20 11 20 14 L20 19 M20 16 Q17 15 17 17.5 Q17 19.5 20 19"),
        new(typeof(OpenFileDialog), "Dialogs", "M6 3 L14 3 L19 8 L19 21 L6 21 Z M14 3 L14 8 L19 8 M12.5 17 L12.5 11 M10 13.5 L12.5 11 L15 13.5"),
        new(typeof(SaveFileDialog), "Dialogs", "M5 4 L17 4 L20 7 L20 20 L5 20 Z M8 4 L8 9 L16 9 L16 4 M8 20 L8 14 L17 14 L17 20"),
    ];
}
