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
`AutoScaleMode`, the `Load`, `Shown`, `Activated`, `FormClosing` and `FormClosed` events), `UserControl`,
`ContainerControl`, `ScrollableControl` (`AutoScroll`), `Control` (layout, painting, input, focus, validation,
`Invoke`/`BeginInvoke`, `CreateGraphics`), `WindowsFormsSynchronizationContext` (so `async` handlers and
`BackgroundWorker` come back to the UI thread), `Screen`, `SystemInformation`, `Cursor`/`Cursors`.

**Controls**: `Button`, `CheckBox`, `RadioButton`, `Label`, `LinkLabel`, `TextBox` (multiline, password, read-only,
`AutoComplete`), `RichTextBox` (plain text), `ComboBox`, `ListBox`, `CheckedListBox`, `NumericUpDown`, `DomainUpDown`,
`DateTimePicker`, `MonthCalendar`, `TrackBar`, `ProgressBar`, `PictureBox`, `GroupBox`, `Panel`, `FlowLayoutPanel`,
`TableLayoutPanel`, `SplitContainer`, `TabControl`/`TabPage`, `DataGridView` (text, check box, combo box, button,
link and image columns, editing, sorting, selection, data binding, cell formatting and validation events).

**Menus and toolbars**: `MenuStrip`, `ContextMenuStrip`, `ToolStrip`, `StatusStrip`, `ToolStripMenuItem` (shortcut
keys, checked items, nested menus), `ToolStripButton`, `ToolStripLabel`, `ToolStripStatusLabel`,
`ToolStripSeparator`, `ToolStripDropDownButton`, `ToolStripSplitButton`, `ToolStripTextBox`, `ToolStripComboBox`,
`ToolStripProgressBar`, `ToolStripControlHost`.

**Components and dialogs**: `Timer`, `ToolTip`, `ImageList` (images added in code), `BindingSource`, `Binding` and
`BindingContext` (simple and list binding, `INotifyPropertyChanged`, `IBindingList`), `Clipboard` (text),
`MessageBox` (every button set and icon), `OpenFileDialog`, `SaveFileDialog` and `FolderBrowserDialog` (the device's
storage picker), `ColorDialog`, `FontDialog`, `TextRenderer`.

**Layout**: `Anchor`, `Dock`, `Padding`, `Margin`, `AutoSize`, `MinimumSize`/`MaximumSize`, `SuspendLayout`/
`ResumeLayout`, flow and table layout, `AutoScaleMode.Font` and `AutoScaleDimensions` as the designer writes them.

**System.Drawing**: `Graphics` (lines, rectangles, ellipses, arcs, pies, polygons, curves, paths, images, strings,
`MeasureString`, transforms, clipping, `SmoothingMode`, `TextRenderingHint`), `Pen`, `SolidBrush`,
`LinearGradientBrush`, `Brushes`, `Pens`, `SystemBrushes`, `SystemPens`, `SystemFonts`, `Font`, `FontFamily`,
`StringFormat`, `Bitmap` and `Image` (PNG, JPEG, BMP and GIF from files and streams, `GetPixel`/`SetPixel`, `Save`,
which writes PNG whatever the format asked for),
`Icon`, `Region`, `GraphicsPath`, `Matrix`. `Color`, `Point`, `Size`, `Rectangle` and their `F` forms are .NET's own.

## Not supported

- Controls: `TreeView`, `ListView`, `MaskedTextBox`, `PropertyGrid`, `WebBrowser`, `HScrollBar`/`VScrollBar`,
  `ErrorProvider`, `NotifyIcon`, `HelpProvider`, `PrintDocument` and the print dialogs; the old `MainMenu`,
  `ContextMenu`, `StatusBar` and `ToolBar`.
- MDI (`IsMdiContainer`, `MdiParent`, `LayoutMdi`): show child forms as separate windows with `Show(owner)`.
- Resources: `.resx` files are not compiled, so `ImageList.ImageStream`, `Properties.Resources` and images the
  designer stores in `Form1.resx` are not available; load images with `Image.FromFile` or add them in code.
- Windows itself: P/Invoke, COM, the registry, window messages (there is no `WndProc` to override, and
  `Application.AddMessageFilter` throws), `Handle`-based APIs and GDI device contexts (`Graphics.GetHdc`,
  `Graphics.FromHwnd`), `Application.Restart`.
- Drawing: `Graphics.FromImage` (draw in a `Paint` handler, or set pixels with `Bitmap.SetPixel`),
  `GraphicsPath.AddString`, metafiles, `TextureBrush`, `PathGradientBrush`, hatch brushes.
- Rich text (`RichTextBox.Rtf`, RTF streams): `RichTextBox` holds plain text.
- Tool strip overflow menus, custom `ToolStripRenderer`s, owner-drawn menu items; the clipboard holds text only.
- A folder picked in `FolderBrowserDialog` must be on the device; a cloud folder has no path an app can use.

## The designer

`TabStudio.Forms.Design` opens `Form1.cs` on the form built from `Form1.Designer.cs`. It reads `InitializeComponent`
with Roslyn and never runs the app's code; it writes `InitializeComponent` back as Visual Studio does, in the file's
own style (`this.` and full type names, or the simplified style of projects with implicit usings), and keeps the
statements it does not understand. A control from the app itself, such as its own `UserControl`, is shown as a
placeholder of its size. Tool strip items are added from the Properties panel's Items ("Type Here"), and their
properties and events are edited there; images, the `DataGridView` column editor and collection editors other than
items are not part of the designer.
