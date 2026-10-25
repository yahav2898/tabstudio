using TabStudio.Toolchains.Terminals;

namespace TabStudio.Toolchains.Tests.Terminals;

/// <summary>What keys send to programs, as xterm sends them.</summary>
public sealed class TerminalKeysTests
{
    [Theory]
    [InlineData(TerminalKey.Up, TerminalModifiers.None, false, "\u001b[A")]
    [InlineData(TerminalKey.Up, TerminalModifiers.None, true, "\u001bOA")]
    [InlineData(TerminalKey.Left, TerminalModifiers.Control, false, "\u001b[1;5D")]
    [InlineData(TerminalKey.Right, TerminalModifiers.Shift | TerminalModifiers.Alt, true, "\u001b[1;4C")]
    [InlineData(TerminalKey.Home, TerminalModifiers.None, false, "\u001b[H")]
    [InlineData(TerminalKey.End, TerminalModifiers.None, true, "\u001bOF")]
    [InlineData(TerminalKey.Delete, TerminalModifiers.None, false, "\u001b[3~")]
    [InlineData(TerminalKey.PageUp, TerminalModifiers.Shift, false, "\u001b[5;2~")]
    [InlineData(TerminalKey.F1, TerminalModifiers.None, false, "\u001bOP")]
    [InlineData(TerminalKey.F5, TerminalModifiers.None, false, "\u001b[15~")]
    [InlineData(TerminalKey.F12, TerminalModifiers.Control, false, "\u001b[24;5~")]
    [InlineData(TerminalKey.Enter, TerminalModifiers.None, false, "\r")]
    [InlineData(TerminalKey.Backspace, TerminalModifiers.None, false, "\u007f")]
    [InlineData(TerminalKey.Tab, TerminalModifiers.Shift, false, "\u001b[Z")]
    [InlineData(TerminalKey.Escape, TerminalModifiers.None, false, "\u001b")]
    public void Keys_send_xterm_sequences(TerminalKey key, TerminalModifiers modifiers, bool application, string expected)
    {
        Assert.Equal(expected, TerminalKeys.Encode(key, modifiers, application));
    }

    [Theory]
    [InlineData("c", TerminalModifiers.Control, "\u0003")]
    [InlineData("D", TerminalModifiers.Control, "\u0004")]
    [InlineData("[", TerminalModifiers.Control, "\u001b")]
    [InlineData(" ", TerminalModifiers.Control, "\0")]
    [InlineData("b", TerminalModifiers.Alt, "\u001bb")]
    [InlineData("ls -la\n", TerminalModifiers.None, "ls -la\r")]
    [InlineData("é", TerminalModifiers.None, "é")]
    public void Typed_text_becomes_what_a_keyboard_sends(string text, TerminalModifiers modifiers, string expected)
    {
        Assert.Equal(expected, TerminalKeys.EncodeText(text, modifiers));
    }

    [Fact]
    public void Pasted_text_is_bracketed_when_the_shell_asks()
    {
        Assert.Equal("echo 1\recho 2", TerminalKeys.EncodePaste("echo 1\r\necho 2", bracketed: false));
        Assert.Equal("\u001b[200~a\rb\u001b[201~", TerminalKeys.EncodePaste("a\nb\u001b[201~", bracketed: true));
    }
}
