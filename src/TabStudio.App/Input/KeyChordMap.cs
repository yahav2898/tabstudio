using System.Windows.Input;
using Avalonia.Input;

namespace TabStudio.App.Input;

/// <summary>
/// Two-step keyboard shortcuts, such as Visual Studio's Ctrl+R, A: a first gesture starts the chord and the next key
/// finishes it. Avalonia's key bindings only know single gestures.
/// </summary>
/// <remarks>
/// The first gesture is not swallowed, so other handlers still see it; the second key is swallowed only when it
/// completes a chord of this map. Any other key ends the pending chord.
/// </remarks>
public sealed class KeyChordMap
{
    private readonly Dictionary<(Key Key, KeyModifiers Modifiers), Dictionary<Key, Chord>> _chords = [];
    private (Key Key, KeyModifiers Modifiers)? _pending;

    /// <summary>
    /// Raised when a chord starts, with the text Visual Studio shows in its status bar, and with
    /// <see langword="null"/> when it ends.
    /// </summary>
    public event EventHandler<string?>? PendingChanged;

    /// <summary>Whether the first gesture of a chord was pressed and the map waits for the second key.</summary>
    public bool IsPending => _pending is not null;

    /// <summary>Adds a chord.</summary>
    /// <param name="first">The first key, such as R.</param>
    /// <param name="modifiers">The modifiers of the first key, such as Ctrl.</param>
    /// <param name="second">The second key, pressed without modifiers, such as A.</param>
    /// <param name="command">Returns the command to run; it is looked up when the chord completes.</param>
    /// <param name="parameter">The command's parameter.</param>
    public void Add(Key first, KeyModifiers modifiers, Key second, Func<ICommand?> command, object? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_chords.TryGetValue((first, modifiers), out var seconds))
        {
            seconds = [];
            _chords[(first, modifiers)] = seconds;
        }

        seconds[second] = new Chord(command, parameter);
    }

    /// <summary>Handles a key press.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <returns><see langword="true"/> when the key completed a chord, so nothing else should handle it.</returns>
    public bool HandleKeyDown(Key key, KeyModifiers modifiers)
    {
        if (IsModifier(key))
        {
            return false;
        }

        if (_pending is { } pending)
        {
            _pending = null;
            PendingChanged?.Invoke(this, null);
            if (modifiers == KeyModifiers.None && _chords[pending].TryGetValue(key, out var chord))
            {
                if (chord.Command() is { } command && command.CanExecute(chord.Parameter))
                {
                    command.Execute(chord.Parameter);
                }

                return true;
            }
        }

        if (_chords.ContainsKey((key, modifiers)))
        {
            _pending = (key, modifiers);
            PendingChanged?.Invoke(this, $"({new KeyGesture(key, modifiers)}) was pressed. Waiting for second key of chord...");
        }

        return false;
    }

    private static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    /// <summary>What a completed chord runs.</summary>
    /// <param name="Command">Returns the command.</param>
    /// <param name="Parameter">The command's parameter.</param>
    private sealed record Chord(Func<ICommand?> Command, object? Parameter);
}
