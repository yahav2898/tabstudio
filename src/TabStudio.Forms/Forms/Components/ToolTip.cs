using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace System.Windows.Forms;

/// <summary>Short help shown when the pointer rests on a control.</summary>
[ProvideProperty("ToolTip", typeof(Control))]
public class ToolTip : Component, IExtenderProvider
{
    private static readonly ConditionalWeakTable<Control, ToolTipText> Texts = new();
    private int _automaticDelay = 500;

    /// <summary>Creates a tooltip provider.</summary>
    public ToolTip()
    {
    }

    /// <summary>Creates a tooltip provider in a container, as the designer does.</summary>
    /// <param name="cont">The container.</param>
    public ToolTip(IContainer cont)
        : this()
    {
        ArgumentNullException.ThrowIfNull(cont);
        cont.Add(this);
    }

    /// <summary>Whether tooltips are shown.</summary>
    [DefaultValue(true)]
    public bool Active { get; set; } = true;

    /// <summary>The delay that sets the other delays.</summary>
    [DefaultValue(500)]
    public int AutomaticDelay
    {
        get => _automaticDelay;
        set
        {
            _automaticDelay = value;
            InitialDelay = value;
            AutoPopDelay = value * 10;
            ReshowDelay = value / 5;
        }
    }

    /// <summary>How long the pointer rests before a tooltip shows, in milliseconds.</summary>
    public int InitialDelay { get; set; } = 500;

    /// <summary>How long a tooltip stays, in milliseconds.</summary>
    public int AutoPopDelay { get; set; } = 5000;

    /// <summary>How long before another control's tooltip shows, in milliseconds.</summary>
    public int ReshowDelay { get; set; } = 100;

    /// <summary>Whether tooltips show when the form is not active.</summary>
    public bool ShowAlways { get; set; }

    /// <summary>Whether tooltips are drawn as balloons. Kept for compatibility.</summary>
    public bool IsBalloon { get; set; }

    /// <summary>A title over the text. Kept for compatibility.</summary>
    public string ToolTipTitle { get; set; } = string.Empty;

    /// <summary>An icon beside the title. Kept for compatibility.</summary>
    public ToolTipIcon ToolTipIcon { get; set; }

    /// <summary>Whether tooltips animate. Kept for compatibility.</summary>
    public bool UseAnimation { get; set; } = true;

    /// <summary>Whether tooltips fade. Kept for compatibility.</summary>
    public bool UseFading { get; set; } = true;

    /// <summary>Whether ampersands are removed. Kept for compatibility.</summary>
    public bool StripAmpersands { get; set; }

    /// <summary>The background. Kept for compatibility.</summary>
    public Color BackColor { get; set; } = SystemColors.Info;

    /// <summary>The text color. Kept for compatibility.</summary>
    public Color ForeColor { get; set; } = SystemColors.InfoText;

    /// <summary>Data about the provider.</summary>
    public object? Tag { get; set; }

    /// <summary>Whether a control can have a tooltip: every control can.</summary>
    /// <param name="target">The control.</param>
    public bool CanExtend(object target) => target is Control;

    /// <summary>Sets a control's tooltip.</summary>
    /// <param name="control">The control.</param>
    /// <param name="caption">The tooltip, or <see langword="null"/> to remove it.</param>
    public void SetToolTip(Control control, string? caption)
    {
        ArgumentNullException.ThrowIfNull(control);
        Texts.Remove(control);
        if (!string.IsNullOrEmpty(caption))
        {
            Texts.Add(control, new ToolTipText(this, caption));
        }
    }

    /// <summary>A control's tooltip.</summary>
    /// <param name="control">The control.</param>
    public string GetToolTip(Control? control) =>
        control is not null && Texts.TryGetValue(control, out var text) && text.Owner == this ? text.Text : string.Empty;

    /// <summary>Removes every tooltip of this provider.</summary>
    public void RemoveAll()
    {
        foreach (var (control, text) in Texts.ToList())
        {
            if (text.Owner == this)
            {
                Texts.Remove(control);
            }
        }
    }

    /// <summary>Shows a tooltip over a control now; it shows when the pointer next rests there.</summary>
    /// <param name="text">The text.</param>
    /// <param name="window">The control.</param>
    public void Show(string? text, IWin32Window window)
    {
        if (window is Control control)
        {
            SetToolTip(control, text);
        }
    }

    /// <summary>Shows a tooltip over a control.</summary>
    /// <param name="text">The text.</param>
    /// <param name="window">The control.</param>
    /// <param name="duration">How long, in milliseconds.</param>
    public void Show(string? text, IWin32Window window, int duration) => Show(text, window);

    /// <summary>Shows a tooltip over a control.</summary>
    /// <param name="text">The text.</param>
    /// <param name="window">The control.</param>
    /// <param name="x">The x, ignored.</param>
    /// <param name="y">The y, ignored.</param>
    public void Show(string? text, IWin32Window window, int x, int y) => Show(text, window);

    /// <summary>Hides the tooltip of a control.</summary>
    /// <param name="win">The control.</param>
    public void Hide(IWin32Window win)
    {
        // The tooltip hides when the pointer leaves the control.
    }

    /// <summary>The tooltip of a control, from any provider.</summary>
    /// <param name="control">The control.</param>
    internal static (string Text, int Delay, bool Active)? Find(Control control) =>
        Texts.TryGetValue(control, out var text) ? (text.Text, text.Owner.InitialDelay, text.Owner.Active) : null;

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            RemoveAll();
        }

        base.Dispose(disposing);
    }

    private sealed record ToolTipText(ToolTip Owner, string Text);
}

/// <summary>The icon of a tooltip's title.</summary>
public enum ToolTipIcon
{
    /// <summary>No icon.</summary>
    None = 0,

    /// <summary>An information icon.</summary>
    Info = 1,

    /// <summary>A warning icon.</summary>
    Warning = 2,

    /// <summary>An error icon.</summary>
    Error = 3,
}
