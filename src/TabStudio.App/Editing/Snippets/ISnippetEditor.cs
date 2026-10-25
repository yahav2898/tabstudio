using TabStudio.Extensions.Snippets;

namespace TabStudio.App.Editing.Snippets;

/// <summary>What <see cref="SnippetCompletionFeatures"/> needs from the editor it lists snippets in.</summary>
public interface ISnippetEditor
{
    /// <summary>The snippets installed extensions contribute for the document's language now.</summary>
    IReadOnlyList<SnippetDefinition> Snippets { get; }

    /// <summary>The caret offset.</summary>
    int Caret { get; }

    /// <summary>The text of the line from its start to an offset.</summary>
    /// <param name="offset">An offset in the document.</param>
    string LineTextBefore(int offset);

    /// <summary>Replaces a range with a snippet, expanded for its place, and starts filling in its tab stops.</summary>
    /// <param name="snippet">The snippet.</param>
    /// <param name="start">Where the replaced text starts, such as the start of the typed prefix.</param>
    /// <param name="length">How much text is replaced.</param>
    void InsertSnippet(SnippetDefinition snippet, int start, int length);

    /// <summary>Replaces the text of the current choice tab stop with one of its options.</summary>
    /// <param name="choice">The chosen option.</param>
    void ChooseOption(string choice);
}
