using TabStudio.Compilation.Running;

namespace TabStudio.App.ViewModels;

/// <summary>A file location in a line of the Output panel, shown as a link.</summary>
/// <param name="FilePath">The file's full path.</param>
/// <param name="Line">The 1-based line.</param>
/// <param name="Column">The 1-based column.</param>
/// <param name="Start">Where the link starts in the output line.</param>
/// <param name="Length">The link's length.</param>
public sealed record OutputLink(string FilePath, int Line, int Column, int Start, int Length);

/// <summary>Links to file locations, and input for programs that run in their own process.</summary>
public sealed partial class OutputViewModel
{
    /// <summary>
    /// Finds the file locations in a line of output, such as the frames of a Python traceback, which the panel
    /// shows as links; none when <see langword="null"/>. Called for the lines in view, on the UI thread.
    /// </summary>
    public Func<string, IReadOnlyList<OutputLink>>? LinkFinder { get; set; }

    /// <summary>Raised on the UI thread when the user taps a link.</summary>
    public event EventHandler<OutputLink>? LinkActivated;

    /// <summary>Opens a link: raises <see cref="LinkActivated"/>.</summary>
    /// <param name="link">The link.</param>
    public void ActivateLink(OutputLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        LinkActivated?.Invoke(this, link);
    }

    /// <summary>
    /// Connects the input box to a program in its own process, such as a Python script, for its standard input.
    /// Unlike <see cref="BeginInput"/>, reading the input does not mean the program waits for a line, since the
    /// IDE forwards lines as they are typed; <see cref="ShowWaitingForInput"/> says when it seems to.
    /// </summary>
    /// <returns>The input, whose lines go to the program.</returns>
    public ConsoleInput BeginProcessInput()
    {
        var input = BeginInput();
        input.WaitingForInput -= OnWaitingForInput;
        return input;
    }

    /// <summary>
    /// Says the program seems to wait for input, as when it printed a prompt without a newline and went quiet,
    /// which focuses the input box. Safe to call from any thread.
    /// </summary>
    public void ShowWaitingForInput() => _ui.Post(() =>
    {
        if (IsAcceptingInput)
        {
            IsWaitingForInput = true;
        }
    });
}
