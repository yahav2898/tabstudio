using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>Find (Ctrl+F) across the text of every cell, as the code editor finds in a file.</summary>
public sealed partial class NotebookViewModel
{
    private int _matchIndex = -1;

    /// <summary>Whether the find bar is shown.</summary>
    [ObservableProperty]
    public partial bool IsFindOpen { get; set; }

    /// <summary>What to find. Matching ignores case.</summary>
    [ObservableProperty]
    public partial string FindText { get; set; } = string.Empty;

    /// <summary>Where the current match is, such as "2 of 5", or "No results".</summary>
    [ObservableProperty]
    public partial string? FindStatus { get; private set; }

    /// <summary>Raised when the find bar should take the keyboard.</summary>
    public event EventHandler? FindRequested;

    /// <summary>Opens the find bar and puts the keyboard in it.</summary>
    public void OpenFind()
    {
        IsFindOpen = true;
        FindRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Selects the next match, after the current one.</summary>
    [RelayCommand]
    private void FindNext() => MoveToMatch(1);

    /// <summary>Selects the previous match.</summary>
    [RelayCommand]
    private void FindPrevious() => MoveToMatch(-1);

    [RelayCommand]
    private void CloseFind()
    {
        IsFindOpen = false;
        FindStatus = null;
    }

    partial void OnFindTextChanged(string value)
    {
        // Search as the user types, from the top.
        _matchIndex = -1;
        MoveToMatch(1);
    }

    private void MoveToMatch(int step)
    {
        var matches = FindMatches();
        if (matches.Count == 0)
        {
            _matchIndex = -1;
            FindStatus = FindText.Length == 0 ? null : "No results";
            return;
        }

        _matchIndex = _matchIndex < 0 && step < 0
            ? matches.Count - 1
            : (Math.Max(_matchIndex, -1) + step + matches.Count) % matches.Count;
        var (cell, offset) = matches[_matchIndex];
        SelectedCell = cell;
        cell.IsEditing = cell.IsMarkdown;
        cell.Highlight(offset, FindText.Length);
        FindStatus = string.Create(CultureInfo.CurrentCulture, $"{_matchIndex + 1} of {matches.Count}");
    }

    private List<(NotebookCellViewModel Cell, int Offset)> FindMatches()
    {
        var matches = new List<(NotebookCellViewModel, int)>();
        var query = FindText;
        if (query.Length == 0)
        {
            return matches;
        }

        foreach (var cell in Cells)
        {
            var text = cell.Source.Text;
            for (var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase); index >= 0; index = text.IndexOf(query, index + query.Length, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add((cell, index));
            }
        }

        return matches;
    }
}
