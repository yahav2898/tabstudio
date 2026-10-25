namespace TabStudio.WindowsCompatibility.Display.X11;

/// <summary>Where a key symbol is on the keyboard: its key code, and whether Shift must be held for it.</summary>
/// <param name="Keycode">The key code.</param>
/// <param name="Shift">Whether the symbol is the key's shifted one.</param>
internal readonly record struct KeyPosition(byte Keycode, bool Shift);

/// <summary>An X server's keyboard mapping: the key symbols of each key code, without and with Shift.</summary>
/// <param name="FirstKeycode">The first key code.</param>
/// <param name="SymbolsPerKeycode">How many symbols each key code has.</param>
/// <param name="Keysyms">The symbols, key code by key code.</param>
internal sealed record KeyboardMapping(byte FirstKeycode, int SymbolsPerKeycode, uint[] Keysyms)
{
    /// <summary>How many key codes the mapping has.</summary>
    public int Count => SymbolsPerKeycode == 0 ? 0 : Keysyms.Length / SymbolsPerKeycode;

    /// <summary>The key that types a symbol, preferring one without Shift.</summary>
    /// <param name="keysym">The symbol.</param>
    /// <returns>Its key, or <see langword="null"/> when no key has it.</returns>
    public KeyPosition? Find(uint keysym)
    {
        for (var column = 0; column < Math.Min(2, SymbolsPerKeycode); column++)
        {
            for (var key = 0; key < Count; key++)
            {
                if (Keysyms[(key * SymbolsPerKeycode) + column] == keysym)
                {
                    return new KeyPosition((byte)(FirstKeycode + key), column == 1);
                }
            }
        }

        return null;
    }

    /// <summary>Key codes that have no symbols, from the highest down, to lend to characters the keyboard does not have.</summary>
    public IReadOnlyList<byte> SpareKeycodes()
    {
        var spare = new List<byte>();
        for (var key = Count - 1; key >= 0; key--)
        {
            var empty = true;
            for (var column = 0; column < SymbolsPerKeycode; column++)
            {
                empty &= Keysyms[(key * SymbolsPerKeycode) + column] == 0;
            }

            if (empty)
            {
                spare.Add((byte)(FirstKeycode + key));
            }
        }

        return spare;
    }
}
