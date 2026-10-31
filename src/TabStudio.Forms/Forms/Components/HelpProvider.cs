using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace System.Windows.Forms;

/// <summary>
/// The help of controls: a help string, keyword and navigator per control, and the help file they are in. Windows'
/// help viewer does not exist here, so the help is kept for the code that reads it, and F1 shows nothing.
/// </summary>
[ProvideProperty("HelpString", typeof(Control))]
[ProvideProperty("HelpKeyword", typeof(Control))]
[ProvideProperty("HelpNavigator", typeof(Control))]
[ProvideProperty("ShowHelp", typeof(Control))]
public class HelpProvider : Component, IExtenderProvider
{
    private static readonly ConditionalWeakTable<Control, ControlHelp> HelpByControl = new();

    /// <summary>Creates a help provider.</summary>
    public HelpProvider()
    {
    }

    /// <summary>The help file, such as a <c>.chm</c> file, or a web page, that the help keywords are in.</summary>
    [DefaultValue(null)]
    public virtual string? HelpNamespace { get; set; }

    /// <summary>Data the app keeps with the provider.</summary>
    [DefaultValue(null)]
    public object? Tag { get; set; }

    /// <inheritdoc />
    public virtual bool CanExtend(object target) => target is Control;

    /// <summary>The help string of a control.</summary>
    /// <param name="ctl">The control.</param>
    public virtual string? GetHelpString(Control ctl) => Of(ctl).Text;

    /// <summary>Sets the help string of a control.</summary>
    /// <param name="ctl">The control.</param>
    /// <param name="helpString">The help string.</param>
    public virtual void SetHelpString(Control ctl, string? helpString)
    {
        Of(ctl).Text = helpString;
        Of(ctl).Show ??= helpString is not null;
    }

    /// <summary>The help keyword of a control.</summary>
    /// <param name="ctl">The control.</param>
    public virtual string? GetHelpKeyword(Control ctl) => Of(ctl).Keyword;

    /// <summary>Sets the help keyword of a control.</summary>
    /// <param name="ctl">The control.</param>
    /// <param name="keyword">The keyword.</param>
    public virtual void SetHelpKeyword(Control ctl, string? keyword)
    {
        Of(ctl).Keyword = keyword;
        Of(ctl).Show ??= keyword is not null;
    }

    /// <summary>How a control's help keyword is looked up in the help file.</summary>
    /// <param name="ctl">The control.</param>
    public virtual HelpNavigator GetHelpNavigator(Control ctl) => Of(ctl).Navigator;

    /// <summary>Sets how a control's help keyword is looked up in the help file.</summary>
    /// <param name="ctl">The control.</param>
    /// <param name="navigator">The navigator.</param>
    public virtual void SetHelpNavigator(Control ctl, HelpNavigator navigator) => Of(ctl).Navigator = navigator;

    /// <summary>Whether a control has help to show.</summary>
    /// <param name="ctl">The control.</param>
    public virtual bool GetShowHelp(Control ctl) => Of(ctl).Show ?? false;

    /// <summary>Sets whether a control has help to show.</summary>
    /// <param name="ctl">The control.</param>
    /// <param name="value">Whether it does.</param>
    public virtual void SetShowHelp(Control ctl, bool value) => Of(ctl).Show = value;

    /// <summary>Takes a control's help away.</summary>
    /// <param name="ctl">The control.</param>
    public virtual void ResetShowHelp(Control ctl) => Of(ctl).Show = null;

    /// <inheritdoc />
    public override string ToString() => base.ToString() + ", HelpNamespace: " + HelpNamespace;

    private static ControlHelp Of(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return HelpByControl.GetOrCreateValue(control);
    }

    private sealed class ControlHelp
    {
        public string? Text { get; set; }

        public string? Keyword { get; set; }

        public HelpNavigator Navigator { get; set; } = HelpNavigator.AssociateIndex;

        public bool? Show { get; set; }
    }
}
