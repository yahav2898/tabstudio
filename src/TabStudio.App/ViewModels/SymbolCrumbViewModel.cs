using CommunityToolkit.Mvvm.Input;
using TabStudio.Compilation.Features;

namespace TabStudio.App.ViewModels;

/// <summary>
/// A declaration in the breadcrumb under the tabs, such as the type or member the caret is in. Tapping it lists the
/// declarations next to it, like Visual Studio's navigation bar, and picking one moves the caret there.
/// </summary>
public sealed partial class SymbolCrumbViewModel
{
    private readonly DocumentViewModel _document;
    private readonly IReadOnlyList<DocumentSymbol> _siblings;
    private IReadOnlyList<SymbolCrumbViewModel>? _siblingCrumbs;

    /// <summary>Creates a crumb.</summary>
    /// <param name="document">The document the declaration is in.</param>
    /// <param name="symbol">The declaration.</param>
    /// <param name="siblings">The declarations at the same level, the declaration included.</param>
    public SymbolCrumbViewModel(DocumentViewModel document, DocumentSymbol symbol, IReadOnlyList<DocumentSymbol> siblings)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(siblings);
        _document = document;
        Symbol = symbol;
        _siblings = siblings;
    }

    /// <summary>The declaration.</summary>
    public DocumentSymbol Symbol { get; }

    /// <summary>Its name as shown, such as <c>Greet(string)</c>.</summary>
    public string Name => Symbol.DisplayName;

    /// <summary>Its icon.</summary>
    public SymbolGlyph Kind => Symbol.Kind;

    /// <summary>The declarations at the same level, for the drop-down.</summary>
    public IReadOnlyList<SymbolCrumbViewModel> Siblings =>
        _siblingCrumbs ??= _siblings.Select(s => new SymbolCrumbViewModel(_document, s, [])).ToList();

    /// <summary>Moves the caret to the declaration's name.</summary>
    [RelayCommand]
    private void Navigate() => _document.NavigateToOffset(Symbol.NameStart);
}
