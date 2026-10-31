using Avalonia;
using Avalonia.Controls;

namespace TabStudio.App.Accessibility;

/// <summary>
/// Attached properties for accessibility (docs/design/README.md, "Accessibility").
/// </summary>
public static class A11y
{
    /// <summary>
    /// Marks a control whose content is the user's, not the IDE's: a running app in the preview, a web page, a form in
    /// the designer, a virtual device's screen. The IDE's text size, touch targets and names do not reach into it, and
    /// the accessibility checks leave it out: it is the app's own, checked by the layout checks (SWRM001 to SWRM005).
    /// </summary>
    public static readonly AttachedProperty<bool> IsContentProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsContent", typeof(A11y));

    /// <summary>Whether a control holds the user's content.</summary>
    public static bool GetIsContent(Control control) => control.GetValue(IsContentProperty);

    /// <summary>Marks a control as holding the user's content.</summary>
    public static void SetIsContent(Control control, bool value) => control.SetValue(IsContentProperty, value);
}
