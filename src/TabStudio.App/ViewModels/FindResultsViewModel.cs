using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The Find Results panel: Find in Files (Ctrl+Shift+F) and Replace in Files (Ctrl+Shift+H) as Visual Studio has them,
/// with what to find, what to replace it with, Match case, Match whole word and Use regular expressions above the
/// matches. Its list is its own, apart from Find All References', so one search never replaces the other's results.
/// </summary>
public sealed partial class FindResultsViewModel : ObservableObject
{
    /// <summary>The panel's title before the first search.</summary>
    internal const string DefaultTitle = "Find Results";

    private readonly Func<FindInFilesQuery, Task> _find;
    private readonly Func<FindInFilesQuery, string, Task> _replace;

    /// <summary>Creates the panel.</summary>
    /// <param name="navigate">Opens a file at a 1-based line and column.</param>
    /// <param name="find">Runs Find All for a query and shows its matches in <see cref="Results"/>.</param>
    /// <param name="replace">Runs Replace All for a query and a replacement.</param>
    public FindResultsViewModel(Func<string, int, int, Task> navigate, Func<FindInFilesQuery, Task> find, Func<FindInFilesQuery, string, Task> replace)
    {
        ArgumentNullException.ThrowIfNull(find);
        ArgumentNullException.ThrowIfNull(replace);
        _find = find;
        _replace = replace;
        Results = new ReferencesViewModel(navigate, DefaultTitle, "Find text in every file of the solution's folder: type it above and press Enter.");
    }

    /// <summary>The matches of the last Find All or Replace All, grouped by file. Tapping one opens it.</summary>
    public ReferencesViewModel Results { get; }

    /// <summary>What to find.</summary>
    [ObservableProperty]
    public partial string FindText { get; set; } = string.Empty;

    /// <summary>What replaces each match.</summary>
    [ObservableProperty]
    public partial string ReplaceText { get; set; } = string.Empty;

    /// <summary>Whether letter case must match.</summary>
    [ObservableProperty]
    public partial bool MatchCase { get; set; }

    /// <summary>Whether a match must be a whole word.</summary>
    [ObservableProperty]
    public partial bool MatchWholeWord { get; set; }

    /// <summary>Whether <see cref="FindText"/> is a regular expression, and <c>$1</c> in <see cref="ReplaceText"/> its first group.</summary>
    [ObservableProperty]
    public partial bool UseRegularExpressions { get; set; }

    /// <summary>Whether the replace box and Replace All show: Replace in Files (Ctrl+Shift+H) rather than Find in Files.</summary>
    [ObservableProperty]
    public partial bool IsReplaceMode { get; set; }

    /// <summary>The query the form describes; its text is the first line of <see cref="FindText"/>.</summary>
    public FindInFilesQuery Query => new(FirstLine(FindText), MatchCase, MatchWholeWord, UseRegularExpressions);

    /// <summary>Raised when the find box should take the keyboard focus, as Ctrl+Shift+F opens the panel.</summary>
    public event EventHandler? FocusRequested;

    /// <summary>Asks the view to put the keyboard focus in the find box, with its text selected.</summary>
    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Find All: lists every match. A new search stops the one still running.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task FindAllAsync()
    {
        if (Query.Text.Length == 0)
        {
            Results.ShowMessage(DefaultTitle, "Type what to find first.");
            return Task.CompletedTask;
        }

        return _find(Query);
    }

    /// <summary>Replace All: replaces every match in every file, and lists what was replaced.</summary>
    [RelayCommand]
    private Task ReplaceAllAsync()
    {
        if (Query.Text.Length == 0)
        {
            Results.ShowMessage(DefaultTitle, "Type what to find first.");
            return Task.CompletedTask;
        }

        return _replace(Query, FirstLine(ReplaceText));
    }

    private static string FirstLine(string? text) => (text ?? string.Empty).Split('\n')[0].TrimEnd('\r');
}
