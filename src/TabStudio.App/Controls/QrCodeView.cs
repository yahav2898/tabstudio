using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TabStudio.App.Theming;
using TabStudio.Link.Qr;

namespace TabStudio.App.Controls;

/// <summary>
/// Draws a QR code as large as it fits: black modules on white with the standard quiet zone, on whole pixels so
/// cameras read it from across a desk.
/// </summary>
public sealed class QrCodeView : Control
{
    /// <summary>The quiet zone around the code, in modules, as the standard asks.</summary>
    private const int QuietZone = 4;

    /// <summary>The code to draw.</summary>
    public static readonly StyledProperty<QrCode?> CodeProperty = AvaloniaProperty.Register<QrCodeView, QrCode?>(nameof(Code));

    static QrCodeView()
    {
        AffectsRender<QrCodeView>(CodeProperty);
    }

    /// <summary>The code to draw, or <see langword="null"/> for nothing.</summary>
    public QrCode? Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Code is not { } code)
        {
            return;
        }

        var modules = code.Size + (2 * QuietZone);
        var side = Math.Min(Bounds.Width, Bounds.Height);
        var module = Math.Max(1, Math.Floor(side / modules));
        var size = module * modules;
        var origin = new Point(Math.Round((Bounds.Width - size) / 2), Math.Round((Bounds.Height - size) / 2));
        context.DrawRectangle(ThemeBrushes.Get("Brush.QrCode.Light"), null, new Rect(origin, new Size(size, size)), 6, 6);

        var geometry = new StreamGeometry();
        using (var figures = geometry.Open())
        {
            for (var y = 0; y < code.Size; y++)
            {
                for (var x = 0; x < code.Size; x++)
                {
                    if (!code[x, y])
                    {
                        continue;
                    }

                    var left = origin.X + ((x + QuietZone) * module);
                    var top = origin.Y + ((y + QuietZone) * module);
                    figures.BeginFigure(new Point(left, top), isFilled: true);
                    figures.LineTo(new Point(left + module, top));
                    figures.LineTo(new Point(left + module, top + module));
                    figures.LineTo(new Point(left, top + module));
                    figures.EndFigure(isClosed: true);
                }
            }
        }

        context.DrawGeometry(ThemeBrushes.Get("Brush.QrCode.Dark"), null, geometry);
    }
}
