# TabStudio.Forms

TabStudio's Windows Forms layer: the `System.Windows.Forms` and `System.Drawing` API that Windows Forms apps are
written against, implemented on Avalonia so the apps run on an Android tablet. A project with
`<UseWindowsForms>true</UseWindowsForms>` (any `TargetFramework`, such as `net10.0-windows`) is compiled against this
assembly instead of the Windows Desktop SDK, runs in the app preview inside a Windows 10 window frame, and its forms
are designed in the Forms designer (`TabStudio.Forms.Design`).

Names, signatures, defaults and event order follow Windows Forms on .NET, so code written in Visual Studio compiles
unchanged. A member that cannot work on a tablet throws `NotSupportedException` with a message that names the member
and says what to do instead, for example `Graphics.GetHdc is not supported by TabStudio's Forms layer: there is no GDI
device context on this device.`

## How an app runs

- `Application.Run(form)` runs the app's message loop on its own UI thread, as on Windows. `Show` and `ShowDialog`
  open more windows on the same desktop; modal dialogs block the windows below them.
- Closing the main form ends the run. An exception the app does not handle ends the app with the exception in the
  Output panel, never TabStudio. `Console` output goes to the Output panel.
- `ApplicationConfiguration.Initialize()` is generated from the project's `ApplicationHighDpiMode`,
  `ApplicationVisualStyles`, `ApplicationUseCompatibleTextRendering` and `ApplicationDefaultFont` properties, as the
  Windows Desktop SDK does; with `ImplicitUsings`, `System.Drawing` and `System.Windows.Forms` are global usings.
- Touch: a tap is a click, a drag scrolls lists, grids and scrollable panels, a long press is a right-click (context
  menus), and text fields edit with the device's keyboard. A hardware keyboard and mouse work as on Windows,
  mnemonics, Tab order, `AcceptButton`/`CancelButton` and `KeyPreview` included.
- Colors are Windows 10's: `SystemColors.Control` is `#F0F0F0` and the window frame is drawn as on Windows 10.

## Supported

**Application and forms**: `Application` (`Run`, `Exit`, `DoEvents`, `OpenForms`, `ThreadException`,
`SetUnhandledExceptionMode`, `EnableVisualStyles`, `SetHighDpiMode`, `SetDefaultFont`), `ApplicationContext`, `Form`
(`Show`, `ShowDialog`, `Close`, `DialogResult`, `AcceptButton`, `CancelButton`, `KeyPreview`, `StartPosition`,
`WindowState`, `FormBorderStyle`, `ControlBox`, `MinimizeBox`, `MaximizeBox`, `Owner`, `MainMenuStrip`,
`AutoScaleMode`, the `Load`, `Shown`, `Activated`, `FormClosing` and `FormClosed` events), MDI (`IsMdiContainer`,
`MdiParent`, `MdiChildren`, `ActiveMdiChild`, `LayoutMdi`, `MdiChildActivate`, the menu's `MdiWindowListItem`: children
are windows inside the parent's workspace, each with a title bar to move, minimize, maximize and close it), forms
with `TopLevel = false` inside panels, `Application.Restart` (TabStudio runs the app again),
`Application.AddMessageFilter` (mouse and keyboard messages, `WM_LBUTTONDOWN`, `WM_KEYDOWN` and the rest), `UserControl`,
`ContainerControl`, `ScrollableControl` (`AutoScroll`), `Control` (layout, painting, input, focus, validation,
`Invoke`/`BeginInvoke`, `CreateGraphics`), `WindowsFormsSynchronizationContext` (so `async` handlers and
`BackgroundWorker` come back to the UI thread), `Screen`, `SystemInformation`, `Cursor`/`Cursors`.

**Controls**: `Button`, `CheckBox`, `RadioButton`, `Label`, `LinkLabel`, `TextBox` (multiline, password, read-only,
`AutoComplete`), `RichTextBox` (one font and color; `Rtf`, `SelectedRtf`, `LoadFile` and `SaveFile` read and write RTF documents' text), `ComboBox`, `ListBox`, `CheckedListBox`, `NumericUpDown`, `DomainUpDown`,
`DateTimePicker`, `MonthCalendar`, `TrackBar`, `ProgressBar`, `PictureBox`, `GroupBox`, `Panel`, `FlowLayoutPanel`,
`TableLayoutPanel`, `SplitContainer`, `TabControl`/`TabPage`, `DataGridView` (text, check box, combo box, button,
link and image columns, editing, sorting, selection, data binding, cell formatting and validation events).

**Menus and toolbars**: `MenuStrip`, `ContextMenuStrip`, `ToolStrip`, `StatusStrip`, `ToolStripMenuItem` (shortcut
keys, checked items, nested menus), `ToolStripButton`, `ToolStripLabel`, `ToolStripStatusLabel`,
`ToolStripSeparator`, `ToolStripDropDownButton`, `ToolStripSplitButton`, `ToolStripTextBox`, `ToolStripComboBox`,
`ToolStripProgressBar`, `ToolStripControlHost`.

**Components and dialogs**: `Timer`, `ToolTip`, `ImageList` (images added in code, or from the `.resx` file as
`ImageStream`), `BindingSource`, `Binding` and
`BindingContext` (simple and list binding, `INotifyPropertyChanged`, `IBindingList`), `Clipboard` (text, images
through the device's clipboard, and data objects within the app),
`MessageBox` (every button set and icon), `OpenFileDialog`, `SaveFileDialog` and `FolderBrowserDialog` (the device's
storage picker), `ColorDialog`, `FontDialog`, `TextRenderer`.

**Layout**: `Anchor`, `Dock`, `Padding`, `Margin`, `AutoSize`, `MinimumSize`/`MaximumSize`, `SuspendLayout`/
`ResumeLayout`, flow and table layout, `AutoScaleMode.Font` and `AutoScaleDimensions` as the designer writes them.

**System.Drawing**: `Graphics` on a control (`Paint`, `CreateGraphics`, `FromHwnd`) and on a bitmap
(`Graphics.FromImage`): lines, rectangles, ellipses, arcs, pies, polygons, curves, paths, images (also with
`ImageAttributes` color matrices and from one bitmap into another or itself), strings, `MeasureString`, transforms,
clipping by rectangles, paths and regions, `SmoothingMode`, `PixelOffsetMode`, `CompositingMode`, `Clear`, `Flush`.
Pens have widths, dash styles and patterns, dash caps and offsets, start, end and custom caps (arrows included),
joins and miter limits, and alignment; brushes are `SolidBrush`, `LinearGradientBrush` (blends, interpolation colors,
transforms), `TextureBrush`, `HatchBrush` (every `HatchStyle`, with Windows' patterns) and `PathGradientBrush`.
Drawing on a bitmap is rasterized into its pixels as GDI+ does, close enough that the tests compare it with pictures
GDI+ drew on Windows. `Bitmap` and `Image`: PNG, JPEG, BMP, GIF and ICO from files and streams, `GetPixel`/`SetPixel`,
`LockBits`/`UnlockBits` (`Format32bppArgb`, `Format32bppPArgb`, `Format32bppRgb`, `Format24bppRgb`), `Clone`,
`RotateFlip`, `MakeTransparent`, `Save` as PNG, JPEG (with a quality encoder parameter), BMP, GIF and ICO.
`BufferedGraphicsContext`, `BufferedGraphicsManager`, `BufferedGraphics` and `DoubleBuffered` for drawing without
flicker. `GraphicsPath` (with `AddString`), `Region`, `Matrix`, `Font`, `FontFamily`, `StringFormat`, `Icon`, `Brushes`,
`Pens`, `SystemBrushes`, `SystemPens`, `SystemFonts`. `Color`, `Point`, `Size`, `Rectangle` and their `F` forms are
.NET's own.

**Resources**: `.resx` files are compiled as Visual Studio compiles them: strings, images, icons, files the `.resx`
refers to, values with a type such as a `Point`, and `ImageList.ImageStream` (read from the .NET Framework's
serialized form), so `Properties.Resources` and the images the designer stores in `Form1.resx` load with
`resources.GetObject` as on Windows. Other values saved with the .NET Framework's binary formatter are left out with a
warning, as .NET itself can no longer read them.

## Not supported

- Controls: `TreeView`, `ListView`, `MaskedTextBox`, `PropertyGrid`, `WebBrowser`, `HScrollBar`/`VScrollBar`,
  `ErrorProvider`, `NotifyIcon`, `HelpProvider`, `PrintDocument` and the print dialogs; the old `MainMenu`,
  `ContextMenu`, `StatusBar` and `ToolBar`.
- Windows itself: P/Invoke, COM, the registry, `WndProc` (there are no window messages other than the mouse and
  keyboard messages message filters see), and GDI handles: device contexts (`Graphics.GetHdc`), `Bitmap.GetHbitmap`,
  `Bitmap.GetHicon`.
- Drawing: metafiles; rich text formatting (a `RichTextBox` shows an RTF document's text in one font and color).
- Tool strip overflow menus, custom `ToolStripRenderer`s, owner-drawn menu items.
- A folder picked in `FolderBrowserDialog` must be on the device; a cloud folder has no path an app can use.

## What throws `NotSupportedException`, and why

Every `NotSupportedException` in this assembly was reviewed for 0.18. What tutorial and student apps reach for
(drawing on bitmaps, double buffering, MDI, RTF, `ImageStream`, clipboard images, `Application.Restart`, message
filters, `Graphics.FromHwnd`, `PathGradientBrush`, `GraphicsPath.AddString`) is implemented. What remains cannot exist
on Android, or is what .NET itself throws:

| Member | Why |
| --- | --- |
| `Graphics.GetHdc`, `Graphics.ReleaseHdc`, `BufferedGraphics.Render(IntPtr)`, `BufferedGraphicsContext.Allocate(IntPtr, Rectangle)` | They hand out or take a GDI device context, which only Windows has. Draw with the `Graphics` itself. |
| `Bitmap.GetHbitmap`, `Bitmap.GetHicon` | They create GDI bitmap and icon handles. Draw the bitmap with `DrawImage`, or save it with `ImageFormat.Icon`. |
| `TextRenderer.DrawText` on an `IDeviceContext` that is not a `Graphics` | Such a device context is a GDI handle; every `Graphics` is accepted. |
| `PictureBox.Load` of an address that is not a file, `http` or `https` | There is nothing else to load from. |
| Adding to read-only collections (`ListBox.SelectedItems`, `CheckedListBox.CheckedItems`, a row's `Cells`, `ControlCollection` set by index, `ImageList.Images.Insert`) | Windows Forms throws the same exception there. |
| `BindingSource` sorting of a list that cannot sort, `CurrencyManager.AddNew` on a list that cannot add, property-to-property `Binding` list operations | The list itself does not support it; Windows Forms throws the same exception. |

## The designer

`TabStudio.Forms.Design` opens `Form1.cs` on the form built from `Form1.Designer.cs`. It reads `InitializeComponent`
with Roslyn and never runs the app's code; it writes `InitializeComponent` back as Visual Studio does, in the file's
own style (`this.` and full type names, or the simplified style of projects with implicit usings), and keeps the
statements it does not understand. A control from the app itself, such as its own `UserControl`, is shown as a
placeholder of its size. Tool strip items are added from the Properties panel's Items ("Type Here"), and their
properties and events are edited there; images, the `DataGridView` column editor and collection editors other than
items are not part of the designer.
