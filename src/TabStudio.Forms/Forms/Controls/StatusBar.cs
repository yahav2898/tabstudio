using System.Collections;
using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>How a <see cref="StatusBarPanel"/> sizes itself in its bar.</summary>
public enum StatusBarPanelAutoSize
{
    /// <summary>The panel keeps its <see cref="StatusBarPanel.Width"/>.</summary>
    None = 1,

    /// <summary>The panel shares the space the other panels leave.</summary>
    Spring = 2,

    /// <summary>The panel fits its text.</summary>
    Contents = 3,
}

/// <summary>The border drawn around a <see cref="StatusBarPanel"/>.</summary>
public enum StatusBarPanelBorderStyle
{
    /// <summary>No border.</summary>
    None = 1,

    /// <summary>A raised border.</summary>
    Raised = 2,

    /// <summary>A sunken border.</summary>
    Sunken = 3,
}

/// <summary>Whether a <see cref="StatusBarPanel"/> draws its text or lets its bar's owner draw it.</summary>
public enum StatusBarPanelStyle
{
    /// <summary>The panel draws its text.</summary>
    Text = 1,

    /// <summary>The bar raises <see cref="StatusBar.DrawItem"/> for the panel.</summary>
    OwnerDraw = 2,
}

/// <summary>A panel of a <see cref="StatusBar"/>, the .NET Framework status bar.</summary>
[DefaultProperty(nameof(Text))]
public class StatusBarPanel : Component, ISupportInitialize
{
    private string _text = string.Empty;
    private int _width = 100;
    private int _minWidth = 10;
    private StatusBarPanelAutoSize _autoSize = StatusBarPanelAutoSize.None;
    private HorizontalAlignment _alignment = HorizontalAlignment.Left;
    private StatusBarPanelBorderStyle _borderStyle = StatusBarPanelBorderStyle.Sunken;

    /// <summary>The bar the panel is in.</summary>
    public StatusBar? Parent { get; internal set; }

    /// <summary>The panel's name, as the designer sets it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The text the panel shows.</summary>
    [Localizable(true)]
    public string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (_text != value)
            {
                _text = value;
                Changed();
            }
        }
    }

    /// <summary>The text shown when the pointer rests on the panel.</summary>
    [Localizable(true)]
    public string ToolTipText { get; set; } = string.Empty;

    /// <summary>The panel's width; panels that spring or fit their contents change it as the bar lays out.</summary>
    public int Width
    {
        get => _width;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _width = Math.Max(value, _minWidth);
            Changed();
        }
    }

    /// <summary>The least width the panel takes.</summary>
    public int MinWidth
    {
        get => _minWidth;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _minWidth = value;
            _width = Math.Max(_width, value);
            Changed();
        }
    }

    /// <summary>How the panel sizes itself.</summary>
    public StatusBarPanelAutoSize AutoSize
    {
        get => _autoSize;
        set
        {
            _autoSize = value;
            Changed();
        }
    }

    /// <summary>Where the text sits in the panel.</summary>
    public HorizontalAlignment Alignment
    {
        get => _alignment;
        set
        {
            _alignment = value;
            Changed();
        }
    }

    /// <summary>The panel's border.</summary>
    public StatusBarPanelBorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            Changed();
        }
    }

    /// <summary>Whether the panel draws its text or its owner draws it.</summary>
    public StatusBarPanelStyle Style { get; set; } = StatusBarPanelStyle.Text;

    /// <summary>The icon shown before the text.</summary>
    public Icon? Icon { get; set; }

    /// <summary>Anything the program keeps with the panel.</summary>
    public object? Tag { get; set; }

    /// <summary>The width the text needs, with the panel's margins.</summary>
    internal int ContentsWidth => TextRenderer.MeasureText(_text, Parent?.Font).Width + 8;

    /// <inheritdoc />
    public void BeginInit()
    {
    }

    /// <inheritdoc />
    public void EndInit() => Changed();

    /// <inheritdoc />
    public override string ToString() => "StatusBarPanel: {" + _text + "}";

    /// <summary>Sets the width the bar's layout gave the panel, without laying the bar out again.</summary>
    internal void SetWidthQuietly(int width) => _width = Math.Max(width, _minWidth);

    private void Changed() => Parent?.PanelsChanged();
}

/// <summary>Data for <see cref="StatusBar.PanelClick"/>.</summary>
/// <param name="statusBarPanel">The panel that was clicked.</param>
/// <param name="button">The mouse button.</param>
/// <param name="clicks">How many times it was clicked.</param>
/// <param name="x">Where, across the bar.</param>
/// <param name="y">Where, down the bar.</param>
public class StatusBarPanelClickEventArgs(StatusBarPanel statusBarPanel, MouseButtons button, int clicks, int x, int y)
    : MouseEventArgs(button, clicks, x, y, 0)
{
    /// <summary>The panel that was clicked.</summary>
    public StatusBarPanel StatusBarPanel { get; } = statusBarPanel;
}

/// <summary>Handles <see cref="StatusBar.PanelClick"/>.</summary>
/// <param name="sender">The bar.</param>
/// <param name="e">The panel and the click.</param>
public delegate void StatusBarPanelClickEventHandler(object sender, StatusBarPanelClickEventArgs e);

/// <summary>Data for <see cref="StatusBar.DrawItem"/>.</summary>
/// <param name="g">Where to draw.</param>
/// <param name="font">The bar's font.</param>
/// <param name="r">The panel's bounds.</param>
/// <param name="itemId">The panel's index.</param>
/// <param name="itemState">The panel's state.</param>
/// <param name="panel">The panel to draw.</param>
public class StatusBarDrawItemEventArgs(Graphics g, Font font, Rectangle r, int itemId, DrawItemState itemState, StatusBarPanel panel)
    : DrawItemEventArgs(g, font, r, itemId, itemState)
{
    /// <summary>The panel to draw.</summary>
    public StatusBarPanel Panel { get; } = panel;
}

/// <summary>Handles <see cref="StatusBar.DrawItem"/>.</summary>
/// <param name="sender">The bar.</param>
/// <param name="sbdevent">The panel to draw.</param>
public delegate void StatusBarDrawItemEventHandler(object sender, StatusBarDrawItemEventArgs sbdevent);

/// <summary>
/// The .NET Framework's status bar, which .NET Core removed in favour of <see cref="StatusStrip"/>: a bar docked to the
/// bottom of its form that shows its <see cref="Control.Text"/>, or its <see cref="Panels"/> when
/// <see cref="ShowPanels"/> is set. Programs written for the .NET Framework, such as VBDev2Dev's Dots, still use it.
/// </summary>
[DefaultEvent(nameof(PanelClick))]
[DefaultProperty(nameof(Text))]
public class StatusBar : Control
{
    private static readonly object EventPanelClick = new();
    private static readonly object EventDrawItem = new();
    private bool _showPanels;
    private bool _sizingGrip = true;

    // Created on first use: the base constructor already lays the bar out, before this class's fields are set.
    private StatusBarPanelCollection? _panels;

    /// <summary>Creates a status bar, docked to the bottom.</summary>
    public StatusBar()
    {
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        Dock = DockStyle.Bottom;
    }

    /// <summary>The bar's panels, shown when <see cref="ShowPanels"/> is set.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public StatusBarPanelCollection Panels => _panels ??= new StatusBarPanelCollection(this);

    /// <summary>Whether the bar shows its panels rather than its text.</summary>
    public bool ShowPanels
    {
        get => _showPanels;
        set
        {
            _showPanels = value;
            PanelsChanged();
        }
    }

    /// <summary>Whether the bar shows a sizing grip in its corner.</summary>
    public bool SizingGrip
    {
        get => _sizingGrip;
        set
        {
            _sizingGrip = value;
            PanelsChanged();
        }
    }

    /// <summary>Whether the panels show their tool tips.</summary>
    public bool ShowToolTips { get; set; }

    /// <inheritdoc />
    protected override Size DefaultSize => new(100, 22);

    /// <inheritdoc />
    protected override Padding DefaultMargin => Padding.Empty;

    /// <summary>Raised when a panel is clicked.</summary>
    public event StatusBarPanelClickEventHandler? PanelClick
    {
        add => Events.AddHandler(EventPanelClick, value);
        remove => Events.RemoveHandler(EventPanelClick, value);
    }

    /// <summary>Raised to draw a panel whose <see cref="StatusBarPanel.Style"/> is owner draw.</summary>
    public event StatusBarDrawItemEventHandler? DrawItem
    {
        add => Events.AddHandler(EventDrawItem, value);
        remove => Events.RemoveHandler(EventDrawItem, value);
    }

    /// <summary>The bounds of each panel, as the bar lays them out now.</summary>
    internal IReadOnlyList<Rectangle> PanelBounds
    {
        get
        {
            var bounds = new Rectangle[Panels.Count];
            var x = 0;
            for (var i = 0; i < Panels.Count; i++)
            {
                bounds[i] = new Rectangle(x, 2, Panels[i].Width, Math.Max(0, Height - 2));
                x += Panels[i].Width + 2;
            }

            return bounds;
        }
    }

    /// <inheritdoc />
    public override string ToString() => base.ToString() + ", Panels.Count: " + Panels.Count;

    /// <summary>Raises <see cref="PanelClick"/>.</summary>
    /// <param name="e">The panel and the click.</param>
    protected virtual void OnPanelClick(StatusBarPanelClickEventArgs e) => ((StatusBarPanelClickEventHandler?)Events[EventPanelClick])?.Invoke(this, e);

    /// <summary>Raises <see cref="DrawItem"/>.</summary>
    /// <param name="sbdievent">The panel to draw.</param>
    protected virtual void OnDrawItem(StatusBarDrawItemEventArgs sbdievent) => ((StatusBarDrawItemEventHandler?)Events[EventDrawItem])?.Invoke(this, sbdievent);

    /// <inheritdoc />
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (!_showPanels)
        {
            return;
        }

        var bounds = PanelBounds;
        for (var i = 0; i < bounds.Count; i++)
        {
            if (e.X >= bounds[i].Left && e.X < bounds[i].Right)
            {
                OnPanelClick(new StatusBarPanelClickEventArgs(Panels[i], e.Button, e.Clicks, e.X, e.Y));
                return;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        LayoutPanels();
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutPanels();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        PanelsChanged();
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        if (!IsBackColorSet)
        {
            using var background = new SolidBrush(Theme.StripBackground);
            g.FillRectangle(background, ClientRectangle);
        }

        var color = Enabled ? ForeColor : Theme.DisabledText;
        if (!_showPanels)
        {
            ThemePainter.DrawText(g, Text, Font, color, new Rectangle(4, 0, Math.Max(0, Width - 8), Height), ContentAlignment.MiddleLeft, ellipsis: true, mnemonics: false);
        }
        else
        {
            var bounds = PanelBounds;
            for (var i = 0; i < bounds.Count; i++)
            {
                var panel = Panels[i];
                if (panel.BorderStyle != StatusBarPanelBorderStyle.None)
                {
                    ThemePainter.DrawBorder(g, bounds[i], panel.BorderStyle == StatusBarPanelBorderStyle.Raised ? BorderStyle.FixedSingle : BorderStyle.Fixed3D);
                }

                if (panel.Style == StatusBarPanelStyle.OwnerDraw)
                {
                    OnDrawItem(new StatusBarDrawItemEventArgs(g, Font, bounds[i], i, DrawItemState.None, panel));
                    continue;
                }

                var alignment = panel.Alignment switch
                {
                    HorizontalAlignment.Center => ContentAlignment.MiddleCenter,
                    HorizontalAlignment.Right => ContentAlignment.MiddleRight,
                    _ => ContentAlignment.MiddleLeft,
                };
                ThemePainter.DrawText(g, panel.Text, Font, color, Rectangle.Inflate(bounds[i], -4, 0), alignment, ellipsis: true, mnemonics: false);
            }
        }

        if (_sizingGrip && FindForm() is { FormBorderStyle: FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow, WindowState: not FormWindowState.Maximized })
        {
            using var dots = new SolidBrush(Theme.ScrollArrow);
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column <= row; column++)
                {
                    g.FillRectangle(dots, Width - 5 - (column * 4), Height - 5 - ((2 - row) * 4), 2, 2);
                }
            }
        }
    }

    /// <summary>Lays the panels out again and redraws the bar after a panel or the bar changed.</summary>
    internal void PanelsChanged()
    {
        LayoutPanels();
        Invalidate();
    }

    /// <summary>
    /// Sizes the panels as the .NET Framework does: panels that fit their contents take their text's width (at least
    /// their minimum), and the panels that spring share what is left, never less than their minimum.
    /// </summary>
    private void LayoutPanels()
    {
        if (Panels.Count == 0)
        {
            return;
        }

        var available = Width - (_sizingGrip ? 16 : 0) - (2 * (Panels.Count - 1));
        var springs = new List<StatusBarPanel>();
        foreach (StatusBarPanel panel in Panels)
        {
            if (panel.AutoSize == StatusBarPanelAutoSize.Contents)
            {
                panel.SetWidthQuietly(panel.ContentsWidth);
            }

            if (panel.AutoSize == StatusBarPanelAutoSize.Spring)
            {
                springs.Add(panel);
            }
            else
            {
                available -= panel.Width;
            }
        }

        for (var i = 0; i < springs.Count; i++)
        {
            var share = Math.Max(0, available) / (springs.Count - i);
            springs[i].SetWidthQuietly(share);
            available -= springs[i].Width;
        }
    }
}

/// <summary>The panels of a <see cref="StatusBar"/>.</summary>
public class StatusBarPanelCollection : IList
{
    private readonly StatusBar _owner;
    private readonly List<StatusBarPanel> _panels = [];

    /// <summary>Creates the collection of a bar.</summary>
    /// <param name="owner">The bar.</param>
    public StatusBarPanelCollection(StatusBar owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>How many panels there are.</summary>
    public int Count => _panels.Count;

    /// <summary>Whether the collection is read-only; it is not.</summary>
    public bool IsReadOnly => false;

    bool IList.IsFixedSize => false;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    /// <summary>A panel by index.</summary>
    /// <param name="index">The index.</param>
    public virtual StatusBarPanel this[int index]
    {
        get => _panels[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _panels[index].Parent = null;
            Adopt(value);
            _panels[index] = value;
            _owner.PanelsChanged();
        }
    }

    /// <summary>A panel by name, or <see langword="null"/>.</summary>
    /// <param name="key">The panel's name.</param>
    public virtual StatusBarPanel? this[string key] => _panels.Find(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));

    object? IList.this[int index]
    {
        get => this[index];
        set => this[index] = (StatusBarPanel)value!;
    }

    /// <summary>Adds a panel.</summary>
    /// <param name="value">The panel.</param>
    /// <returns>Its index.</returns>
    public virtual int Add(StatusBarPanel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Adopt(value);
        _panels.Add(value);
        _owner.PanelsChanged();
        return _panels.Count - 1;
    }

    /// <summary>Adds a panel with a text.</summary>
    /// <param name="text">The panel's text.</param>
    /// <returns>The new panel.</returns>
    public virtual StatusBarPanel Add(string text)
    {
        var panel = new StatusBarPanel { Text = text };
        Add(panel);
        return panel;
    }

    /// <summary>Adds panels, as the designer does.</summary>
    /// <param name="panels">The panels.</param>
    public virtual void AddRange(StatusBarPanel[] panels)
    {
        ArgumentNullException.ThrowIfNull(panels);
        foreach (var panel in panels)
        {
            Add(panel);
        }
    }

    /// <summary>Inserts a panel.</summary>
    /// <param name="index">Where.</param>
    /// <param name="value">The panel.</param>
    public virtual void Insert(int index, StatusBarPanel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Adopt(value);
        _panels.Insert(index, value);
        _owner.PanelsChanged();
    }

    /// <summary>Removes a panel.</summary>
    /// <param name="value">The panel.</param>
    public virtual void Remove(StatusBarPanel value)
    {
        if (value is not null && _panels.Remove(value))
        {
            value.Parent = null;
            _owner.PanelsChanged();
        }
    }

    /// <summary>Removes the panel at an index.</summary>
    /// <param name="index">The index.</param>
    public virtual void RemoveAt(int index) => Remove(_panels[index]);

    /// <summary>Removes the panel with a name.</summary>
    /// <param name="key">The name.</param>
    public virtual void RemoveByKey(string key)
    {
        if (this[key] is { } panel)
        {
            Remove(panel);
        }
    }

    /// <summary>Removes every panel.</summary>
    public virtual void Clear()
    {
        foreach (var panel in _panels)
        {
            panel.Parent = null;
        }

        _panels.Clear();
        _owner.PanelsChanged();
    }

    /// <summary>Whether a panel is in the collection.</summary>
    /// <param name="panel">The panel.</param>
    public bool Contains(StatusBarPanel panel) => _panels.Contains(panel);

    /// <summary>Whether a panel with a name is in the collection.</summary>
    /// <param name="key">The name.</param>
    public virtual bool ContainsKey(string key) => this[key] is not null;

    /// <summary>The index of a panel, or -1.</summary>
    /// <param name="panel">The panel.</param>
    public int IndexOf(StatusBarPanel panel) => _panels.IndexOf(panel);

    /// <summary>The index of the panel with a name, or -1.</summary>
    /// <param name="key">The name.</param>
    public virtual int IndexOfKey(string key) => _panels.FindIndex(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public IEnumerator GetEnumerator() => _panels.GetEnumerator();

    int IList.Add(object? value) => Add((StatusBarPanel)value!);

    bool IList.Contains(object? value) => value is StatusBarPanel panel && Contains(panel);

    int IList.IndexOf(object? value) => value is StatusBarPanel panel ? IndexOf(panel) : -1;

    void IList.Insert(int index, object? value) => Insert(index, (StatusBarPanel)value!);

    void IList.Remove(object? value)
    {
        if (value is StatusBarPanel panel)
        {
            Remove(panel);
        }
    }

    void ICollection.CopyTo(Array array, int index) => ((ICollection)_panels).CopyTo(array, index);

    private void Adopt(StatusBarPanel panel)
    {
        panel.Parent?.Panels.Remove(panel);
        panel.Parent = _owner;
    }
}
