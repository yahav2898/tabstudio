using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using TabStudio.Forms.Tests.Infrastructure;

namespace TabStudio.Forms.Tests.Runtime;

/// <summary>
/// What paint programs and games from tutorials do: draw on a bitmap with the mouse, show it in a picture box, draw
/// through a buffer, copy the picture, save it, and the smaller things they reach for.
/// </summary>
public sealed class DrawingAppTests
{
    [AvaloniaFact]
    public void A_paint_app_draws_mouse_strokes_into_its_bitmap_shows_them_and_saves_them()
    {
        PictureBox canvas = null!;
        Bitmap bitmap = null!;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form { ClientSize = new Size(260, 200), Text = "Paint" };
            canvas = new PictureBox { Location = new Point(10, 10), Size = new Size(240, 180), Name = "canvas" };
            form.Controls.Add(canvas);
            form.Load += (_, _) =>
            {
                // As SimplePaint's Load does: a bitmap the size of the picture box, cleared to white.
                bitmap = new Bitmap(canvas.Width, canvas.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.White);
                }

                canvas.Image = bitmap;
            };
            Point? last = null;
            canvas.MouseDown += (_, e) => last = e.Location;
            canvas.MouseMove += (_, e) =>
            {
                if (last is { } from && e.Button == MouseButtons.Left)
                {
                    using var g = Graphics.FromImage(bitmap);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using var pen = new Pen(Color.Red, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLine(pen, from, e.Location);
                    last = e.Location;
                    canvas.Invalidate();
                }
            };
            canvas.MouseUp += (_, _) => last = null;
            return form;
        });

        run.Drag(canvas, new Point(20, 30), new Point(200, 30), new Point(200, 150));

        var (onLine, corner, untouched) = run.Invoke(() => (bitmap.GetPixel(110, 30), bitmap.GetPixel(200, 100), bitmap.GetPixel(100, 100)));
        AssertRed(onLine);
        AssertRed(corner);
        Assert.Equal(Color.White.ToArgb(), untouched.ToArgb());

        // The picture box shows the strokes once it was invalidated.
        var form = run.Invoke(() => canvas.FindForm()!);
        var shown = Screenshots.Save(run.FrameOf(form), "drawing-app-strokes");
        AssertRed(shown.At(10 + 110, 10 + 30));
        AssertRed(shown.At(10 + 200, 10 + 100));
        Assert.Equal(Color.White.ToArgb(), shown.At(10 + 100, 10 + 100).ToArgb());

        // Saved as a PNG and read back, the strokes are there.
        var path = Path.Combine(Path.GetTempPath(), $"tabstudio-paint-{Guid.NewGuid():N}.png");
        try
        {
            run.Invoke(() => bitmap.Save(path, ImageFormat.Png));
            using var saved = new Bitmap(path);
            Assert.Equal(new Size(240, 180), saved.Size);
            AssertRed(saved.GetPixel(110, 30));
            Assert.Equal(Color.White.ToArgb(), saved.GetPixel(100, 100).ToArgb());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void A_form_that_paints_through_BufferedGraphics_shows_the_buffer()
    {
        Form form = null!;
        using var run = FormsTestRun.Start(() =>
        {
            form = new DoubleBufferedForm { ClientSize = new Size(200, 120) };
            return form;
        });

        var shown = Screenshots.Save(run.FrameOf(form), "drawing-app-buffered");
        Assert.Equal(Color.Navy.ToArgb(), shown.At(20, 20).ToArgb());
        Assert.Equal(Color.Gold.ToArgb(), shown.At(100, 60).ToArgb());
    }

    [AvaloniaFact]
    public void A_picture_copied_to_the_clipboard_comes_back_with_its_pixels()
    {
        using var run = FormsTestRun.Start(() => new Form());
        var (size, pixel, contains) = run.Invoke(() =>
        {
            using var picture = new Bitmap(30, 20);
            using (var g = Graphics.FromImage(picture))
            {
                g.Clear(Color.Lime);
                g.FillRectangle(Brushes.Blue, 10, 5, 10, 10);
            }

            Clipboard.SetImage(picture);
            using var pasted = (Bitmap)Clipboard.GetImage()!;
            return (pasted.Size, pasted.GetPixel(15, 10), Clipboard.ContainsImage());
        });

        Assert.NotNull(run.Presenter.ClipboardImage);
        Assert.True(contains);
        Assert.Equal(new Size(30, 20), size);
        Assert.Equal(Color.Blue.ToArgb(), pixel.ToArgb());
    }

    [AvaloniaFact]
    public void GraphicsPath_AddString_adds_the_outlines_of_the_text()
    {
        using var run = FormsTestRun.Start(() => new Form());
        var (bounds, points, inked) = run.Invoke(() =>
        {
            using var path = new GraphicsPath();
            path.AddString("Hi", FontFamily.GenericSansSerif, (int)FontStyle.Bold, 48, new Point(4, 4), StringFormat.GenericDefault);
            using var bitmap = new Bitmap(120, 80);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                g.FillPath(Brushes.Black, path);
            }

            var count = 0;
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    count += bitmap.GetPixel(x, y).R < 128 ? 1 : 0;
                }
            }

            return (path.GetBounds(), path.PointCount, count);
        });

        Assert.True(points > 10, $"Only {points} points.");
        Assert.InRange(bounds.Width, 25, 110);
        Assert.InRange(bounds.Height, 25, 70);
        Assert.True(inked > 200, $"Only {inked} pixels are inked.");
    }

    [AvaloniaFact]
    public void A_RichTextBox_reads_and_writes_RTF()
    {
        using var run = FormsTestRun.Start(() => new Form());
        var path = Path.Combine(Path.GetTempPath(), $"tabstudio-rtf-{Guid.NewGuid():N}.rtf");
        try
        {
            var (text, roundTrip, plain) = run.Invoke(() =>
            {
                using var box = new RichTextBox();
                box.Rtf = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\f0\fs20 Hello \b bold\b0\par Caf\'e9 荤?\par}";
                var read = box.Text;
                box.SaveFile(path);
                using var other = new RichTextBox();
                other.LoadFile(path);
                box.Rtf = "not rtf";
                return (read, other.Text, box.Text);
            });

            Assert.Equal("Hello bold\nCafé €", text);
            Assert.Equal(text, roundTrip);
            Assert.Equal("not rtf", plain);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void A_message_filter_sees_and_can_swallow_mouse_clicks()
    {
        Button button = null!;
        var clicks = 0;
        var filter = new SwallowClicks();
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form { ClientSize = new Size(200, 100) };
            button = new Button { Location = new Point(10, 10), Size = new Size(80, 30), Text = "Go" };
            button.Click += (_, _) => clicks++;
            form.Controls.Add(button);
            Application.AddMessageFilter(filter);
            return form;
        });

        run.Tap(button);
        Assert.Equal(0, run.Invoke(() => clicks));
        Assert.True(filter.Seen > 0);

        run.Invoke(() => Application.RemoveMessageFilter(filter));
        run.Tap(button);
        Assert.Equal(1, run.Invoke(() => clicks));
    }

    [AvaloniaFact]
    public void Application_Restart_ends_the_run_and_asks_to_run_again()
    {
        Button button = null!;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form();
            button = new Button { Text = "Restart", Size = new Size(80, 30) };
            button.Click += (_, _) => Application.Restart();
            form.Controls.Add(button);
            return form;
        });

        run.Tap(button);

        Assert.True(run.Exit.Wait(TimeSpan.FromSeconds(10)));
        Assert.Null(run.Exit.Result.Error);
        Assert.True(run.Exit.Result.Restart);
    }

    private static void AssertRed(Color color) =>
        Assert.True(color.R > 200 && color.G < 80 && color.B < 80, $"Expected red, found {color}.");

    private sealed class DoubleBufferedForm : Form
    {
        public DoubleBufferedForm()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var context = BufferedGraphicsManager.Current;
            using var buffer = context.Allocate(e.Graphics, ClientRectangle);
            buffer.Graphics.Clear(Color.Navy);
            buffer.Graphics.FillEllipse(Brushes.Gold, 60, 20, 80, 80);
            buffer.Render(e.Graphics);
            base.OnPaint(e);
        }
    }

    private sealed class SwallowClicks : IMessageFilter
    {
        public int Seen { get; private set; }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg is 0x0201 or 0x0202)
            {
                Seen++;
                return true;
            }

            return false;
        }
    }
}
