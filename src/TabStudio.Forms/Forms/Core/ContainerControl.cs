using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// A control that contains other controls and keeps track of the active one, moving focus with Tab and the arrow
/// keys, and scaling its children when it was designed at another resolution: the base of forms and user controls.
/// </summary>
public class ContainerControl : ScrollableControl, IContainerControl
{
    private Control? _activeControl;
    private SizeF _autoScaleDimensions;
    private AutoScaleMode _autoScaleMode = AutoScaleMode.Inherit;

    /// <summary>Creates a container.</summary>
    public ContainerControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint, false);
        SetStyle(ControlStyles.ContainerControl, true);
    }

    /// <summary>The control with focus in the container.</summary>
    public Control? ActiveControl
    {
        get => _activeControl;
        set
        {
            if (value is not null && !Contains(value))
            {
                throw new ArgumentException("Invisible or disabled control cannot be activated.", nameof(value));
            }

            _activeControl = value;
            if (value is not null && Window is not null)
            {
                value.Focus();
            }
        }
    }

    /// <summary>The average character size of the font, or the resolution, the container was designed at.</summary>
    public SizeF AutoScaleDimensions
    {
        get => _autoScaleDimensions;
        set => _autoScaleDimensions = value;
    }

    /// <summary>How the container scales when shown at another font size or resolution than it was designed at.</summary>
    public AutoScaleMode AutoScaleMode
    {
        get => _autoScaleMode;
        set => _autoScaleMode = value;
    }

    /// <summary>The average character size or resolution here, which <see cref="AutoScaleDimensions"/> is compared with.</summary>
    public SizeF CurrentAutoScaleDimensions => AutoScaling.CurrentDimensions(EffectiveAutoScaleMode, Font);

    /// <summary>The factor the container scales by.</summary>
    public SizeF AutoScaleFactor => AutoScaling.Factor(_autoScaleDimensions, EffectiveAutoScaleMode, Font);

    /// <summary>How the container validates its controls when focus moves.</summary>
    public virtual AutoValidate AutoValidate { get; set; } = AutoValidate.Inherit;

    /// <summary>The form the container is on.</summary>
    public Form? ParentForm => Parent?.FindForm();

    private AutoScaleMode EffectiveAutoScaleMode =>
        _autoScaleMode == AutoScaleMode.Inherit ? (Parent?.GetContainerControl() as ContainerControl)?.EffectiveAutoScaleMode ?? AutoScaleMode.None : _autoScaleMode;

    /// <summary>Makes a control the active control and gives it focus.</summary>
    /// <param name="active">The control.</param>
    /// <returns>Whether it became active.</returns>
    public bool ActivateControl(Control active)
    {
        ArgumentNullException.ThrowIfNull(active);
        ActiveControl = active;
        return ActiveControl == active;
    }

    /// <summary>Scales the container and its children from the size they were designed at to this device's.</summary>
    public void PerformAutoScale()
    {
        var factor = AutoScaleFactor;
        if (factor.Width != 1f || factor.Height != 1f)
        {
            Scale(factor);
        }

        _autoScaleDimensions = CurrentAutoScaleDimensions;
    }

    /// <summary>Validates the active control.</summary>
    /// <returns>Whether it is valid.</returns>
    public bool Validate() => Validate(false);

    /// <summary>Validates the active control.</summary>
    /// <param name="checkAutoValidate">Whether to honor <see cref="AutoValidate"/>.</param>
    /// <returns>Whether it is valid.</returns>
    public bool Validate(bool checkAutoValidate) =>
        (checkAutoValidate && AutoValidate == AutoValidate.Disable) || _activeControl is null || _activeControl.DispatchValidation();

    /// <summary>Validates every child control.</summary>
    /// <returns>Whether all are valid.</returns>
    public virtual bool ValidateChildren() => ValidateChildren(ValidationConstraints.Selectable);

    /// <summary>Validates the child controls that meet constraints.</summary>
    /// <param name="validationConstraints">Which children to validate.</param>
    /// <returns>Whether all are valid.</returns>
    public virtual bool ValidateChildren(ValidationConstraints validationConstraints)
    {
        var valid = true;
        foreach (var child in Descendants(this))
        {
            if (Meets(child, validationConstraints) && child.CausesValidation && !child.DispatchValidation())
            {
                valid = false;
            }
        }

        return valid;
    }

    /// <summary>Called when a control in the container gets focus; records it as the active control.</summary>
    /// <param name="control">The control.</param>
    internal void SetActiveControlFromWindow(Control control)
    {
        for (Control? container = control.Parent; container is not null; container = container.Parent)
        {
            if (container is ContainerControl containerControl)
            {
                containerControl._activeControl = control;
            }
        }
    }

    /// <summary>Moves focus with Tab and Shift+Tab through the tab order, and with the arrow keys among siblings.</summary>
    /// <param name="keyData">The key.</param>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if ((keyData & (Keys.Alt | Keys.Control)) == 0)
        {
            var code = keyData & Keys.KeyCode;
            if (code == Keys.Tab && this is Form)
            {
                var forward = (keyData & Keys.Shift) == 0;
                if (SelectNextControl(Window?.FocusedControl, forward, tabStopOnly: true, nested: true, wrap: true))
                {
                    return true;
                }
            }
            else if (code is Keys.Left or Keys.Right or Keys.Up or Keys.Down && Window?.FocusedControl is { Parent: { } parent } focused)
            {
                var forward = code is Keys.Right or Keys.Down;
                if (parent.SelectNextControl(focused, forward, tabStopOnly: false, nested: false, wrap: true))
                {
                    return true;
                }
            }
        }

        return base.ProcessDialogKey(keyData);
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Controls.ToArray())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool Meets(Control control, ValidationConstraints constraints) =>
        ((constraints & ValidationConstraints.Selectable) == 0 || control.CanSelect)
        && ((constraints & ValidationConstraints.Enabled) == 0 || control.Enabled)
        && ((constraints & ValidationConstraints.Visible) == 0 || control.Visible)
        && ((constraints & ValidationConstraints.TabStop) == 0 || control.TabStop);
}

/// <summary>Which child controls <see cref="ContainerControl.ValidateChildren(ValidationConstraints)"/> validates.</summary>
[Flags]
public enum ValidationConstraints
{
    /// <summary>Every child.</summary>
    None = 0,

    /// <summary>Children that can be selected.</summary>
    Selectable = 1,

    /// <summary>Enabled children.</summary>
    Enabled = 2,

    /// <summary>Visible children.</summary>
    Visible = 4,

    /// <summary>Children with a tab stop.</summary>
    TabStop = 8,

    /// <summary>Direct children only.</summary>
    ImmediateChildren = 16,
}

/// <summary>
/// Automatic scaling: the factor between the resolution a form was designed at, which the designer records in
/// AutoScaleDimensions, and the 96 DPI the app preview lays forms out at.
/// </summary>
internal static class AutoScaling
{
    /// <summary>The resolutions Windows scales to, in dots per inch: 100%, 125%, 150% and so on.</summary>
    private static readonly int[] StandardDpis = [96, 120, 144, 168, 192, 240, 288];

    /// <summary>The average character size of Segoe UI 9 points at 96 DPI, as the designer records it.</summary>
    private static readonly SizeF DefaultFontDimensions = new(7f, 15f);

    /// <summary>The dimensions here, for a mode and font.</summary>
    public static SizeF CurrentDimensions(AutoScaleMode mode, Font font) => mode switch
    {
        AutoScaleMode.Dpi => new SizeF(96f, 96f),
        AutoScaleMode.Font => new SizeF(DefaultFontDimensions.Width * font.SizeInPoints / 9f, DefaultFontDimensions.Height * font.SizeInPoints / 9f),
        _ => SizeF.Empty,
    };

    /// <summary>
    /// The factor to scale by. The designer's dimensions tell the resolution a form was designed at; it is snapped to
    /// the standard resolutions, so a form designed at 100% is not scaled at all.
    /// </summary>
    public static SizeF Factor(SizeF designed, AutoScaleMode mode, Font font)
    {
        if (mode is AutoScaleMode.None or AutoScaleMode.Inherit || designed.Width <= 0 || designed.Height <= 0)
        {
            return new SizeF(1f, 1f);
        }

        var current = CurrentDimensions(mode, font);
        var designDpi = mode == AutoScaleMode.Dpi ? designed.Height : 96f * designed.Height / current.Height;
        var snapped = StandardDpis.MinBy(dpi => Math.Abs(dpi - designDpi));
        if (Math.Abs(snapped - designDpi) > snapped * 0.08f)
        {
            snapped = (int)Math.Round(designDpi);
        }

        var factor = 96f / snapped;
        return new SizeF(factor, factor);
    }
}
