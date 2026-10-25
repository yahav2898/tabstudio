using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TabStudio.App.Controls;

/// <summary>
/// A text box that takes the keyboard focus when it appears, for boxes that open because the user asked to write
/// in them, such as a review comment. It looks and works like any other text box.
/// </summary>
public sealed class AutoFocusTextBox : TextBox
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(FocusIfShown, DispatcherPriority.Background);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible)
        {
            Dispatcher.UIThread.Post(FocusIfShown, DispatcherPriority.Background);
        }
    }

    private void FocusIfShown()
    {
        if (IsEffectivelyVisible && this.GetVisualRoot() is not null)
        {
            Focus();
        }
    }
}
