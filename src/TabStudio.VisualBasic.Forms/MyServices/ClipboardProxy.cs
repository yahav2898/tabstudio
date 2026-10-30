using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Windows.Forms;

namespace Microsoft.VisualBasic.MyServices;

/// <summary>
/// The clipboard (<c>My.Computer.Clipboard</c>), on TabStudio's Forms layer: text and images are shared with the
/// device's other apps; audio, file lists and other data stay in the app, as they do between the parts of one app.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class ClipboardProxy
{
    private const string WaveAudio = "WaveAudio";
    private const string FileDrop = "FileDrop";

    internal ClipboardProxy()
    {
    }

    /// <summary>Empties the clipboard.</summary>
    public void Clear() => Clipboard.Clear();

    /// <summary>Whether the clipboard holds audio the app put there.</summary>
    public bool ContainsAudio() => Clipboard.ContainsData(WaveAudio);

    /// <summary>Whether the clipboard holds data of a format.</summary>
    /// <param name="format">The format.</param>
    public bool ContainsData(string format) => Clipboard.ContainsData(format);

    /// <summary>Whether the clipboard holds a list of files the app put there.</summary>
    public bool ContainsFileDropList() => Clipboard.ContainsData(FileDrop);

    /// <summary>Whether the clipboard holds an image.</summary>
    public bool ContainsImage() => Clipboard.ContainsImage();

    /// <summary>Whether the clipboard holds text.</summary>
    public bool ContainsText() => Clipboard.ContainsText();

    /// <summary>Whether the clipboard holds text; every format is text on the device.</summary>
    /// <param name="format">The format.</param>
    public bool ContainsText(TextDataFormat format) => Clipboard.ContainsText(format);

    /// <summary>The audio on the clipboard, or <see langword="null"/>.</summary>
    public Stream? GetAudioStream() => Clipboard.GetData(WaveAudio) as Stream;

    /// <summary>The data of a format on the clipboard, or <see langword="null"/>.</summary>
    /// <param name="format">The format.</param>
    [Obsolete("`ClipboardProxy.GetData(As String)` method is obsolete. Use `ClipboardProxy.TryGetData(Of T)(As String, As T)` instead.", DiagnosticId = "WFDEV005", UrlFormat = "https://aka.ms/winforms-warnings/{0}")]
    public object? GetData(string format) => Clipboard.GetData(format);

    /// <summary>Everything the clipboard holds: text, an image and the app's own data.</summary>
    public IDataObject? GetDataObject() => Clipboard.GetDataObject();

    /// <summary>The list of files on the clipboard, empty when there is none.</summary>
    public StringCollection GetFileDropList()
    {
        var files = new StringCollection();
        if (Clipboard.GetData(FileDrop) is string[] paths)
        {
            files.AddRange(paths);
        }

        return files;
    }

    /// <summary>The image on the clipboard, copied by this app or another, or <see langword="null"/>.</summary>
    public Image? GetImage() => Clipboard.GetImage();

    /// <summary>The text on the clipboard, or an empty string.</summary>
    public string GetText() => Clipboard.GetText();

    /// <summary>The text on the clipboard, or an empty string; every format is text on the device.</summary>
    /// <param name="format">The format.</param>
    public string GetText(TextDataFormat format) => Clipboard.GetText(format);

    /// <summary>Puts audio on the clipboard, for the app itself.</summary>
    /// <param name="audioBytes">The audio, as a .wav file's bytes.</param>
    public void SetAudio(byte[] audioBytes)
    {
        ArgumentNullException.ThrowIfNull(audioBytes);
        Clipboard.SetData(WaveAudio, new MemoryStream(audioBytes, writable: false));
    }

    /// <summary>Puts audio on the clipboard, for the app itself.</summary>
    /// <param name="audioStream">The audio, as a .wav stream.</param>
    public void SetAudio(Stream audioStream)
    {
        ArgumentNullException.ThrowIfNull(audioStream);
        Clipboard.SetData(WaveAudio, audioStream);
    }

    /// <summary>Puts data of a format on the clipboard.</summary>
    /// <param name="format">The format.</param>
    /// <param name="data">The data.</param>
    public void SetData(string format, object data) => Clipboard.SetData(format, data);

    /// <summary>Puts data on the clipboard as JSON, which <see cref="TryGetData{T}(string, out T)"/> reads back.</summary>
    /// <typeparam name="T">The data's type.</typeparam>
    /// <param name="format">The format.</param>
    /// <param name="data">The data.</param>
    public void SetDataAsJson<T>(string format, T data)
    {
        ArgumentException.ThrowIfNullOrEmpty(format);
        Clipboard.SetData(format, new JsonData(typeof(T), JsonSerializer.Serialize(data)));
    }

    /// <summary>Puts data in several formats on the clipboard.</summary>
    /// <param name="data">The data.</param>
    public void SetDataObject(DataObject data) => Clipboard.SetDataObject(data);

    /// <summary>Puts a list of files on the clipboard, for the app itself.</summary>
    /// <param name="filePaths">The files' paths.</param>
    /// <exception cref="ArgumentException">The list is empty.</exception>
    public void SetFileDropList(StringCollection filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        if (filePaths.Count == 0)
        {
            throw new ArgumentException("The file list is empty.", nameof(filePaths));
        }

        var paths = new string[filePaths.Count];
        filePaths.CopyTo(paths, 0);
        Clipboard.SetData(FileDrop, paths);
    }

    /// <summary>Puts an image on the clipboard, as a PNG the device's other apps can paste.</summary>
    /// <param name="image">The image.</param>
    public void SetImage(Image image) => Clipboard.SetImage(image);

    /// <summary>Puts text on the clipboard, shared with the device's other apps.</summary>
    /// <param name="text">The text.</param>
    public void SetText(string text) => Clipboard.SetText(text);

    /// <summary>Puts text on the clipboard; every format is text on the device.</summary>
    /// <param name="text">The text.</param>
    /// <param name="format">The format.</param>
    public void SetText(string text, TextDataFormat format) => Clipboard.SetText(text, format);

    /// <summary>Reads data of a format from the clipboard, if it is of a type.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="format">The format.</param>
    /// <param name="data">The data, when the clipboard holds data of the type in the format.</param>
    /// <returns>Whether it did.</returns>
    public bool TryGetData<T>(string format, [MaybeNullWhen(false)] out T data) => TryGetData(format, resolver: null, out data);

    /// <summary>Reads data of a format from the clipboard, if it is of a type.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="format">The format.</param>
    /// <param name="resolver">Resolves type names; the data never leaves the app on the tablet, so it is not needed.</param>
    /// <param name="data">The data, when the clipboard holds data of the type in the format.</param>
    /// <returns>Whether it did.</returns>
    public bool TryGetData<T>(string format, Func<TypeName, Type>? resolver, [MaybeNullWhen(false)] out T data)
    {
        ArgumentException.ThrowIfNullOrEmpty(format);
        switch (Clipboard.GetData(format))
        {
            case T value:
                data = value;
                return true;
            case JsonData json when json.Type == typeof(T) || typeof(T).IsAssignableFrom(json.Type):
                if (JsonSerializer.Deserialize<T>(json.Json) is { } deserialized)
                {
                    data = deserialized;
                    return true;
                }

                break;
        }

        data = default;
        return false;
    }

    /// <summary>Data <see cref="SetDataAsJson{T}"/> put on the clipboard, as JSON with the type it came from.</summary>
    private sealed record JsonData(Type Type, string Json);
}
