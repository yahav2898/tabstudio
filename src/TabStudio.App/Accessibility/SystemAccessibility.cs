namespace TabStudio.App.Accessibility;

/// <summary>
/// The device's accessibility settings, as the platform reports them: Android's font scale, whether animations are
/// off (Settings &gt; Accessibility &gt; Remove animations, or the developer options' animation scales at off), and the
/// layout direction of the device's language. Desktop hosts and tests keep the defaults, or set them.
/// </summary>
/// <remarks>The platform calls <see cref="Update"/> on the UI thread, at start and whenever a setting changes.</remarks>
public sealed class SystemAccessibility
{
    /// <summary>The one instance the platform updates and the IDE reads.</summary>
    public static SystemAccessibility Current { get; } = new();

    /// <summary>The system's font scale: 1 is Android's default font size, 1.3 its Large.</summary>
    public double FontScale { get; private set; } = 1;

    /// <summary>Whether the system asks apps not to animate.</summary>
    public bool ReduceMotion { get; private set; }

    /// <summary>Whether the system's language is written right to left, so layouts mirror.</summary>
    public bool IsRightToLeft { get; private set; }

    /// <summary>Raised after a setting changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Takes the settings the platform reports; raises <see cref="Changed"/> when one differs.</summary>
    /// <param name="fontScale">The system's font scale.</param>
    /// <param name="reduceMotion">Whether animations are off.</param>
    /// <param name="isRightToLeft">Whether layouts mirror.</param>
    public void Update(double fontScale, bool reduceMotion, bool isRightToLeft)
    {
        if (!double.IsFinite(fontScale) || fontScale <= 0)
        {
            fontScale = 1;
        }

        if (Math.Abs(fontScale - FontScale) < 0.001 && reduceMotion == ReduceMotion && isRightToLeft == IsRightToLeft)
        {
            return;
        }

        FontScale = fontScale;
        ReduceMotion = reduceMotion;
        IsRightToLeft = isRightToLeft;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
