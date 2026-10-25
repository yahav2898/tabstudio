using Microsoft.CodeAnalysis;

namespace TabStudio.Compilation.Features;

/// <summary>Turns the difference between two Roslyn solutions into per-file text edits for the editor.</summary>
internal static class SolutionEdits
{
    /// <summary>Returns the edits that turn <paramref name="oldSolution"/> into <paramref name="newSolution"/>.</summary>
    /// <param name="oldSolution">The solution the edits apply to.</param>
    /// <param name="newSolution">The solution after a rename or code action.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public static async Task<List<DocumentEdit>> GetDocumentEditsAsync(Solution oldSolution, Solution newSolution, CancellationToken cancellationToken)
    {
        var edits = new List<DocumentEdit>();
        foreach (var project in newSolution.GetChanges(oldSolution).GetProjectChanges())
        {
            foreach (var id in project.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true))
            {
                var oldDocument = oldSolution.GetDocument(id);
                var newDocument = newSolution.GetDocument(id);
                if (oldDocument is null || newDocument is null || string.IsNullOrEmpty(oldDocument.FilePath))
                {
                    continue;
                }

                var changes = await newDocument.GetTextChangesAsync(oldDocument, cancellationToken).ConfigureAwait(false);
                var textEdits = changes
                    .OrderBy(c => c.Span.Start)
                    .Select(c => new TextEdit(c.Span.Start, c.Span.Length, c.NewText ?? string.Empty))
                    .ToList();
                if (textEdits.Count > 0)
                {
                    edits.Add(new DocumentEdit(oldDocument.FilePath, DocumentEditKind.Modified, textEdits));
                }
            }

            foreach (var id in project.GetAddedDocuments())
            {
                if (newSolution.GetDocument(id) is not { } document)
                {
                    continue;
                }

                var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                edits.Add(new DocumentEdit(PathOf(document), DocumentEditKind.Added, [new TextEdit(0, 0, text.ToString())]));
            }

            foreach (var id in project.GetRemovedDocuments())
            {
                if (oldSolution.GetDocument(id)?.FilePath is { Length: > 0 } path)
                {
                    edits.Add(new DocumentEdit(path, DocumentEditKind.Removed, []));
                }
            }
        }

        return edits;
    }

    /// <summary>The path of a document, also for documents a code action created without one.</summary>
    private static string PathOf(Document document)
    {
        if (!string.IsNullOrEmpty(document.FilePath))
        {
            return document.FilePath;
        }

        var directory = Path.GetDirectoryName(document.Project.FilePath) ?? string.Empty;
        return Path.Combine([directory, .. document.Folders, document.Name]);
    }
}
