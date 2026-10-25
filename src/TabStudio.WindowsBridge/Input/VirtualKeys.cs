using System.Collections.Frozen;

namespace TabStudio.WindowsBridge.Input;

/// <summary>
/// The Windows virtual-key codes of the keys a tablet's keyboard sends, by the names Avalonia (and WPF) give them,
/// such as <c>Enter</c>, <c>D7</c> or <c>OemComma</c>. Names that mean the same key, such as <c>Return</c> and
/// <c>Enter</c>, are all known.
/// </summary>
public static class VirtualKeys
{
    /// <summary>Backspace.</summary>
    public const int Back = 0x08;

    /// <summary>Tab.</summary>
    public const int Tab = 0x09;

    /// <summary>Enter.</summary>
    public const int Enter = 0x0D;

    /// <summary>Shift, either one.</summary>
    public const int Shift = 0x10;

    /// <summary>Ctrl, either one.</summary>
    public const int Control = 0x11;

    /// <summary>Alt, either one.</summary>
    public const int Alt = 0x12;

    /// <summary>Escape.</summary>
    public const int Escape = 0x1B;

    /// <summary>The Windows key on the left.</summary>
    public const int LeftWindows = 0x5B;

    private static readonly FrozenDictionary<string, int> Codes = CreateCodes();

    /// <summary>The virtual-key code of a key.</summary>
    /// <param name="keyName">The key's name, as Avalonia's <c>Key</c> enumeration writes it.</param>
    /// <returns>The code, or <see langword="null"/> for a key Windows has no code for.</returns>
    public static int? Find(string keyName)
    {
        ArgumentNullException.ThrowIfNull(keyName);
        return Codes.TryGetValue(keyName, out var code) ? code : null;
    }

    /// <summary>The codes of the modifier keys held, in the order they go down: Ctrl, Alt, Shift, then Windows.</summary>
    /// <param name="modifiers">The modifiers as Avalonia writes them, such as <c>Control, Shift</c>; empty or <c>None</c> for none.</param>
    public static IReadOnlyList<int> Modifiers(string? modifiers)
    {
        var codes = new List<int>();
        if (string.IsNullOrWhiteSpace(modifiers))
        {
            return codes;
        }

        var names = modifiers.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (names.Contains("Control", StringComparer.Ordinal))
        {
            codes.Add(Control);
        }

        if (names.Contains("Alt", StringComparer.Ordinal))
        {
            codes.Add(Alt);
        }

        if (names.Contains("Shift", StringComparer.Ordinal))
        {
            codes.Add(Shift);
        }

        if (names.Contains("Meta", StringComparer.Ordinal))
        {
            codes.Add(LeftWindows);
        }

        return codes;
    }

    /// <summary>Whether a code is a modifier key, which other keys are pressed together with.</summary>
    /// <param name="virtualKey">The code.</param>
    public static bool IsModifier(int virtualKey) =>
        virtualKey is Shift or Control or Alt or LeftWindows or 0x5C or (>= 0xA0 and <= 0xA5);

    /// <summary>
    /// Whether a key is one that changes text or moves in it, such as Enter or the arrows, rather than a character:
    /// such keys are sent as keys even when the tablet's keyboard also sends text for them.
    /// </summary>
    /// <param name="virtualKey">The code.</param>
    public static bool IsNonCharacter(int virtualKey) =>
        virtualKey is Back or Tab or Enter or Escape or 0x2E or (>= 0x21 and <= 0x28) or (>= 0x70 and <= 0x87) or 0x2D;

    private static FrozenDictionary<string, int> CreateCodes()
    {
        var codes = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Cancel"] = 0x03, ["Back"] = Back, ["Tab"] = Tab, ["Clear"] = 0x0C, ["Return"] = Enter, ["Enter"] = Enter,
            ["Pause"] = 0x13, ["CapsLock"] = 0x14, ["Capital"] = 0x14, ["HangulMode"] = 0x15, ["KanaMode"] = 0x15,
            ["JunjaMode"] = 0x17, ["FinalMode"] = 0x18, ["KanjiMode"] = 0x19, ["HanjaMode"] = 0x19, ["Escape"] = Escape,
            ["ImeConvert"] = 0x1C, ["ImeNonConvert"] = 0x1D, ["ImeAccept"] = 0x1E, ["ImeModeChange"] = 0x1F,
            ["Space"] = 0x20, ["PageUp"] = 0x21, ["Prior"] = 0x21, ["PageDown"] = 0x22, ["Next"] = 0x22,
            ["End"] = 0x23, ["Home"] = 0x24, ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28,
            ["Select"] = 0x29, ["Print"] = 0x2A, ["Execute"] = 0x2B, ["Snapshot"] = 0x2C, ["PrintScreen"] = 0x2C,
            ["Insert"] = 0x2D, ["Delete"] = 0x2E, ["Help"] = 0x2F,
            ["LWin"] = LeftWindows, ["RWin"] = 0x5C, ["Apps"] = 0x5D, ["Sleep"] = 0x5F,
            ["Multiply"] = 0x6A, ["Add"] = 0x6B, ["Separator"] = 0x6C, ["Subtract"] = 0x6D, ["Decimal"] = 0x6E, ["Divide"] = 0x6F,
            ["NumLock"] = 0x90, ["Scroll"] = 0x91,
            ["LeftShift"] = 0xA0, ["RightShift"] = 0xA1, ["LeftCtrl"] = 0xA2, ["RightCtrl"] = 0xA3, ["LeftAlt"] = 0xA4, ["RightAlt"] = 0xA5,
            ["BrowserBack"] = 0xA6, ["BrowserForward"] = 0xA7, ["BrowserRefresh"] = 0xA8, ["BrowserStop"] = 0xA9,
            ["BrowserSearch"] = 0xAA, ["BrowserFavorites"] = 0xAB, ["BrowserHome"] = 0xAC,
            ["VolumeMute"] = 0xAD, ["VolumeDown"] = 0xAE, ["VolumeUp"] = 0xAF,
            ["MediaNextTrack"] = 0xB0, ["MediaPreviousTrack"] = 0xB1, ["MediaStop"] = 0xB2, ["MediaPlayPause"] = 0xB3,
            ["LaunchMail"] = 0xB4, ["SelectMedia"] = 0xB5, ["LaunchApplication1"] = 0xB6, ["LaunchApplication2"] = 0xB7,
            ["OemSemicolon"] = 0xBA, ["Oem1"] = 0xBA, ["OemPlus"] = 0xBB, ["OemComma"] = 0xBC, ["OemMinus"] = 0xBD,
            ["OemPeriod"] = 0xBE, ["OemQuestion"] = 0xBF, ["Oem2"] = 0xBF, ["OemTilde"] = 0xC0, ["Oem3"] = 0xC0,
            ["AbntC1"] = 0xC1, ["AbntC2"] = 0xC2, ["OemOpenBrackets"] = 0xDB, ["Oem4"] = 0xDB, ["OemPipe"] = 0xDC,
            ["Oem5"] = 0xDC, ["OemCloseBrackets"] = 0xDD, ["Oem6"] = 0xDD, ["OemQuotes"] = 0xDE, ["Oem7"] = 0xDE,
            ["Oem8"] = 0xDF, ["OemBackslash"] = 0xE2, ["Oem102"] = 0xE2, ["ImeProcessed"] = 0xE5,
            ["Attn"] = 0xF6, ["CrSel"] = 0xF7, ["ExSel"] = 0xF8, ["EraseEof"] = 0xF9, ["Play"] = 0xFA, ["Zoom"] = 0xFB,
            ["Pa1"] = 0xFD, ["OemClear"] = 0xFE,
        };

        for (var digit = 0; digit <= 9; digit++)
        {
            codes[$"D{digit}"] = 0x30 + digit;
            codes[$"NumPad{digit}"] = 0x60 + digit;
        }

        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            codes[new string(letter, 1)] = letter;
        }

        for (var function = 1; function <= 24; function++)
        {
            codes[$"F{function}"] = 0x6F + function;
        }

        return codes.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
