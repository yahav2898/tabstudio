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

    /// <summary>Data objects on the clipboard are not supported on this device.</summary>
    /// <param name="data">The data.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public static void SetDataObject(object data) =>
        throw new NotSupportedException("Clipboard.SetDataObject is not supported by TabStudio's Forms layer: use Clipboard.SetText.");
}
