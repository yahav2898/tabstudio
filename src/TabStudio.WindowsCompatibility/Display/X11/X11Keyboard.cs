namespace TabStudio.WindowsCompatibility.Display.X11;

/// <summary>
/// Types on an X server's keyboard through XTEST: keys by their key symbols, and any character, also ones the
/// keyboard mapping does not have, which borrow a spare key code for as long as they are needed.
/// </summary>
/// <remarks>
/// A character the mapping lacks, such as a Hebrew letter, is given to a spare key code with
/// ChangeKeyboardMapping, the way <c>xdotool type</c> does; Wine reads the new mapping when the server announces
/// it, and turns the key into the character. Spare key codes are reused least recently used first, so a key code
/// is not remapped while the program may still be reading its last press.
/// </remarks>
internal sealed class X11Keyboard : IDisposable
{
    private readonly X11Client _client;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly LinkedList<(byte Keycode, uint Keysym)> _borrowed = new();
    private KeyboardMapping? _mapping;
    private List<byte> _spare = [];

    /// <summary>Creates the keyboard.</summary>
    /// <param name="client">The connection to the server.</param>
    public X11Keyboard(X11Client client)
    {
        _client = client;
        _client.KeyboardMappingChanged += (_, _) => _mapping = null;
    }

    /// <summary>Presses or releases the key with a symbol, such as Control or an arrow.</summary>
    /// <param name="keysym">The symbol.</param>
    /// <param name="pressed">Whether the key goes down.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task KeyAsync(uint keysym, bool pressed, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var key = await PositionAsync(keysym, cancellationToken).ConfigureAwait(false);
            if (key is { } found)
            {
                await _client.KeyAsync(found.Keycode, pressed, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Types characters: presses and releases the key of each, with Shift when its symbol needs it.</summary>
    /// <param name="text">The characters.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task TypeAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var rune in text.EnumerateRunes())
            {
                var key = await PositionAsync(KeySyms.ForCharacter(rune.Value), cancellationToken).ConfigureAwait(false);
                if (key is not { } found)
                {
                    continue;
                }

                var shift = found.Shift ? (await PositionAsync(KeySyms.ShiftL, cancellationToken).ConfigureAwait(false))?.Keycode : null;
                if (shift is { } shiftKey)
                {
                    await _client.KeyAsync(shiftKey, pressed: true, cancellationToken).ConfigureAwait(false);
                }

                await _client.KeyAsync(found.Keycode, pressed: true, cancellationToken).ConfigureAwait(false);
                await _client.KeyAsync(found.Keycode, pressed: false, cancellationToken).ConfigureAwait(false);
                if (shift is { } releasedShift)
                {
                    await _client.KeyAsync(releasedShift, pressed: false, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    /// <summary>The key of a symbol: from the mapping, or a spare key code given the symbol.</summary>
    private async Task<KeyPosition?> PositionAsync(uint keysym, CancellationToken cancellationToken)
    {
        if (keysym == 0)
        {
            return null;
        }

        if (_mapping is null)
        {
            _mapping = await _client.KeyboardMappingAsync(cancellationToken).ConfigureAwait(false);
            _spare = [.. _mapping.SpareKeycodes().Where(k => _borrowed.All(b => b.Keycode != k))];
        }

        if (_mapping.Find(keysym) is { } mapped)
        {
            Touch(mapped.Keycode);
            return mapped;
        }

        // Borrow the spare key code used least recently, or one never used yet.
        byte keycode;
        if (_spare.Count > 0)
        {
            keycode = _spare[0];
            _spare.RemoveAt(0);
        }
        else if (_borrowed.First is { } oldest)
        {
            keycode = oldest.Value.Keycode;
            _borrowed.RemoveFirst();
        }
        else
        {
            return null;
        }

        _borrowed.AddLast((keycode, keysym));
        await _client.ChangeKeyboardMappingAsync(keycode, [keysym, keysym], cancellationToken).ConfigureAwait(false);
        await _client.SyncAsync(cancellationToken).ConfigureAwait(false);
        _mapping = await _client.KeyboardMappingAsync(cancellationToken).ConfigureAwait(false);
        return new KeyPosition(keycode, Shift: false);
    }

    /// <summary>Marks a borrowed key code as just used.</summary>
    private void Touch(byte keycode)
    {
        for (var node = _borrowed.First; node is not null; node = node.Next)
        {
            if (node.Value.Keycode == keycode)
            {
                _borrowed.Remove(node);
                _borrowed.AddLast(node.Value);
                return;
            }
        }
    }
}
