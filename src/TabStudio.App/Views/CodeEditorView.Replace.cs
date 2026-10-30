using System.Text.RegularExpressions;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Search;
using TabStudio.App.Editing;

namespace TabStudio.App.Views;

/// <summary>Replace and Replace All in the find panel, with a regular expression's groups filled in.</summary>
public sealed partial class CodeEditorView
{
    /// <summary>The find panel, for tests.</summary>
    internal SearchPanel Search => _search;

    /// <summary>
    /// Takes over the find panel's Replace and Replace All, its buttons and Alt+R and Alt+A alike: with a regular
    /// expression they expand <c>$1</c> and stop a pattern that runs too long; otherwise they replace as before.
    /// </summary>
    private void UseRegexReplace()
    {
        var collections = _editor.TextArea.DefaultInputHandler.NestedInputHandlers.OfType<TextAreaInputHandler>()
            .Select(h => h.CommandBindings)
            .Append(_search.CommandBindings)
            .Where(c => c.Any(b => b.Command == SearchCommands.ReplaceNext || b.Command == SearchCommands.ReplaceAll))
            .ToList();
        foreach (var bindings in collections)
        {
            foreach (var old in bindings.Where(b => b.Command == SearchCommands.ReplaceNext || b.Command == SearchCommands.ReplaceAll).ToList())
            {
                bindings.Remove(old);
            }

            bindings.Add(new RoutedCommandBinding(SearchCommands.ReplaceNext, (_, e) => { ReplaceNext(); e.Handled = true; }, CanReplace));
            bindings.Add(new RoutedCommandBinding(SearchCommands.ReplaceAll, (_, e) => { ReplaceAll(); e.Handled = true; }, CanReplace));
        }
    }

    private void CanReplace(object? sender, CanExecuteRoutedEventArgs e) => e.CanExecute = _search.IsOpened && _search.IsReplaceMode;

    /// <summary>Replaces the match at the selection, or the next one, and selects the match after it.</summary>
    internal void ReplaceNext()
    {
        if (!_search.IsReplaceMode)
        {
            return;
        }

        if (!_search.UseRegex)
        {
            _search.ReplaceNext();
            return;
        }

        if (RegexReplace.Create(_search.SearchPattern, _search.MatchCase) is not { } regex)
        {
            return;
        }

        try
        {
            var document = _editor.Document;
            var from = _editor.TextArea.Selection.IsEmpty ? _editor.CaretOffset : _editor.SelectionStart;
            if (RegexReplace.Next(regex, document.Text, from, _search.ReplacePattern, _search.WholeWords) is not { } replacement)
            {
                return;
            }

            document.Replace(replacement.Start, replacement.Length, replacement.Text);
            var after = replacement.Start + replacement.Text.Length;
            if (RegexReplace.Next(regex, document.Text, after, _search.ReplacePattern, _search.WholeWords) is { } next)
            {
                _editor.Select(next.Start, next.Length);
                _editor.ScrollTo(document.GetLineByOffset(next.Start).LineNumber, 0);
            }
            else
            {
                _editor.CaretOffset = after;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // The pattern runs too long on this document; nothing is replaced.
        }
    }

    /// <summary>Replaces every match, as one undo step.</summary>
    internal void ReplaceAll()
    {
        if (!_search.IsReplaceMode)
        {
            return;
        }

        if (!_search.UseRegex)
        {
            _search.ReplaceAll();
            return;
        }

        if (RegexReplace.Create(_search.SearchPattern, _search.MatchCase) is not { } regex)
        {
            return;
        }

        try
        {
            var document = _editor.Document;
            var replacements = RegexReplace.All(regex, document.Text, _search.ReplacePattern, _search.WholeWords);
            using (document.RunUpdate())
            {
                foreach (var replacement in replacements)
                {
                    document.Replace(replacement.Start, replacement.Length, replacement.Text);
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // The pattern runs too long on this document; nothing is replaced.
        }
    }
}
