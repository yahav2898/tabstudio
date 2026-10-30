namespace TabStudio.Compilation.Features;

/// <summary>
/// IntelliSense for the editor: completion, Quick Info, signature help, navigation, rename, code actions,
/// document symbols and highlights, all answered by Roslyn from the live model in a <see cref="RoslynWorkspaceHost"/>,
/// for C# and Visual Basic files alike. Each query works in the language of the file it is asked about.
/// </summary>
/// <remarks>
/// <para>
/// Create one per host and keep it for the lifetime of the host. Every query reads an immutable snapshot of the
/// solution, so queries run off the UI thread and concurrently with edits; cancel a query when the user moves on.
/// </para>
/// <para>
/// The API knows nothing about the UI and exposes no Roslyn types. Positions are zero-based character offsets
/// into the document text, and results are plain records, so the same API can later be served to another device
/// over TabStudio Link.
/// </para>
/// </remarks>
public sealed class LanguageFeatures
{
    /// <summary>Creates the features for a host.</summary>
    /// <param name="host">The host that holds the loaded projects.</param>
    public LanguageFeatures(RoslynWorkspaceHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Completion = new CompletionFeature(host);
        QuickInfo = new QuickInfoFeature(host);
        SignatureHelp = new SignatureHelpFeature(host);
        Navigation = new NavigationFeature(host);
        Highlights = new HighlightFeature(host);
        Rename = new RenameFeature(host);
        CodeActions = new CodeActionFeature(host);
        Symbols = new DocumentSymbolFeature(host);
    }

    /// <summary>The completion list, committing items and item descriptions.</summary>
    public CompletionFeature Completion { get; }

    /// <summary>Tooltips with the signature and documentation of a symbol.</summary>
    public QuickInfoFeature QuickInfo { get; }

    /// <summary>The parameter list of the call being typed.</summary>
    public SignatureHelpFeature SignatureHelp { get; }

    /// <summary>Go to Definition, Go to Implementation and Find All References.</summary>
    public NavigationFeature Navigation { get; }

    /// <summary>Occurrences of the symbol under the caret.</summary>
    public HighlightFeature Highlights { get; }

    /// <summary>Renaming a symbol everywhere it is used.</summary>
    public RenameFeature Rename { get; }

    /// <summary>Quick fixes and refactorings (Ctrl+.).</summary>
    public CodeActionFeature CodeActions { get; }

    /// <summary>The declarations of a file, for the navigation bar and the breadcrumb.</summary>
    public DocumentSymbolFeature Symbols { get; }
}
