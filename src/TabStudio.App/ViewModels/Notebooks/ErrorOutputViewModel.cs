using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Notebooks;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>
/// An exception or compile error under a cell: what went wrong, and the trace, shortened to the first lines until
/// the user asks for the rest.
/// </summary>
public sealed partial class ErrorOutputViewModel : NotebookOutputViewModel
{
    /// <summary>How many trace lines are shown before "Show more".</summary>
    public const int CollapsedLineCount = 8;

    /// <summary>The name the kernel gives the error output of a cell that did not compile.</summary>
    public const string CompilationErrorName = "CompilationError";

    private readonly List<string> _details;

    /// <summary>Creates the output.</summary>
    /// <param name="error">The error.</param>
    public ErrorOutputViewModel(ErrorOutput error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Name = error.Name;
        IsCompilationError = error.Name == CompilationErrorName;
        var message = AnsiText.Strip(error.Message).Trim();
        Title = IsCompilationError ? "The cell has errors" : FormatTitle(AnsiText.Strip(error.Name), message);

        // The trace usually starts by repeating the title; it is shown once.
        var lines = error.Traceback
            .SelectMany(line => AnsiText.Strip(line).ReplaceLineEndings("\n").Split('\n'))
            .Select(line => line.TrimEnd())
            .ToList();
        while (lines.Count > 0 && (lines[0].Length == 0 || lines[0] == Title || lines[0] == message || lines[0] == $"{error.Name}: {message}"))
        {
            lines.RemoveAt(0);
        }

        if (IsCompilationError && lines.Count == 0 && message.Length > 0)
        {
            lines.AddRange(message.Split('\n'));
        }

        _details = lines;
    }

    /// <summary>The kind of error, such as <c>System.InvalidOperationException</c>.</summary>
    public string Name { get; }

    /// <summary>Whether the cell did not compile, rather than throwing while it ran.</summary>
    public bool IsCompilationError { get; }

    /// <summary>The first line: the exception type and message.</summary>
    public string Title { get; }

    /// <summary>Whether the whole trace is shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details), nameof(ToggleText))]
    public partial bool IsExpanded { get; set; }

    /// <summary>The trace lines shown: the first few, or all of them when expanded.</summary>
    public string Details => string.Join('\n', IsExpanded ? _details : _details.Take(CollapsedLineCount));

    /// <summary>Whether there is a trace to show.</summary>
    public bool HasDetails => _details.Count > 0;

    /// <summary>Whether the trace is longer than what is shown collapsed.</summary>
    public bool CanExpand => _details.Count > CollapsedLineCount;

    /// <summary>The label of the button that shows or hides the rest of the trace.</summary>
    public string ToggleText => IsExpanded
        ? "Show less"
        : string.Create(CultureInfo.CurrentCulture, $"Show {_details.Count - CollapsedLineCount} more lines");

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    private static string FormatTitle(string name, string message)
    {
        var shortName = name.Contains('.', StringComparison.Ordinal) ? name[(name.LastIndexOf('.') + 1)..] : name;
        if (message.Length == 0)
        {
            return shortName;
        }

        return shortName.Length == 0 ? message : $"{shortName}: {message}";
    }
}
