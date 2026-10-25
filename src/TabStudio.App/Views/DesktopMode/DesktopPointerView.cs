using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TabStudio.App.Platform;

namespace TabStudio.App.Views.DesktopMode;

/// <summary>
/// The arrow of the mouse pointer TabStudio draws on the external display, at the <see cref="DesktopCursor"/>'s
/// position. It lies above everything the display shows, menus and tooltips too, and never takes a click.
/// </summary>
internal sealed class DesktopPointerView : Control
{
    /// <summary>A classic arrow, its tip at the origin, in device-independent units.</summary>
    private static readonly Geometry Arrow = Geometry.Parse("M0.5,0.5 L0.5,17 L4.6,13.1 L7.3,19.4 L9.9,18.3 L7.3,12.2 L12.8,12.2 Z");

    private static readonly ImmutablePen Outline = new(new ImmutableSolidColorBrush(Colors.Black), 1, lineJoin: PenLineJoin.Miter);

    private DesktopCursor? _cursor;

    /// <summary>Creates the arrow, hidden until the cursor shows.</summary>
    public DesktopPointerView()
    {
        IsHitTestVisible = false;
        IsVisible = false;
        ZIndex = int.MaxValue;
        Width = 14;
        Height = 21;
    }

    /// <summary>The pointer this draws.</summary>
    public DesktopCursor? MousePointer
    {
        get => _cursor;
        set
        {
            if (_cursor is not null)
            {
                _cursor.PropertyChanged -= OnCursorChanged;
            }

            _cursor = value;
            if (_cursor is not null)
            {
                _cursor.PropertyChanged += OnCursorChanged;
            }

            Follow();
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context) => context.DrawGeometry(Brushes.White, Outline, Arrow);

    private void OnCursorChanged(object? sender, PropertyChangedEventArgs e) => Follow();

    private void Follow()
    {
        IsVisible = _cursor?.IsVisible == true;
        if (_cursor is { } cursor)
        {
            Canvas.SetLeft(this, cursor.Position.X);
            Canvas.SetTop(this, cursor.Position.Y);
        }
    }
}
