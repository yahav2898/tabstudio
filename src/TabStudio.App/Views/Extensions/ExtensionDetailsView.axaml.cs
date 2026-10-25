using Avalonia;
using Avalonia.Controls;

namespace TabStudio.App.Views.Extensions;

/// <summary>
/// An extension's details page. The "More information" card sits beside the README where there is room, and under it
/// where there is not.
/// </summary>
public sealed partial class ExtensionDetailsView : UserControl
{
    // Below this width the information card moves under the README.
    private const double NarrowWidth = 820;

    /// <summary>Creates the view; its data context is the <see cref="ViewModels.Extensions.ExtensionDetailsViewModel"/>.</summary>
    public ExtensionDetailsView() => InitializeComponent();

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Another extension's details start at the top.
        Scroller.Offset = default;
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var narrow = e.NewSize.Width < NarrowWidth;
        Grid.SetColumn(InfoCard, narrow ? 0 : 1);
        Grid.SetRow(InfoCard, narrow ? 1 : 0);
        InfoCard.Width = narrow ? double.NaN : 270;
        InfoCard.Margin = narrow ? new Thickness(0, 18, 0, 0) : new Thickness(22, 0, 0, 0);
        Page.Margin = narrow ? new Thickness(16, 16, 16, 28) : new Thickness(24, 20, 24, 32);
    }
}
