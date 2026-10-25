using Avalonia.Controls;
using Avalonia.Input;

namespace TabStudio.App.Views.Notebooks;

/// <summary>
/// The text of a cell output: selectable with a mouse, while a finger on it scrolls the notebook instead of
/// selecting. Long-press or right-click offers Copy.
/// </summary>
public sealed class OutputText : SelectableTextBlock
{
    /// <summary>Creates the text.</summary>
    public OutputText()
    {
        var copy = new MenuItem { Header = "Copy" };
        copy.Click += (_, _) => _ = CopyAsync();
        ContextFlyout = new MenuFlyout { Items = { copy } };
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        // Selecting with a finger would capture it, and a long output could then not be scrolled past.
        if (e.Pointer.Type != PointerType.Touch)
        {
            base.OnPointerPressed(e);
        }
    }

    private async Task CopyAsync()
    {
        var selected = SelectedText;
        var text = string.IsNullOrEmpty(selected) ? Text : selected;
        if (!string.IsNullOrEmpty(text) && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
