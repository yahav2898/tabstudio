using System.Drawing;
using System.Drawing.Imaging;
using Avalonia.Headless.XUnit;

namespace TabStudio.Forms.Tests.Drawing;

/// <summary>
/// DualBrain's Solitaire passes its form's Icon.Handle to Windows' ShellAbout; Icon had no Handle, so the app did not
/// build (BC30456).
/// </summary>
public sealed class IconHandleTests
{
    [AvaloniaFact]
    public void An_icon_has_a_handle_that_names_it()
    {
        using var stream = new MemoryStream();
        using (var bitmap = new Bitmap(16, 16))
        {
            bitmap.Save(stream, ImageFormat.Png);
        }

        stream.Position = 0;
        using var icon = new Icon(stream);
        using var other = new Icon(icon, 16, 16);

        Assert.NotEqual(IntPtr.Zero, icon.Handle);
        Assert.Equal(icon.Handle, icon.Handle);
        Assert.NotEqual(icon.Handle, other.Handle);
        Assert.Same(icon, Icon.FromHandle(icon.Handle));
        Assert.Throws<ArgumentException>(() => Icon.FromHandle(new IntPtr(1)));
    }
}
