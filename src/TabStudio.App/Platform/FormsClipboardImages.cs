using TabStudio.Forms.Runtime;

namespace TabStudio.App.Platform;

/// <summary>Hands the device's image clipboard to the Windows Forms layer.</summary>
/// <param name="clipboard">The device's image clipboard.</param>
internal sealed class FormsClipboardImages(IImageClipboard clipboard) : IClipboardImages
{
    /// <inheritdoc />
    public Task SetImageAsync(byte[] png) => clipboard.SetImageAsync(png);

    /// <inheritdoc />
    public Task<byte[]?> GetImageAsync() => clipboard.GetImageAsync();
}
