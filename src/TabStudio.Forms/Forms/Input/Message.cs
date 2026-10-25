namespace System.Windows.Forms;

/// <summary>
/// A window message, as <see cref="Control.ProcessCmdKey"/> receives one for a key. There is no Windows message
/// loop on this device; a message describes the key event the Forms layer is handling.
/// </summary>
public struct Message : IEquatable<Message>
{
    /// <summary>The message number of a key press.</summary>
    internal const int KeyDownMessage = 0x0100;

    /// <summary>The window the message is for.</summary>
    public IntPtr HWnd { readonly get; set; }

    /// <summary>The message number.</summary>
    public int Msg { readonly get; set; }

    /// <summary>The first parameter: for a key, its key code.</summary>
    public IntPtr WParam { readonly get; set; }

    /// <summary>The second parameter.</summary>
    public IntPtr LParam { readonly get; set; }

    /// <summary>The result the handler returns.</summary>
    public IntPtr Result { readonly get; set; }

    /// <summary>Creates a message.</summary>
    /// <param name="hWnd">The window.</param>
    /// <param name="msg">The message number.</param>
    /// <param name="wparam">The first parameter.</param>
    /// <param name="lparam">The second parameter.</param>
    public static Message Create(IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam) =>
        new() { HWnd = hWnd, Msg = msg, WParam = wparam, LParam = lparam };

    /// <summary>Whether two messages are the same.</summary>
    /// <param name="a">The first message.</param>
    /// <param name="b">The second message.</param>
    public static bool operator ==(Message a, Message b) => a.Equals(b);

    /// <summary>Whether two messages differ.</summary>
    /// <param name="a">The first message.</param>
    /// <param name="b">The second message.</param>
    public static bool operator !=(Message a, Message b) => !a.Equals(b);

    /// <inheritdoc />
    public readonly bool Equals(Message other) =>
        HWnd == other.HWnd && Msg == other.Msg && WParam == other.WParam && LParam == other.LParam && Result == other.Result;

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is Message other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(HWnd, Msg, WParam, LParam);

    /// <inheritdoc />
    public override readonly string ToString() => $"msg=0x{Msg:x} hwnd=0x{HWnd:x} wparam=0x{WParam:x} lparam=0x{LParam:x}";
}
