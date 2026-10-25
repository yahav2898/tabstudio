using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.Compilation;
using TabStudio.Compilation.Features;

namespace TabStudio.App.ViewModels;

/// <summary>IntelliSense for the document: the language features, edits from them, and the breadcrumb.</summary>
public sealed partial class DocumentViewModel
{
    private IReadOnlyList<DocumentSymbol> _symbols = [];
    private int _symbolsEdits = -1;
    private int _crumbsSymbolsEdits = -1;

    /// <summary>IntelliSense for this document, or <see langword="null"/> when Roslyn does not analyze it.</summary>
    public ILanguageFeatures? Features => _language?.Features;

    /// <summary>A number that changes with every edit, to tell whether a result is about the current text.</summary>
    public int EditCount => _edits;

    /// <summary>The declarations that contain the caret, outermost first, for the breadcrumb.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SymbolCrumbViewModel> SymbolPath { get; private set; } = [];

    /// <summary>Applies edits from the language service as one undo step.</summary>
    /// <param name="edits">Edits in document order, against the current text.</param>
    public void ApplyEdits(IReadOnlyList<TextEdit> edits) => TextEdits.Apply(Document, edits);

    /// <summary>Asks the editor to move the caret to an offset and scroll it into view.</summary>
    /// <param name="offset">The offset.</param>
    public void NavigateToOffset(int offset)
    {
        var location = Document.GetLocation(Math.Clamp(offset, 0, Document.TextLength));
        NavigateTo(location.Line, location.Column);
    }

    /// <summary>Updates <see cref="SymbolPath"/> for a caret offset.</summary>
    /// <param name="offset">The caret offset.</param>
    /// <param name="cancellationToken">Cancels the update, typically because the caret moved again.</param>
    public async Task UpdateSymbolPathAsync(int offset, CancellationToken cancellationToken = default)
    {
        if (Features is not { } features)
        {
            return;
        }

        SyncText();
        var edits = _edits;
        IReadOnlyList<DocumentSymbol> path;
        try
        {
            if (_symbolsEdits != edits)
            {
                _symbols = await features.GetDocumentSymbolsAsync(FilePath, cancellationToken);
                _symbolsEdits = edits;
            }

            path = await features.GetSymbolPathAsync(FilePath, offset, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_disposed || edits != _edits || cancellationToken.IsCancellationRequested
            || (_crumbsSymbolsEdits == _symbolsEdits && IsSamePath(path)))
        {
            return;
        }

        var crumbs = new List<SymbolCrumbViewModel>(path.Count);
        var level = _symbols;
        foreach (var symbol in path)
        {
            crumbs.Add(new SymbolCrumbViewModel(this, symbol, level));
            level = level.FirstOrDefault(s => s.NameStart == symbol.NameStart && s.Name == symbol.Name)?.Children ?? [];
        }

        _crumbsSymbolsEdits = _symbolsEdits;
        SymbolPath = crumbs;
    }

    private bool IsSamePath(IReadOnlyList<DocumentSymbol> path) =>
        path.Count == SymbolPath.Count
        && path.Zip(SymbolPath, (symbol, crumb) => symbol.NameStart == crumb.Symbol.NameStart && symbol.DisplayName == crumb.Symbol.DisplayName).All(same => same);
}
