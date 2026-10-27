namespace TabStudio.App.Platform;

/// <summary>
/// The device's clipboard for images, for platforms whose Avalonia clipboard holds only text and files, as on
/// Android: Windows Forms apps copy and paste images through it (<c>Clipboard.SetImage</c> and <c>GetImage</c>).
/// </summary>
public interface IImageClipboard
{
    /// <summary>Puts an image on the device's clipboard, where other apps can paste it.</summary>
    /// <param name="png">The image as a PNG file.</param>
    Task SetImageAsync(byte[] png);

    /// <summary>The image on the device's clipboard, copied by any app.</summary>
    /// <returns>The image file's bytes, or <see langword="null"/> when the clipboard holds no image.</returns>
    Task<byte[]?> GetImageAsync();
}
