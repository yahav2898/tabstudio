using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Compilation.WindowsForms;

namespace TabStudio.Forms.Tests.Resources;

/// <summary>
/// .resx files written by the Windows Forms designer on Windows, with an image list in each of three color depths, a
/// picture, an icon and a location, compiled as TabStudio compiles a Windows Forms project's resources and read
/// back through a <see cref="ResourceManager"/>, as the designer's code reads them.
/// </summary>
public sealed class ResxResourceTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Resources", "Windows");

    [AvaloniaTheory]
    [InlineData("Depth32Bit", "Depth32Bit-image")]
    [InlineData("Depth24Bit", "image")]
    [InlineData("Depth8Bit", "Depth8Bit-image")]
    public void An_image_list_from_a_resx_file_has_the_images_Windows_Forms_has(string depth, string expected)
    {
        var resources = Load($"imagelist-{depth}.resx");
        using var list = new ImageList();
        list.ImageStream = (ImageListStreamer)resources.GetObject("imageList1.ImageStream", CultureInfo.InvariantCulture)!;

        Assert.Equal(new Size(16, 16), list.ImageSize);
        Assert.Equal(5, list.Images.Count);
        for (var i = 0; i < 5; i++)
        {
            using var reference = new Bitmap(Path.Combine(Folder, $"{expected}{i}.png"));
            var image = (Bitmap)list.Images[i];
            for (var y = 0; y < 16; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var actual = image.GetPixel(x, y);
                    var want = reference.GetPixel(x, y);
                    Assert.True(
                        (actual.A == 0 && want.A == 0) || actual.ToArgb() == want.ToArgb(),
                        $"{depth} image {i} at ({x}, {y}): {actual}, Windows has {want}.");
                }
            }
        }
    }

    [AvaloniaFact]
    public void Pictures_icons_points_and_strings_come_back_as_their_types()
    {
        var resources = Load("imagelist-Depth32Bit.resx");

        var picture = Assert.IsType<Bitmap>(resources.GetObject("pictureBox1.Image", CultureInfo.InvariantCulture));
        Assert.Equal(new Size(16, 16), picture.Size);
        Assert.Equal(Color.Lime.ToArgb(), picture.GetPixel(8, 8).ToArgb());
        Assert.IsType<Icon>(resources.GetObject("$this.Icon", CultureInfo.InvariantCulture));
        Assert.Equal(new Point(12, 34), resources.GetObject("label1.Location", CultureInfo.InvariantCulture));
        Assert.Equal("Hello", resources.GetString("label1.Text", CultureInfo.InvariantCulture));
    }

    [AvaloniaFact]
    public void An_image_list_copies_its_images_to_another_through_ImageStream()
    {
        using var first = new ImageList { ImageSize = new Size(8, 8) };
        first.Images.Add(new Bitmap(8, 8));
        using var second = new ImageList { ImageStream = first.ImageStream };
        Assert.Single(second.Images);
        Assert.Equal(new Size(8, 8), second.ImageSize);
    }

    /// <summary>Compiles a .resx file into an assembly, as TabStudio builds a project, and opens its resources.</summary>
    private static ResourceManager Load(string file)
    {
        var compiled = WindowsFormsResources.TryCompile(Path.Combine(Folder, file))
            ?? throw new InvalidOperationException($"{file} compiled as strings only.");
        var name = "ResxTest" + Guid.NewGuid().ToString("N");
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText("namespace ResxTest; public sealed class Form1 { }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();
        var result = compilation.Emit(output, manifestResources: [new ResourceDescription("ResxTest.Form1.resources", () => compiled, isPublic: true)]);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        var assembly = Assembly.Load(output.ToArray());
        return new ResourceManager("ResxTest.Form1", assembly);
    }
}
