using Avalonia;

namespace TabStudio.App.Swarm.Recording;

/// <summary>What one step of a recorded UI test does.</summary>
public enum UiTestStepKind
{
    /// <summary>Taps an element.</summary>
    Tap,

    /// <summary>Long-presses an element, or right-clicks it.</summary>
    LongPress,

    /// <summary>Scrolls the element under the pointer.</summary>
    Scroll,

    /// <summary>Types text into the focused element.</summary>
    Type,

    /// <summary>Presses a key, with modifiers, on the focused element.</summary>
    Press,

    /// <summary>Checks that an element shows a text.</summary>
    Check,
}

/// <summary>One step of a recorded UI test: an input as a person made it, or a check of what an element shows.</summary>
/// <param name="Kind">What the step does.</param>
/// <param name="ElementPath">The element, in <see cref="AppInput.ElementPath"/>'s form, or its name; empty for typing and keys.</param>
/// <param name="Text">The text typed, or the text a check expects.</param>
/// <param name="Key">The key pressed, as Avalonia names it, such as <c>Enter</c>.</param>
/// <param name="Modifiers">The modifiers held, as Avalonia names them, such as <c>Control, Shift</c>.</param>
/// <param name="ScrollX">How far a scroll went sideways, in device-independent pixels.</param>
/// <param name="ScrollY">How far a scroll went down, in device-independent pixels.</param>
public sealed record UiTestStep(
    UiTestStepKind Kind,
    string ElementPath,
    string? Text = null,
    string? Key = null,
    string? Modifiers = null,
    double ScrollX = 0,
    double ScrollY = 0)
{
    /// <summary>The step an input becomes.</summary>
    /// <param name="input">The input.</param>
    public static UiTestStep From(AppInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Kind switch
        {
            AppInputKind.Tap => new UiTestStep(UiTestStepKind.Tap, input.ElementPath),
            AppInputKind.LongPress => new UiTestStep(UiTestStepKind.LongPress, input.ElementPath),
            AppInputKind.Scroll => new UiTestStep(UiTestStepKind.Scroll, input.ElementPath, ScrollX: input.ScrollDelta.X, ScrollY: input.ScrollDelta.Y),
            AppInputKind.Text => new UiTestStep(UiTestStepKind.Type, string.Empty, input.Text),
            _ => new UiTestStep(UiTestStepKind.Press, string.Empty, Key: input.Key, Modifiers: input.Modifiers),
        };
    }

    /// <summary>A check that an element shows a text.</summary>
    /// <param name="elementPath">The element.</param>
    /// <param name="text">The text it shows.</param>
    public static UiTestStep Check(string elementPath, string text) => new(UiTestStepKind.Check, elementPath, text);

    /// <summary>The input to replay for the step; <see langword="null"/> for a check.</summary>
    public AppInput? ToInput() => Kind switch
    {
        UiTestStepKind.Tap => new AppInput(AppInputKind.Tap, ElementPath, new Point(0.5, 0.5)),
        UiTestStepKind.LongPress => new AppInput(AppInputKind.LongPress, ElementPath, new Point(0.5, 0.5)),
        UiTestStepKind.Scroll => new AppInput(AppInputKind.Scroll, ElementPath, new Point(0.5, 0.5), ScrollDelta: new Vector(ScrollX, ScrollY)),
        UiTestStepKind.Type => new AppInput(AppInputKind.Text, string.Empty, Text: Text),
        UiTestStepKind.Press => new AppInput(AppInputKind.Key, string.Empty, Key: Key, Modifiers: Modifiers),
        _ => null,
    };

    /// <summary>The step in words, as the recorder lists it.</summary>
    public string Description
    {
        get
        {
            var element = ElementPaths.ShortName(ElementPath);
            return Kind switch
            {
                UiTestStepKind.Tap => $"Tap {element}",
                UiTestStepKind.LongPress => $"Long-press {element}",
                UiTestStepKind.Scroll => $"Scroll {element}",
                UiTestStepKind.Type => $"Type \"{Text}\"",
                UiTestStepKind.Press => Modifiers is { Length: > 0 } modifiers ? $"Press {modifiers.Replace(", ", "+", StringComparison.Ordinal)}+{Key}" : $"Press {Key}",
                _ => $"Check that {element} shows \"{Text}\"",
            };
        }
    }

    /// <summary>Whether the step checks the screen rather than giving it input.</summary>
    public bool IsCheck => Kind == UiTestStepKind.Check;
}

/// <summary>A UI test recorded on one screen.</summary>
/// <param name="Name">The test's name, as the person called it, such as <c>Counting three taps</c>.</param>
/// <param name="Profile">The screen it was recorded on, whose size the test starts the app at.</param>
/// <param name="Steps">The steps, in order.</param>
public sealed record UiTestRecording(string Name, ScreenProfile Profile, IReadOnlyList<UiTestStep> Steps);
