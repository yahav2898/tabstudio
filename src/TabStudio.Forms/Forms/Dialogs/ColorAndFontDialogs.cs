using System.ComponentModel;
using System.Drawing;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>A dialog to pick a color from the Windows basic colors and the user's custom colors.</summary>
[DefaultProperty(nameof(Color))]
public class ColorDialog : CommonDialog
{
    private int[] _customColors = new int[16];

    /// <summary>Creates a color dialog.</summary>
    public ColorDialog()
    {
        Array.Fill(_customColors, 0xFFFFFF);
    }

    /// <summary>The picked color.</summary>
    public Color Color { get; set; } = Color.Black;

    /// <summary>Whether the user can define custom colors.</summary>
    public virtual bool AllowFullOpen { get; set; } = true;

    /// <summary>Whether every available color shows. Kept for compatibility.</summary>
    public virtual bool AnyColor { get; set; }

    /// <summary>Whether the custom colors show when the dialog opens. Kept for compatibility.</summary>
    public virtual bool FullOpen { get; set; }

    /// <summary>Whether only solid colors can be picked. Kept for compatibility.</summary>
    public virtual bool SolidColorOnly { get; set; }

    /// <summary>Whether a help button shows. Kept for compatibility.</summary>
    public virtual bool ShowHelp { get; set; }

    /// <summary>The custom colors, as 0x00BBGGRR values.</summary>
    public int[] CustomColors
    {
        get => [.. _customColors];
        set
        {
            _customColors = new int[16];
            Array.Fill(_customColors, 0xFFFFFF);
            if (value is not null)
            {
                Array.Copy(value, _customColors, Math.Min(16, value.Length));
            }
        }
    }

    /// <inheritdoc />
    public override void Reset()
    {
        Color = Color.Black;
        CustomColors = [];
        AllowFullOpen = true;
        FullOpen = false;
    }

    /// <inheritdoc />
    public override string ToString() => $"{base.ToString()},  Color: {Color}";

    /// <inheritdoc />
    protected override bool RunDialog(IntPtr hwndOwner)
    {
        using var dialog = new ColorDialogForm(Color, _customColors, AllowFullOpen);
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return false;
        }

        Color = dialog.Picked;
        _customColors = dialog.Custom;
        return true;
    }
}

/// <summary>A dialog to pick a font: its family, style, size and effects.</summary>
[DefaultProperty(nameof(Font))]
public class FontDialog : CommonDialog
{
    /// <summary>The families the dialog offers: the common Windows families, drawn with the device's.</summary>
    internal static readonly string[] Families =
    [
        "Arial", "Calibri", "Cambria", "Comic Sans MS", "Consolas", "Courier New", "Georgia", "Lucida Console",
        "Microsoft Sans Serif", "Segoe UI", "Tahoma", "Times New Roman", "Trebuchet MS", "Verdana",
    ];

    private static readonly object EventApply = new();

    /// <summary>Creates a font dialog.</summary>
    public FontDialog()
    {
    }

    /// <summary>Occurs when the user applies the font. Kept for compatibility.</summary>
    public event EventHandler? Apply
    {
        add => Events.AddHandler(EventApply, value);
        remove => Events.RemoveHandler(EventApply, value);
    }

    /// <summary>The picked font.</summary>
    public Font Font { get; set; } = Control.DefaultFont;

    /// <summary>The picked color, when <see cref="ShowColor"/> is on.</summary>
    public Color Color { get; set; } = Color.Black;

    /// <summary>Whether a color can be picked.</summary>
    public bool ShowColor { get; set; }

    /// <summary>Whether underline and strikeout can be picked.</summary>
    public bool ShowEffects { get; set; } = true;

    /// <summary>Whether an Apply button shows. Kept for compatibility.</summary>
    public bool ShowApply { get; set; }

    /// <summary>Whether a help button shows. Kept for compatibility.</summary>
    public bool ShowHelp { get; set; }

    /// <summary>The least size; 0 for none.</summary>
    public int MinSize { get; set; }

    /// <summary>The greatest size; 0 for none.</summary>
    public int MaxSize { get; set; }

    /// <summary>Whether the font must exist. Kept for compatibility.</summary>
    public bool FontMustExist { get; set; }

    /// <summary>Whether vertical fonts are offered. Kept for compatibility.</summary>
    public bool AllowVerticalFonts { get; set; } = true;

    /// <summary>Whether simulated styles are offered. Kept for compatibility.</summary>
    public bool AllowSimulations { get; set; } = true;

    /// <summary>Whether vector fonts are offered. Kept for compatibility.</summary>
    public bool AllowVectorFonts { get; set; } = true;

    /// <summary>Whether the script can be changed. Kept for compatibility.</summary>
    public bool AllowScriptChange { get; set; } = true;

    /// <summary>Whether only fixed-pitch fonts are offered. Kept for compatibility.</summary>
    public bool FixedPitchOnly { get; set; }

    /// <summary>Whether symbol fonts are left out. Kept for compatibility.</summary>
    public bool ScriptsOnly { get; set; }

    /// <inheritdoc />
    public override void Reset()
    {
        Font = Control.DefaultFont;
        Color = Color.Black;
        ShowColor = false;
        ShowEffects = true;
        MinSize = 0;
        MaxSize = 0;
    }

    /// <inheritdoc />
    public override string ToString() => $"{base.ToString()},  Font: {Font}";

    /// <summary>Raises Apply.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnApply(EventArgs e) => ((EventHandler?)Events[EventApply])?.Invoke(this, e);

    /// <inheritdoc />
    protected override bool RunDialog(IntPtr hwndOwner)
    {
        using var dialog = new FontDialogForm(this);
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return false;
        }

        Font = dialog.Picked;
        Color = dialog.PickedColor;
        return true;
    }
}

/// <summary>The window of a color dialog: the basic colors, the custom colors, and OK and Cancel.</summary>
internal sealed class ColorDialogForm : Form
{
    private readonly ColorSwatches _basic;
    private readonly ColorSwatches _custom;

    public ColorDialogForm(Color color, int[] customColors, bool allowCustom)
    {
        Text = "Color";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(230, 280);
        var basicLabel = new Label { Text = "&Basic colors:", Location = new Point(8, 8), AutoSize = true };
        _basic = new ColorSwatches(ColorSwatches.Basic, columns: 8) { Location = new Point(8, 28) };
        var customLabel = new Label { Text = "&Custom colors:", Location = new Point(8, 170), AutoSize = true };
        _custom = new ColorSwatches([.. customColors.Select(ColorTranslator.FromWin32)], columns: 8) { Location = new Point(8, 190) };
        _basic.Picked += (_, _) => _custom.Selected = -1;
        _custom.Picked += (_, _) => _basic.Selected = -1;
        var add = new Button { Text = "&Add to Custom Colors", Location = new Point(8, 246), Size = new Size(130, 23), Enabled = allowCustom };
        add.Click += (_, _) => _custom.Replace(Picked);
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(146, 218), Size = new Size(75, 23) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(146, 246), Size = new Size(75, 23) };
        Controls.AddRange([basicLabel, _basic, customLabel, _custom, add, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
        _basic.Selected = Array.FindIndex(ColorSwatches.Basic, c => c.ToArgb() == color.ToArgb());
        if (_basic.Selected < 0)
        {
            _custom.Replace(color);
        }
    }

    /// <summary>The color picked.</summary>
    public Color Picked => _basic.Selected >= 0 ? _basic.Colors[_basic.Selected] : _custom.Selected >= 0 ? _custom.Colors[_custom.Selected] : Color.Black;

    /// <summary>The custom colors, as 0x00BBGGRR values.</summary>
    public int[] Custom => [.. _custom.Colors.Select(ColorTranslator.ToWin32)];
}

/// <summary>A grid of color swatches to tap.</summary>
internal sealed class ColorSwatches : Control
{
    /// <summary>The Windows color dialog's 48 basic colors.</summary>
    public static readonly Color[] Basic =
    [
        .. new[]
        {
            0xFF8080, 0xFFFF80, 0x80FF80, 0x00FF80, 0x80FFFF, 0x0080FF, 0xFF80C0, 0xFF80FF,
            0xFF0000, 0xFFFF00, 0x80FF00, 0x00FF40, 0x00FFFF, 0x0080C0, 0x8080C0, 0xFF00FF,
            0x804040, 0xFF8040, 0x00FF00, 0x008080, 0x004080, 0x8080FF, 0x800040, 0xFF0080,
            0x800000, 0xFF8000, 0x008000, 0x008040, 0x0000FF, 0x0000A0, 0x800080, 0x8000FF,
            0x400000, 0x804000, 0x004000, 0x004040, 0x000080, 0x000040, 0x400040, 0x400080,
            0x000000, 0x808000, 0x808040, 0x808080, 0x408080, 0xC0C0C0, 0x400040, 0xFFFFFF,
        }.Select(rgb => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF)),
    ];

    private const int Cell = 26;
    private readonly int _columns;
    private int _selected = -1;
    private int _next;

    public ColorSwatches(Color[] colors, int columns)
    {
        Colors = colors;
        _columns = columns;
        Size = new Size(columns * Cell, (int)Math.Ceiling(colors.Length / (double)columns) * Cell);
    }

    /// <summary>Occurs when a swatch is tapped.</summary>
    public event EventHandler? Picked;

    /// <summary>The colors.</summary>
    public Color[] Colors { get; }

    /// <summary>The selected swatch, or -1.</summary>
    public int Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            Invalidate();
        }
    }

    /// <summary>Puts a color in the next swatch, as the custom colors fill up.</summary>
    public void Replace(Color color)
    {
        Colors[_next] = color;
        Selected = _next;
        _next = (_next + 1) % Colors.Length;
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var index = (e.Y / Cell * _columns) + (e.X / Cell);
        if (e.X >= 0 && e.Y >= 0 && index < Colors.Length)
        {
            Selected = index;
            Picked?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        for (var i = 0; i < Colors.Length; i++)
        {
            var cell = new Rectangle(i % _columns * Cell, i / _columns * Cell, Cell, Cell);
            var swatch = Rectangle.Inflate(cell, -4, -4);
            using (var brush = new SolidBrush(Colors[i]))
            {
                g.FillRectangle(brush, swatch);
            }

            using var border = new Pen(i == _selected ? Color.Black : Color.FromArgb(160, 160, 160), i == _selected ? 2 : 1);
            g.DrawRectangle(border, swatch.X, swatch.Y, swatch.Width - 1, swatch.Height - 1);
        }
    }
}

/// <summary>The window of a font dialog.</summary>
internal sealed class FontDialogForm : Form
{
    private static readonly int[] Sizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72];
    private readonly ListBox _families = new() { Location = new Point(12, 48), Size = new Size(150, 124), IntegralHeight = false };
    private readonly ListBox _styles = new() { Location = new Point(172, 48), Size = new Size(110, 124), IntegralHeight = false };
    private readonly NumericUpDown _size = new() { Location = new Point(292, 24), Size = new Size(70, 23), Minimum = 1, Maximum = 400, DecimalPlaces = 0 };
    private readonly ListBox _sizes = new() { Location = new Point(292, 48), Size = new Size(70, 124), IntegralHeight = false };
    private readonly CheckBox _strikeout = new() { Text = "Stri&keout", Location = new Point(12, 200), AutoSize = true };
    private readonly CheckBox _underline = new() { Text = "&Underline", Location = new Point(12, 224), AutoSize = true };
    private readonly ComboBox _color = new() { Location = new Point(12, 270), Size = new Size(120, 23), DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _sample = new() { Text = "AaBbYyZz", AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };

    public FontDialogForm(FontDialog dialog)
    {
        Text = "Font";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(374, 320);
        _families.Items.AddRange(FontDialog.Families.Cast<object>().ToArray());
        if (!FontDialog.Families.Contains(dialog.Font.Name, StringComparer.OrdinalIgnoreCase))
        {
            _families.Items.Add(dialog.Font.Name);
        }

        _styles.Items.AddRange(new object[] { "Regular", "Italic", "Bold", "Bold Italic" });
        _sizes.Items.AddRange(Sizes.Select(size => (object)size.ToString(CultureInfo.CurrentCulture)).ToArray());
        _size.Minimum = dialog.MinSize > 0 ? dialog.MinSize : 1;
        _size.Maximum = dialog.MaxSize > 0 ? dialog.MaxSize : 400;
        var effects = new GroupBox { Text = "Effects", Location = new Point(8, 180), Size = new Size(140, 70), Visible = dialog.ShowEffects };
        _strikeout.Location = new Point(10, 20);
        _underline.Location = new Point(10, 42);
        effects.Controls.AddRange([_strikeout, _underline]);
        var colorLabel = new Label { Text = "&Color:", Location = new Point(12, 252), AutoSize = true, Visible = dialog.ShowColor };
        _color.Visible = dialog.ShowColor;
        foreach (var known in new[] { Color.Black, Color.Maroon, Color.Green, Color.Olive, Color.Navy, Color.Purple, Color.Teal, Color.Gray, Color.Silver, Color.Red, Color.Lime, Color.Yellow, Color.Blue, Color.Fuchsia, Color.Aqua, Color.White })
        {
            _color.Items.Add(known.Name);
        }

        var sampleBox = new GroupBox { Text = "Sample", Location = new Point(160, 180), Size = new Size(202, 90) };
        sampleBox.Controls.Add(_sample);
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(206, 284), Size = new Size(75, 23) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(287, 284), Size = new Size(75, 23) };
        Controls.AddRange(
        [
            new Label { Text = "&Font:", Location = new Point(12, 8), AutoSize = true },
            new Label { Text = "Font st&yle:", Location = new Point(172, 8), AutoSize = true },
            new Label { Text = "&Size:", Location = new Point(292, 8), AutoSize = true },
            _families, _styles, _size, _sizes, effects, colorLabel, _color, sampleBox, ok, cancel,
        ]);
        AcceptButton = ok;
        CancelButton = cancel;

        _families.SelectedItem = _families.Items.Cast<object>().FirstOrDefault(item => string.Equals((string)item, dialog.Font.Name, StringComparison.OrdinalIgnoreCase));
        _styles.SelectedIndex = (dialog.Font.Bold ? 2 : 0) + (dialog.Font.Italic ? 1 : 0);
        _size.Value = (decimal)Math.Round(dialog.Font.SizeInPoints);
        _sizes.SelectedItem = _sizes.Items.Cast<object>().FirstOrDefault(item => (string)item == _size.Value.ToString(CultureInfo.CurrentCulture));
        _strikeout.Checked = dialog.Font.Strikeout;
        _underline.Checked = dialog.Font.Underline;
        _color.SelectedItem = _color.Items.Cast<object>().FirstOrDefault(item => (string)item == dialog.Color.Name) ?? "Black";
        _families.SelectedIndexChanged += (_, _) => UpdateSample();
        _styles.SelectedIndexChanged += (_, _) => UpdateSample();
        _sizes.SelectedIndexChanged += (_, _) =>
        {
            if (_sizes.SelectedItem is string text && decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var size))
            {
                _size.Value = Math.Clamp(size, _size.Minimum, _size.Maximum);
            }
        };
        _size.ValueChanged += (_, _) => UpdateSample();
        _strikeout.CheckedChanged += (_, _) => UpdateSample();
        _underline.CheckedChanged += (_, _) => UpdateSample();
        _color.SelectedIndexChanged += (_, _) => UpdateSample();
        UpdateSample();
    }

    /// <summary>The font picked.</summary>
    public Font Picked
    {
        get
        {
            var style = _styles.SelectedIndex switch
            {
                1 => FontStyle.Italic,
                2 => FontStyle.Bold,
                3 => FontStyle.Bold | FontStyle.Italic,
                _ => FontStyle.Regular,
            };

            if (_strikeout.Checked)
            {
                style |= FontStyle.Strikeout;
            }

            if (_underline.Checked)
            {
                style |= FontStyle.Underline;
            }

            return new Font(_families.SelectedItem as string ?? "Segoe UI", (float)_size.Value, style);
        }
    }

    /// <summary>The color picked.</summary>
    public Color PickedColor => _color.SelectedItem is string name ? Color.FromName(name) : Color.Black;

    private void UpdateSample()
    {
        _sample.Font = Picked;
        _sample.ForeColor = PickedColor;
    }
}
