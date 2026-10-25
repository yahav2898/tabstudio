using System.Runtime.InteropServices;
using TabStudio.WindowsBridge.Windows.Native;

namespace TabStudio.WindowsBridge.Windows.Desktop;

/// <summary>The PC's clipboard text, through Win32's clipboard, which another program may hold open for a moment.</summary>
internal static unsafe class WindowsClipboard
{
    /// <summary>The clipboard's text, or <see langword="null"/> when it holds none.</summary>
    public static string? GetText()
    {
        if (!Open())
        {
            return null;
        }

        try
        {
            var memory = NativeMethods.GetClipboardData(NativeMethods.CfUnicodeText);
            if (memory == 0)
            {
                return null;
            }

            var text = NativeMethods.GlobalLock(memory);
            if (text is null)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni((nint)text);
            }
            finally
            {
                _ = NativeMethods.GlobalUnlock(memory);
            }
        }
        finally
        {
            _ = NativeMethods.CloseClipboard();
        }
    }

    /// <summary>Puts text on the clipboard.</summary>
    /// <param name="text">The text.</param>
    /// <exception cref="InvalidOperationException">Another program holds the clipboard.</exception>
    public static void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!Open())
        {
            throw new InvalidOperationException("Another program on the PC holds the clipboard. Try again in a moment.");
        }

        try
        {
            _ = NativeMethods.EmptyClipboard();
            var bytes = (nuint)((text.Length + 1) * sizeof(char));
            var memory = NativeMethods.GlobalAlloc(NativeMethods.GmemMoveable, bytes);
            if (memory == 0)
            {
                throw new InvalidOperationException("Windows had no memory for the clipboard's text.");
            }

            var target = (char*)NativeMethods.GlobalLock(memory);
            fixed (char* source = text)
            {
                Buffer.MemoryCopy(source, target, (long)bytes, text.Length * sizeof(char));
            }

            target[text.Length] = '\0';
            _ = NativeMethods.GlobalUnlock(memory);
            if (NativeMethods.SetClipboardData(NativeMethods.CfUnicodeText, memory) == 0)
            {
                // The clipboard did not take the memory, so it is still ours to free.
                _ = NativeMethods.GlobalFree(memory);
            }
        }
        finally
        {
            _ = NativeMethods.CloseClipboard();
        }
    }

    private static bool Open()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (NativeMethods.OpenClipboard(0))
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return false;
    }
}
