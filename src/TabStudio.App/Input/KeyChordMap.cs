using System.Windows.Input;
using Avalonia.Input;

namespace TabStudio.App.Input;

/// <summary>
/// Two-step keyboard shortcuts, such as Visual Studio's Ctrl+R, A: a first gesture starts the chord and the next key
/// finishes it. Avalonia's key bindings only know single gestures.
/// </summary>
/// <remarks>
/// As in Visual Studio, both keys of a chord are swallowed, and so is a second key that finishes no chord: the
/// status bar then says that the combination is not a command.
/// </remarks>
public sealed class KeyChordMap
{
    private readonly Dictionary<KeyGesture, Dictionary<KeyGesture, Chord>> _chords = [];
    private KeyGesture? _pending;

    /// <summary>
    /// Raised when a chord starts, with the text Visual Studio shows in its status bar, and with
    /// <see langword="null"/> when it ends.
    /// </summary>
    public event EventHandler<string?>? PendingChanged;

    /// <summary>Raised after a chord ended with a second key that finishes no chord, with the message to show.</summary>
    public event EventHandler<string>? NotACommand;

    /// <summary>Whether the first gesture of a chord was pressed and the map waits for the second key.</summary>
    public bool IsPending => _pending is not null;

    /// <summary>Adds a chord whose second key is pressed without modifiers, such as Ctrl+R, A.</summary>
    /// <param name="first">The first key, such as R.</param>
    /// <param name="modifiers">The modifiers of the first key, such as Ctrl.</param>
    /// <param name="second">The second key, such as A.</param>
    /// <param name="command">Returns the command to run when the chord completes, or <see langword="null"/> when it does not apply now.</param>
    /// <param name="parameter">The command's parameter.</param>
    public void Add(Key first, KeyModifiers modifiers, Key second, Func<ICommand?> command, object? parameter = null) =>
        Add(new KeyGesture(first, modifiers), new KeyGesture(second), command, parameter);

    /// <summary>Adds a chord, such as Ctrl+R, Ctrl+R.</summary>
    /// <param name="first">The first gesture.</param>
    /// <param name="second">The second gesture.</param>
    /// <param name="command">Returns the command to run when the chord completes, or <see langword="null"/> when it does not apply now.</param>
    /// <param name="parameter">The command's parameter.</param>
    public void Add(KeyGesture first, KeyGesture second, Func<ICommand?> command, object? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(command);
        if (!_chords.TryGetValue(first, out var seconds))
        {
            seconds = [];
            _chords[first] = seconds;
        }

        seconds[second] = new Chord(command, parameter);
    }

    /// <summary>Handles a key press.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <returns><see langword="true"/> when the key belonged to a chord, so nothing else should handle it.</returns>
    public bool HandleKeyDown(Key key, KeyModifiers modifiers)
    {
        if (IsModifier(key))
        {
            return false;
        }

        var gesture = new KeyGesture(key, modifiers);
        if (_pending is { } pending)
        {
            _pending = null;
            PendingChanged?.Invoke(this, null);
            if (_chords[pending].TryGetValue(gesture, out var chord))
            {
                if (chord.Command() is { } command && command.CanExecute(chord.Parameter))
                {
                    command.Execute(chord.Parameter);
                }
            }
            else
            {
                NotACommand?.Invoke(this, $"The key combination ({pending}, {gesture}) is not a command.");
            }

            return true;
        }

        if (_chords.ContainsKey(gesture))
        {
            _pending = gesture;
            PendingChanged?.Invoke(this, $"({gesture}) was pressed. Waiting for second key of chord...");
            return true;
        }

        return false;
    }

    /// <summary>Forgets a pending chord, such as when the pointer moves the focus.</summary>
    public void Cancel()
    {
        if (_pending is not null)
        {
            _pending = null;
            PendingChanged?.Invoke(this, null);
        }
    }

    private static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    /// <summary>What a completed chord runs.</summary>
    /// <param name="Command">Returns the command.</param>
    /// <param name="Parameter">The command's parameter.</param>
    private sealed record Chord(Func<ICommand?> Command, object? Parameter);
}
