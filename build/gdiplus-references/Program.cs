using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using TabStudio.Forms.Tests.Drawing;

// Draws each GDI+ scene on a white 32bppArgb bitmap and saves it as a PNG.
var output = args.Length > 0 ? args[0] : "references";
Directory.CreateDirectory(output);
foreach (var scene in DrawingScenes.All.Where(scene => scene.FromGdiPlus))
{
    using var bitmap = new Bitmap(scene.Width, scene.Height, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bitmap))
    {
        g.Clear(Color.White);
        scene.Draw(g);
    }

    bitmap.Save(Path.Combine(output, scene.Name + ".png"), ImageFormat.Png);
    Console.WriteLine($"{scene.Name}.png");
}

// GDI+'s hatch patterns, as rows of bits, the highest bit leftmost, for TabStudio's HatchPatterns table.
var text = new StringBuilder();
for (var style = 0; style <= 52; style++)
{
    using var bitmap = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bitmap))
    using (var brush = new HatchBrush((HatchStyle)style, Color.Black, Color.White))
    {
        g.FillRectangle(brush, 0, 0, 8, 8);
    }

    ulong rows = 0;
    for (var y = 0; y < 8; y++)
    {
        for (var x = 0; x < 8; x++)
        {
            if (bitmap.GetPixel(x, y).R < 128)
            {
                rows |= 1UL << (((7 - y) * 8) + (7 - x));
            }
        }
    }

    text.AppendLine($"        0x{rows:X16}, // {(HatchStyle)style}");
}

File.WriteAllText(Path.Combine(output, "hatch-patterns.txt"), text.ToString());
Console.WriteLine("hatch-patterns.txt");
