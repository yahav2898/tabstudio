using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>A push button: clicking or tapping it raises Click; on a dialog it can close the dialog with a result.</summary>
public class Button : ButtonBase, IButtonControl
{
    private DialogResult _dialogResult;

    /// <summary>The result the button gives its form when clicked, which closes a modal dialog.</summary>
    [DefaultValue(DialogResult.None)]
    public virtual DialogResult DialogResult
    {
        get => _dialogResult;
        set => _dialogResult = value;
    }

    /// <summary>Tells the button whether it is its form's default button.</summary>
    /// <param name="value">Whether it is.</param>
    public virtual void NotifyDefault(bool value) => IsDefault = value;

    /// <summary>Clicks the button, as a tap does.</summary>
    public void PerformClick()
    {
        if (Enabled && (Visible || Window is null))
        {
            OnClick(EventArgs.Empty);
        }
    }

    /// <summary>Raises Click, after giving the form the button's DialogResult.</summary>
    /// <param name="e">The event data.</param>
    protected override void OnClick(EventArgs e)
    {
        if (_dialogResult != DialogResult.None && FindForm() is { } form)
        {
            form.DialogResult = _dialogResult;
        }

        base.OnClick(e);
    }

    /// <summary>The button's mnemonic clicks it.</summary>
    /// <param name="charCode">The character.</param>
    protected internal override bool ProcessMnemonic(char charCode)
    {
        if (UseMnemonic && CanProcessMnemonic() && IsMnemonic(charCode, Text))
        {
            PerformClick();
            return true;
        }

        return base.ProcessMnemonic(charCode);
    }

    /// <summary>Enter clicks the focused button.</summary>
    /// <param name="keyData">The key.</param>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if ((keyData & Keys.KeyCode) == Keys.Enter && (keyData & (Keys.Alt | Keys.Control)) == 0 && Focused)
        {
            PerformClick();
            return true;
        }

        return base.ProcessDialogKey(keyData);
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var bounds = new Rectangle(Point.Empty, ClientSize);
        var state = PushState;
        var emphasized = IsDefault || (Focused && Window?.FocusedControl is Button);
        if (UsesCustomFace)
        {
            if (FlatStyle is FlatStyle.Flat or FlatStyle.Popup)
            {
                var face = state switch
                {
                    PushState.Pressed when !FlatAppearance.MouseDownBackColor.IsEmpty => FlatAppearance.MouseDownBackColor,
                    PushState.Hot when !FlatAppearance.MouseOverBackColor.IsEmpty => FlatAppearance.MouseOverBackColor,
                    _ => BackColor,
                };
                var border = FlatAppearance.BorderColor.IsEmpty ? ForeColor : FlatAppearance.BorderColor;
                var stateForFace = (state is PushState.Pressed && !FlatAppearance.MouseDownBackColor.IsEmpty) || (state is PushState.Hot && !FlatAppearance.MouseOverBackColor.IsEmpty) ? PushState.Normal : state;
                ThemePainter.DrawFlatButton(g, bounds, stateForFace, face, border, FlatAppearance.BorderSize);
            }
            else
            {
                ThemePainter.DrawButton(g, bounds, state, emphasized, BackColor);
            }
        }
        else
        {
            ThemePainter.DrawButton(g, bounds, state, emphasized);
        }

        var text = !Enabled ? Theme.DisabledText : ForeColor;
        var area = new Rectangle(4 + Padding.Left, 3 + Padding.Top, bounds.Width - 8 - Padding.Horizontal, bounds.Height - 6 - Padding.Vertical);
        PaintTextAndImage(g, area, text);
        if (Focused && Window?.ShowFocusCues == true)
        {
            ThemePainter.DrawFocusRectangle(g, Rectangle.Inflate(bounds, -3, -3));
        }
    }

    /// <summary>The button paints its own face over the whole client area.</summary>
    /// <param name="pevent">The event data.</param>
    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        if (Parent is { } parent && BackColor.A < 255)
        {
            using var brush = new SolidBrush(parent.BackColor);
            pevent.Graphics.FillRectangle(brush, ClientRectangle);
        }
    }
}

/// <summary>A box the user checks or unchecks, with text beside it.</summary>
[DefaultEvent(nameof(CheckedChanged))]
public class CheckBox : ButtonBase
{
    private static readonly object EventCheckedChanged = new();
    private static readonly object EventCheckStateChanged = new();
    private static readonly object EventAppearanceChanged = new();
    private CheckState _checkState;
    private ContentAlignment _checkAlign = ContentAlignment.MiddleLeft;
    private Appearance _appearance;

    /// <summary>Creates a check box.</summary>
    public CheckBox() => TextAlign = ContentAlignment.MiddleLeft;

    /// <summary>Occurs when <see cref="Checked"/> changes.</summary>
    public event EventHandler? CheckedChanged
    {
        add => Events.AddHandler(EventCheckedChanged, value);
        remove => Events.RemoveHandler(EventCheckedChanged, value);
    }

    /// <summary>Occurs when <see cref="CheckState"/> changes.</summary>
    public event EventHandler? CheckStateChanged
    {
        add => Events.AddHandler(EventCheckStateChanged, value);
        remove => Events.RemoveHandler(EventCheckStateChanged, value);
    }

    /// <summary>Occurs when <see cref="Appearance"/> changes.</summary>
    public event EventHandler? AppearanceChanged
    {
        add => Events.AddHandler(EventAppearanceChanged, value);
        remove => Events.RemoveHandler(EventAppearanceChanged, value);
    }

    /// <summary>Whether the box is checked.</summary>
    [DefaultValue(false)]
    public bool Checked
    {
        get => _checkState != CheckState.Unchecked;
        set => CheckState = value ? CheckState.Checked : CheckState.Unchecked;
    }

    /// <summary>Checked, unchecked or indeterminate.</summary>
    [DefaultValue(CheckState.Unchecked)]
    public CheckState CheckState
    {
        get => _checkState;
        set
        {
            if (_checkState == value)
            {
                return;
            }

            var wasChecked = Checked;
            _checkState = value;
            Invalidate();
            if (wasChecked != Checked)
            {
                OnCheckedChanged(EventArgs.Empty);
            }

            OnCheckStateChanged(EventArgs.Empty);
        }
    }

    /// <summary>Whether a click goes through three states: unchecked, checked, indeterminate.</summary>
    public bool ThreeState { get; set; }

    /// <summary>Whether a click checks and unchecks the box.</summary>
    [DefaultValue(true)]
    public bool AutoCheck { get; set; } = true;

    /// <summary>Whether the check box looks like a box or a button.</summary>
    public Appearance Appearance
    {
        get => _appearance;
        set
        {
            _appearance = value;
            Invalidate();
            ((EventHandler?)Events[EventAppearanceChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Where the box sits.</summary>
    [DefaultValue(ContentAlignment.MiddleLeft)]
    public ContentAlignment CheckAlign
    {
        get => _checkAlign;
        set
        {
            _checkAlign = value;
            Invalidate();
        }
    }

    /// <inheritdoc />
    [DefaultValue(ContentAlignment.MiddleLeft)]
    public override ContentAlignment TextAlign
    {
        get => base.TextAlign;
        set => base.TextAlign = value;
    }

    /// <inheritdoc />
    protected override Size DefaultSize => new(104, 24);

    /// <summary>Raises CheckedChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCheckedChanged(EventArgs e) => ((EventHandler?)Events[EventCheckedChanged])?.Invoke(this, e);

    /// <summary>Raises CheckStateChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCheckStateChanged(EventArgs e) => ((EventHandler?)Events[EventCheckStateChanged])?.Invoke(this, e);

    /// <summary>The box's mnemonic focuses it and checks or unchecks it.</summary>
    /// <param name="charCode">The character.</param>
    protected internal override bool ProcessMnemonic(char charCode)
    {
        if (UseMnemonic && CanSelect && IsMnemonic(charCode, Text))
        {
            if (Focus())
            {
                OnClick(EventArgs.Empty);
            }

            return true;
        }

        return base.ProcessMnemonic(charCode);
    }

    /// <summary>Checks or unchecks the box, then raises Click.</summary>
    /// <param name="e">The event data.</param>
    protected override void OnClick(EventArgs e)
    {
        if (AutoCheck)
        {
            CheckState = _checkState switch
            {
                CheckState.Unchecked => CheckState.Checked,
                CheckState.Checked when ThreeState => CheckState.Indeterminate,
                _ => CheckState.Unchecked,
            };
        }

        base.OnClick(e);
    }

    /// <inheritdoc />
    internal override Size GetPreferredSizeCore(Size proposedSize)
    {
        if (_appearance == Appearance.Button)
        {
            return base.GetPreferredSizeCore(proposedSize);
        }

        var text = TextMetrics.Measure(Graphics.Prefixed(Text, System.Drawing.Text.HotkeyPrefix.Hide), Font.Spec);
        return new Size(
            ThemePainter.GlyphSize + 4 + (int)Math.Ceiling(text.Width) + 3 + Padding.Horizontal,
            Math.Max(ThemePainter.GlyphSize + 4, (int)Math.Ceiling(text.Height) + 4) + Padding.Vertical);
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var bounds = new Rectangle(Point.Empty, ClientSize);
        var color = Enabled ? ForeColor : Theme.DisabledText;
        if (_appearance == Appearance.Button)
        {
            var state = Checked && PushState is PushState.Normal ? PushState.Pressed : PushState;
            ThemePainter.DrawButton(g, bounds, state, emphasized: false, UsesCustomFace ? BackColor : null);
            PaintTextAndImage(g, Rectangle.Inflate(bounds, -4, -3), color);
            return;
        }

        var content = new Rectangle(Padding.Left, Padding.Top, bounds.Width - Padding.Horizontal, bounds.Height - Padding.Vertical);
        var (glyph, text) = ToggleLayout.Split(content, _checkAlign);
        ThemePainter.DrawCheckBox(g, glyph, _checkState, PushState);
        PaintTextAndImage(g, text, color);
        if (Focused && Window?.ShowFocusCues == true)
        {
            ThemePainter.DrawFocusRectangle(g, text);
        }
    }
}

/// <summary>One of a group of options, of which the user picks one: the radio buttons of one parent exclude each other.</summary>
[DefaultEvent(nameof(CheckedChanged))]
public class RadioButton : ButtonBase
{
    private static readonly object EventCheckedChanged = new();
    private static readonly object EventAppearanceChanged = new();
    private bool _checked;
    private ContentAlignment _checkAlign = ContentAlignment.MiddleLeft;
    private Appearance _appearance;

    /// <summary>Creates a radio button.</summary>
    public RadioButton() => TextAlign = ContentAlignment.MiddleLeft;

    /// <summary>Occurs when <see cref="Checked"/> changes.</summary>
    public event EventHandler? CheckedChanged
    {
        add => Events.AddHandler(EventCheckedChanged, value);
        remove => Events.RemoveHandler(EventCheckedChanged, value);
    }

    /// <summary>Occurs when <see cref="Appearance"/> changes.</summary>
    public event EventHandler? AppearanceChanged
    {
        add => Events.AddHandler(EventAppearanceChanged, value);
        remove => Events.RemoveHandler(EventAppearanceChanged, value);
    }

    /// <summary>Whether the option is chosen; checking it unchecks the other radio buttons of its parent.</summary>
    [DefaultValue(false)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            if (value && AutoCheck)
            {
                UncheckSiblings();
            }

            Invalidate();
            OnCheckedChanged(EventArgs.Empty);
        }
    }

    /// <summary>Whether a click checks the button and unchecks the others.</summary>
    [DefaultValue(true)]
    public bool AutoCheck { get; set; } = true;

    /// <summary>Whether the radio button looks like a circle or a button.</summary>
    public Appearance Appearance
    {
        get => _appearance;
        set
        {
            _appearance = value;
            Invalidate();
            ((EventHandler?)Events[EventAppearanceChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Where the circle sits.</summary>
    [DefaultValue(ContentAlignment.MiddleLeft)]
    public ContentAlignment CheckAlign
    {
        get => _checkAlign;
        set
        {
            _checkAlign = value;
            Invalidate();
        }
    }

    /// <inheritdoc />
    [DefaultValue(ContentAlignment.MiddleLeft)]
    public override ContentAlignment TextAlign
    {
        get => base.TextAlign;
        set => base.TextAlign = value;
    }

    /// <inheritdoc />
    protected override Size DefaultSize => new(104, 24);

    /// <summary>Clicks the radio button.</summary>
    public void PerformClick()
    {
        if (Enabled)
        {
            OnClick(EventArgs.Empty);
        }
    }

    /// <summary>Raises CheckedChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCheckedChanged(EventArgs e) => ((EventHandler?)Events[EventCheckedChanged])?.Invoke(this, e);

    /// <summary>The button's mnemonic focuses and checks it.</summary>
    /// <param name="charCode">The character.</param>
    protected internal override bool ProcessMnemonic(char charCode)
    {
        if (UseMnemonic && CanSelect && IsMnemonic(charCode, Text))
        {
            if (Focus())
            {
                PerformClick();
            }

            return true;
        }

        return base.ProcessMnemonic(charCode);
    }

    /// <summary>Checks the button, then raises Click.</summary>
    /// <param name="e">The event data.</param>
    protected override void OnClick(EventArgs e)
    {
        if (AutoCheck)
        {
            Checked = true;
        }

        base.OnClick(e);
    }

    /// <summary>The arrow keys move to the next option of the group and check it.</summary>
    /// <param name="keyData">The key.</param>
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyCode is not (Keys.Left or Keys.Right or Keys.Up or Keys.Down) || Parent is not { } parent)
        {
            return;
        }

        var options = parent.Controls.ToArray().OfType<RadioButton>().Where(r => r.CanSelect).OrderBy(r => r.TabIndex).ToList();
        var index = options.IndexOf(this);
        if (options.Count > 1 && index >= 0)
        {
            var forward = e.KeyCode is Keys.Right or Keys.Down;
            var next = options[(index + (forward ? 1 : options.Count - 1)) % options.Count];
            next.Focus();
            next.PerformClick();
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    internal override Size GetPreferredSizeCore(Size proposedSize)
    {
        if (_appearance == Appearance.Button)
        {
            return base.GetPreferredSizeCore(proposedSize);
        }

        var text = TextMetrics.Measure(Graphics.Prefixed(Text, System.Drawing.Text.HotkeyPrefix.Hide), Font.Spec);
        return new Size(
            ThemePainter.GlyphSize + 4 + (int)Math.Ceiling(text.Width) + 3 + Padding.Horizontal,
            Math.Max(ThemePainter.GlyphSize + 4, (int)Math.Ceiling(text.Height) + 4) + Padding.Vertical);
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var bounds = new Rectangle(Point.Empty, ClientSize);
        var color = Enabled ? ForeColor : Theme.DisabledText;
        if (_appearance == Appearance.Button)
        {
            var state = _checked && PushState is PushState.Normal ? PushState.Pressed : PushState;
            ThemePainter.DrawButton(g, bounds, state, emphasized: false, UsesCustomFace ? BackColor : null);
            PaintTextAndImage(g, Rectangle.Inflate(bounds, -4, -3), color);
            return;
        }

        var content = new Rectangle(Padding.Left, Padding.Top, bounds.Width - Padding.Horizontal, bounds.Height - Padding.Vertical);
        var (glyph, text) = ToggleLayout.Split(content, _checkAlign);
        ThemePainter.DrawRadio(g, glyph, _checked, PushState);
        PaintTextAndImage(g, text, color);
        if (Focused && Window?.ShowFocusCues == true)
        {
            ThemePainter.DrawFocusRectangle(g, text);
        }
    }

    private void UncheckSiblings()
    {
        if (Parent is not { } parent)
        {
            return;
        }

        foreach (var sibling in parent.Controls.ToArray().OfType<RadioButton>())
        {
            if (sibling != this && sibling.AutoCheck && sibling.Checked)
            {
                sibling.Checked = false;
            }
        }
    }
}

/// <summary>Where the glyph and the text of a check box or radio button go.</summary>
internal static class ToggleLayout
{
    /// <summary>Splits a check box's area into its glyph and its text, the glyph at the check alignment.</summary>
    public static (Rectangle Glyph, Rectangle Text) Split(Rectangle area, ContentAlignment checkAlign)
    {
        var size = ThemePainter.GlyphSize;
        var glyph = ThemePainter.Align(area, new Size(size, size), checkAlign);
        var onRight = ThemePainter.Horizontal(checkAlign) == StringAlignment.Far;
        var text = onRight
            ? Rectangle.FromLTRB(area.Left, area.Top, glyph.Left - 3, area.Bottom)
            : Rectangle.FromLTRB(glyph.Right + 4, area.Top, area.Right, area.Bottom);
        return (glyph, text);
    }
}
