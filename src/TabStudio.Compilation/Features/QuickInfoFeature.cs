using Microsoft.CodeAnalysis.QuickInfo;
using RoslynQuickInfoSection = Microsoft.CodeAnalysis.QuickInfo.QuickInfoSection;

namespace TabStudio.Compilation.Features;

/// <summary>Quick Info: the tooltip with the signature and documentation of the symbol under the pointer.</summary>
public sealed class QuickInfoFeature
{
    private readonly RoslynWorkspaceHost _host;

    internal QuickInfoFeature(RoslynWorkspaceHost host) => _host = host;

    /// <summary>Returns the Quick Info tooltip at a position.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">Zero-based offset of the pointer or caret.</param>
    /// <param name="cancellationToken">Cancels the request, typically because the pointer moved.</param>
    /// <returns>The tooltip, or <see langword="null"/> when there is nothing to show.</returns>
    public async Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (_host.GetDocument(filePath) is not { } document || QuickInfoService.GetService(document) is not { } service)
        {
            return null;
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var item = await service.GetQuickInfoAsync(document, Math.Clamp(position, 0, text.Length), cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return null;
        }

        var sections = item.Sections
            .Select(ToSection)
            .Where(s => s.Runs.Count > 0)
            .ToList();

        return sections.Count == 0
            ? null
            : new QuickInfoResult(item.Span.Start, item.Span.Length, GlyphMapper.FromTags(item.Tags), sections);
    }

    private static QuickInfoSection ToSection(RoslynQuickInfoSection section) =>
        new(MapKind(section.Kind), TaggedTextMapper.Map(section.TaggedParts));

    private static QuickInfoSectionKind MapKind(string kind) => kind switch
    {
        QuickInfoSectionKinds.Description => QuickInfoSectionKind.Description,
        QuickInfoSectionKinds.DocumentationComments => QuickInfoSectionKind.Documentation,
        QuickInfoSectionKinds.ReturnsDocumentationComments => QuickInfoSectionKind.Returns,
        QuickInfoSectionKinds.ValueDocumentationComments => QuickInfoSectionKind.Value,
        QuickInfoSectionKinds.RemarksDocumentationComments => QuickInfoSectionKind.Remarks,
        QuickInfoSectionKinds.TypeParameters => QuickInfoSectionKind.TypeParameters,
        QuickInfoSectionKinds.AnonymousTypes => QuickInfoSectionKind.AnonymousTypes,
        QuickInfoSectionKinds.Usage => QuickInfoSectionKind.Usage,
        QuickInfoSectionKinds.Exception => QuickInfoSectionKind.Exceptions,
        QuickInfoSectionKinds.Captures => QuickInfoSectionKind.Captures,
        _ => QuickInfoSectionKind.Other,
    };
}
