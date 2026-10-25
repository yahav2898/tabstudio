using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace CustomerEntry.Porting;

/// <summary>
/// The app's screen on phones and tablets, which have no windows: the main window fills it, and the windows the
/// app opens show over it as dialogs, at their own size if the screen has room.
/// </summary>
internal sealed class ScreenHost : Panel
{
    private readonly Dictionary<WindowView, Control> _overlays = [];

    /// <summary>Shows the main window as the screen.</summary>
    /// <param name="main">The main window.</param>
    public ScreenHost(WindowView main)
    {
        Current = this;
        this[!BackgroundProperty] = new DynamicResourceExtension("SystemControlBackgroundAltHighBrush");
        main.ClearValue(WidthProperty);
        main.ClearValue(HeightProperty);
        main.AttachTo(this);
        Children.Add(main);
    }

    /// <summary>The screen the app shows, once it has one.</summary>
    public static ScreenHost? Current { get; private set; }

    /// <summary>Shows a window over the screen.</summary>
    /// <param name="window">The window.</param>
    /// <param name="modal">Whether the rest of the screen waits until it closes, as for a dialog.</param>
    public void Open(WindowView window, bool modal)
    {
        var title = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(16, 12, 16, 8),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        title.Bind(TextBlock.TextProperty, window.GetObservable(WindowView.TitleProperty));

        // The window's size is the card's; the screen caps it.
        var card = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BoxShadow = BoxShadows.Parse("0 8 24 0 #40000000"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = window.Width,
            Height = window.SizeToContent is SizeToContent.Height or SizeToContent.WidthAndHeight ? double.NaN : window.Height,
            MinWidth = Math.Min(window.MinWidth, 280),
            Child = new DockPanel { Children = { title, window } },
        };
        card[!Border.BackgroundProperty] = new DynamicResourceExtension("SystemControlBackgroundAltHighBrush");
        card[!Border.BorderBrushProperty] = new DynamicResourceExtension("SystemControlForegroundBaseLowBrush");
        DockPanel.SetDock(title, Dock.Top);
        window.ClearValue(WidthProperty);
        window.ClearValue(HeightProperty);
        window.AttachTo(this);

        var overlay = new Panel
        {
            Background = modal ? new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)) : null,
            Children = { card },
        };
        FitToScreen(card);
        _overlays[window] = overlay;
        Children.Add(overlay);
    }

    /// <summary>Removes a window that closed.</summary>
    /// <param name="window">The window.</param>
    public void Remove(WindowView window)
    {
        if (_overlays.Remove(window, out var overlay))
        {
            Children.Remove(overlay);
            if (overlay is Panel { Children: [Border { Child: DockPanel content }] })
            {
                content.Children.Remove(window);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        foreach (var overlay in _overlays.Values)
        {
            if (overlay is Panel { Children: [Border card] })
            {
                FitToScreen(card);
            }
        }
    }

    private void FitToScreen(Border card)
    {
        var size = Bounds.Size;
        card.MaxWidth = size.Width > 0 ? Math.Max(size.Width - 32, 200) : double.PositiveInfinity;
        card.MaxHeight = size.Height > 0 ? Math.Max(size.Height - 32, 160) : double.PositiveInfinity;
    }
}
