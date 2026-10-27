using System.Drawing;
using System.Drawing.Imaging;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>
/// The device's clipboard: text and images an app copies here can be pasted in other apps, and back. Data of other
/// formats is kept for the app itself, as it is on Windows between parts of one app.
/// </summary>
public static class Clipboard
{
    private static readonly object Gate = new();
    private static string? _fallback;
    private static byte[]? _imageFallback;
    private static DataObject? _data;

    /// <summary>Puts text on the clipboard.</summary>
    /// <param name="text">The text.</param>
    public static void SetText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        lock (Gate)
        {
            _fallback = text;
            _imageFallback = null;
            _data = null;
        }

        if (FormsRuntime.Current is { } runtime)
        {
            _ = runtime.Presenter.SetClipboardTextAsync(text);
        }
    }

    /// <summary>The text on the clipboard, or an empty string.</summary>
    public static string GetText()
    {
        if (FormsRuntime.Current is { IsOnThread: true } runtime)
        {
            var read = runtime.Presenter.GetClipboardTextAsync();
            runtime.RunLoop(() => read.IsCompleted);
            return read.IsCompletedSuccessfully ? read.Result ?? string.Empty : _fallback ?? string.Empty;
        }

        return _fallback ?? string.Empty;
    }

    /// <summary>Whether the clipboard holds text.</summary>
    public static bool ContainsText() => GetText().Length > 0;

    /// <summary>Empties the clipboard.</summary>
    public static void Clear()
    {
        lock (Gate)
        {
            _fallback = null;
            _imageFallback = null;
            _data = null;
        }

        if (FormsRuntime.Current is { } runtime)
        {
            _ = runtime.Presenter.SetClipboardTextAsync(string.Empty);
        }
    }

    /// <summary>Puts an image on the clipboard, as a PNG other apps can paste.</summary>
    /// <param name="image">The image.</param>
    public static void SetImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        using var png = new MemoryStream();
        image.Save(png, ImageFormat.Png);
        var bytes = png.ToArray();
        lock (Gate)
        {
            _imageFallback = bytes;
            _fallback = null;
            _data = null;
        }

        if (FormsRuntime.Current is { } runtime)
        {
            var write = runtime.Presenter.SetClipboardImageAsync(bytes);
            if (runtime.IsOnThread)
            {
                // Like Windows, the image is on the clipboard when SetImage returns.
                runtime.RunLoop(() => write.IsCompleted);
            }
        }
    }

    /// <summary>The image on the clipboard, copied by this app or another, or <see langword="null"/>.</summary>
    public static Image? GetImage()
    {
        var bytes = ReadImage();
        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch (ArgumentException)
        {
            // Not an image this device can decode.
            return null;
        }
    }

    /// <summary>Whether the clipboard holds an image.</summary>
    public static bool ContainsImage() => ReadImage() is not null;

    /// <summary>Puts text in a format on the clipboard.</summary>
    /// <param name="text">The text.</param>
    /// <param name="format">The format; text is text on this device.</param>
    public static void SetText(string text, TextDataFormat format) => SetText(text);

    /// <summary>The text on the clipboard.</summary>
    /// <param name="format">The format; text is text on this device.</param>
    public static string GetText(TextDataFormat format) => GetText();

    /// <summary>Whether the clipboard holds text.</summary>
    /// <param name="format">The format; text is text on this device.</param>
    public static bool ContainsText(TextDataFormat format) => ContainsText();

    /// <summary>Puts data on the clipboard: text and images go to the device's clipboard, other data stays in the app.</summary>
    /// <param name="data">A string, an image, a <see cref="IDataObject"/> or any object.</param>
    public static void SetDataObject(object data)
    {
        ArgumentNullException.ThrowIfNull(data);
        switch (data)
        {
            case string text:
                SetText(text);
                return;
            case Image image:
                SetImage(image);
                return;
            case IDataObject dataObject when dataObject.GetData(DataFormats.Bitmap) is Image image:
                SetImage(image);
                break;
            case IDataObject dataObject when dataObject.GetData(DataFormats.Text) is string { Length: > 0 } text:
                SetText(text);
                break;
        }

        lock (Gate)
        {
            _data = data as DataObject ?? (data is IDataObject other ? Copy(other) : new DataObject(data));
        }
    }

    /// <summary>Puts data on the clipboard.</summary>
    /// <param name="data">The data.</param>
    /// <param name="copy">Whether the data stays after the app exits: it stays while the app runs.</param>
    public static void SetDataObject(object data, bool copy) => SetDataObject(data);

    /// <summary>Puts data on the clipboard.</summary>
    /// <param name="data">The data.</param>
    /// <param name="copy">Whether the data stays after the app exits.</param>
    /// <param name="retryTimes">Ignored.</param>
    /// <param name="retryDelay">Ignored.</param>
    public static void SetDataObject(object data, bool copy, int retryTimes, int retryDelay) => SetDataObject(data);

    /// <summary>Puts data of a format on the clipboard.</summary>
    /// <param name="format">The format.</param>
    /// <param name="data">The data.</param>
    public static void SetData(string format, object data)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(data);
        SetDataObject(new DataObject(format, data));
    }

    /// <summary>The data of a format on the clipboard, or <see langword="null"/>.</summary>
    /// <param name="format">The format.</param>
    public static object? GetData(string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        lock (Gate)
        {
            if (_data?.GetData(format) is { } data)
            {
                return data;
            }
        }

        return GetDataObject()?.GetData(format);
    }

    /// <summary>Whether the clipboard holds data of a format.</summary>
    /// <param name="format">The format.</param>
    public static bool ContainsData(string format) => GetData(format) is not null;

    /// <summary>What the clipboard holds: text, an image, and the app's own data.</summary>
    public static IDataObject? GetDataObject()
    {
        DataObject result;
        lock (Gate)
        {
            result = _data is { } data ? Copy(data) : new DataObject();
        }

        if (!result.GetDataPresent(DataFormats.Text) && GetText() is { Length: > 0 } text)
        {
            result.SetData(DataFormats.Text, text);
        }

        if (!result.GetDataPresent(DataFormats.Bitmap) && GetImage() is { } image)
        {
            result.SetData(DataFormats.Bitmap, image);
        }

        return result.GetFormats().Length > 0 ? result : null;
    }

    private static byte[]? ReadImage()
    {
        if (FormsRuntime.Current is { IsOnThread: true } runtime)
        {
            var read = runtime.Presenter.GetClipboardImageAsync();
            runtime.RunLoop(() => read.IsCompleted);
            if (read.IsCompletedSuccessfully)
            {
                return read.Result;
            }
        }

        lock (Gate)
        {
            return _imageFallback;
        }
    }

    private static DataObject Copy(IDataObject data)
    {
        var copy = new DataObject();
        foreach (var format in data.GetFormats())
        {
            copy.SetData(format, data.GetData(format));
        }

        return copy;
    }
}
