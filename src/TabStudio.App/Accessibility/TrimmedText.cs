using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TabStudio.App.Accessibility;

/// <summary>
/// Text that trims with an ellipsis shows in full in a tooltip (docs/design/README.md, "Type"): a path, a test's name
/// or a branch that does not fit, and at a larger text size more of them, are a long press or a hover away. A view
/// need not add the tooltip; one it sets, on the text or on the control around it, is kept.
/// </summary>
public static class TrimmedText
{
    /// <summary>How far up a tooltip of the control around the text is looked for.</summary>
    private const int Depth = 4;

    private static readonly AttachedProperty<bool> IsTippedHereProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, bool>("IsTippedHere", typeof(TrimmedText));

    private static bool _started;

    /// <summary>Starts giving trimmed text its tooltip. Call once, on the UI thread, as the app starts.</summary>
    public static void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        Control.LoadedEvent.AddClassHandler<TextBlock>((text, _) =>
        {
            TrimInSelectionBox(text);
            Update(text);
        });
        TextBlock.TextProperty.Changed.AddClassHandler<TextBlock>((text, _) => Update(text));
        TextBlock.TextTrimmingProperty.Changed.AddClassHandler<TextBlock>((text, _) => Update(text));
    }

    /// <summary>Gives a text block that may trim a tooltip with its whole text, unless it or a control around it has one.</summary>
    /// <param name="text">The text block.</param>
    public static void Update(TextBlock text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var ours = text.GetValue(IsTippedHereProperty);
        if (text.TextTrimming == TextTrimming.None || string.IsNullOrWhiteSpace(text.Text) || IsInContent(text))
        {
            if (ours)
            {
                text.ClearValue(ToolTip.TipProperty);
                text.SetValue(IsTippedHereProperty, false);
            }

            return;
        }

        if (!ours && (ToolTip.GetTip(text) is not null || HasTipAround(text)))
        {
            return;
        }

        ToolTip.SetTip(text, text.Text);
        text.SetValue(IsTippedHereProperty, true);
    }

    /// <summary>
    /// A combo box shows its choice in a box only as wide as its column; at a large text size a long choice trims
    /// rather than running past the box's edge, and its whole text is in the tooltip.
    /// </summary>
    private static void TrimInSelectionBox(TextBlock text)
    {
        if (text.TextTrimming == TextTrimming.None && text.TextWrapping == TextWrapping.NoWrap
            && text.GetVisualParent() is ContentPresenter presenter
            && presenter.GetVisualParent() is ContentControl { Name: "ContentPresenter", TemplatedParent: ComboBox } && !IsInContent(text))
        {
            text.SetCurrentValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        }
    }

    /// <summary>Whether the text is the user's app's, which is left as the app made it.</summary>
    private static bool IsInContent(TextBlock text) =>
        text.GetSelfAndVisualAncestors().Any(v => v is Control c && A11y.GetIsContent(c));

    private static bool HasTipAround(Visual text)
    {
        var depth = 0;
        foreach (var ancestor in text.GetVisualAncestors())
        {
            if (++depth > Depth)
            {
                return false;
            }

            if (ancestor is Control control && ToolTip.GetTip(control) is not null)
            {
                return true;
            }
        }

        return false;
    }
}
