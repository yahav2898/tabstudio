using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>The device's clipboard: text an app copies here can be pasted in other apps, and back.</summary>
public static class Clipboard
{
    private static string? _fallback;

    /// <summary>Puts text on the clipboard.</summary>
    /// <param name="text">The text.</param>
    public static void SetText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        _fallback = text;
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
        _fallback = null;
        if (FormsRuntime.Current is { } runtime)
        {
            _ = runtime.Presenter.SetClipboardTextAsync(string.Empty);
        }
    }

    /// <summary>Images on the clipboard are not supported on this device.</summary>
    /// <param name="image">The image.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public static void SetImage(System.Drawing.Image image) =>
        throw new NotSupportedException("Clipboard.SetImage is not supported by TabStudio's Forms layer: only text can be copied.");

    /// <summary>Puts text on the clipboard, in a format: every format is stored as plain text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="format">The format.</param>
    public static void SetText(string text, TextDataFormat format) => SetText(text);

    /// <summary>The text on the clipboard, in a format: every format reads the plain text.</summary>
    /// <param name="format">The format.</param>
    public static string GetText(TextDataFormat format) => GetText();

    /// <summary>Whether the clipboard holds text in a format.</summary>
    /// <param name="format">The format.</param>
    public static bool ContainsText(TextDataFormat format) => ContainsText();

    /// <summary>Puts data on the clipboard: its text, which is the format other apps can paste.</summary>
    /// <param name="data">The data: a string or a data object with text.</param>
    /// <exception cref="NotSupportedException">The data has no text.</exception>
    public static void SetDataObject(object data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var text = data switch
        {
            string value => value,
            IDataObject dataObject => dataObject.GetData(DataFormats.Text) as string,
            _ => null,
        };

        if (text is null)
        {
            throw new NotSupportedException("Clipboard.SetDataObject in TabStudio's Forms layer takes text only: a string or a DataObject with text.");
        }

        SetText(text);
    }

    /// <summary>Puts data on the clipboard: its text.</summary>
    /// <param name="data">The data.</param>
    /// <param name="copy">Whether the data stays after the app ends; it always does.</param>
    public static void SetDataObject(object data, bool copy) => SetDataObject(data);

    /// <summary>The clipboard's data: its text.</summary>
    public static IDataObject? GetDataObject() => ContainsText() ? new DataObject(DataFormats.Text, GetText()) : null;
}
