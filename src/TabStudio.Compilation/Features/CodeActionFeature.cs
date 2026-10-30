using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace TabStudio.Compilation.Features;

/// <summary>
/// Quick fixes and refactorings, the Ctrl+. (light bulb) menu: add a missing <c>using</c> (<c>Imports</c> in Visual
/// Basic), implement an interface, generate a method, remove unnecessary usings and the rest of Roslyn's C# and
/// Visual Basic code actions. Each document gets the providers of its own language.
/// </summary>
/// <remarks>
/// <para>
/// Listing the actions is cheap; computing what an action changes happens only when the user picks it, through
/// <see cref="GetCodeActionEditsAsync"/>. The actions of the last request are remembered for that.
/// </para>
/// <para>
/// Every provider runs on its own: one that fails or throws is left out, and the others still show up. Actions
/// that need a dialog in Visual Studio, such as Extract Interface, are not offered.
/// </para>
/// </remarks>
public sealed class CodeActionFeature
{
    private const int MaxNestingDepth = 3;

    private readonly RoslynWorkspaceHost _host;
    private readonly Lock _gate = new();
    private Dictionary<string, PendingAction> _pending = new(StringComparer.Ordinal);
    private long _lastRequest;

    internal CodeActionFeature(RoslynWorkspaceHost host) => _host = host;

    /// <summary>Lists the code actions for a selection or the caret.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="start">Zero-based offset of the selection, or of the caret.</param>
    /// <param name="length">Length of the selection, or 0 for the caret.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Fixes for the diagnostics at the position first, then refactorings, most relevant first.</returns>
    public async Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (_host.GetDocument(filePath) is not { } document
            || await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false) is not { } model)
        {
            return [];
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var spanStart = Math.Clamp(start, 0, text.Length);
        var span = new TextSpan(spanStart, Math.Clamp(length, 0, text.Length - spanStart));
        var catalog = CodeActionProviderCatalog.Instance;

        var fixes = new List<(CodeAction Action, string DiagnosticId)>();
        foreach (var diagnostic in DiagnosticsAt(model, text, span, cancellationToken))
        {
            foreach (var action in await GetFixesAsync(document, diagnostic, catalog, cancellationToken).ConfigureAwait(false))
            {
                fixes.Add((action, diagnostic.Id));
            }
        }

        var refactorings = await GetRefactoringsAsync(document, span, catalog, cancellationToken).ConfigureAwait(false);

        var found = fixes
            .OrderByDescending(f => f.Action.Priority)
            .Select(f => (f.Action, Kind: CodeActionKind.QuickFix, DiagnosticId: (string?)f.DiagnosticId))
            .Concat(refactorings
                .OrderByDescending(r => r.Priority)
                .Select(r => (Action: r, Kind: CodeActionKind.Refactoring, DiagnosticId: (string?)null)));

        var request = Interlocked.Increment(ref _lastRequest);
        var pending = new Dictionary<string, PendingAction>(StringComparer.Ordinal);
        var items = new List<CodeActionItem>();
        var seen = new HashSet<(string? Group, string Title)>();
        foreach (var (action, kind, diagnosticId) in found)
        {
            foreach (var (leaf, group) in Flatten(action, group: null, depth: 0))
            {
                if (leaf is CodeActionWithOptions || !seen.Add((group, leaf.Title)))
                {
                    continue;
                }

                var id = string.Create(CultureInfo.InvariantCulture, $"{request}:{items.Count}");
                pending[id] = new PendingAction(document.Project.Solution, leaf);
                items.Add(new CodeActionItem(id, leaf.Title, group, kind, diagnosticId));
            }
        }

        lock (_gate)
        {
            _pending = pending;
        }

        return items;
    }

    /// <summary>Computes what a code action changes.</summary>
    /// <param name="id">The <see cref="CodeActionItem.Id"/> from the last <see cref="GetCodeActionsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    /// The edits for every affected file, with offsets into the text the actions were listed for. Empty when the
    /// action is unknown, from an older request, or fails.
    /// </returns>
    public async Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        PendingAction? pending;
        lock (_gate)
        {
            _pending.TryGetValue(id, out pending);
        }

        if (pending is null)
        {
            return [];
        }

        ImmutableArray<CodeActionOperation> operations;
#pragma warning disable CA1031 // A failing code action must not take the editor down; it simply changes nothing.
        try
        {
            operations = await pending.Action.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return [];
        }
#pragma warning restore CA1031

        foreach (var operation in operations)
        {
            if (operation is ApplyChangesOperation apply)
            {
                return await SolutionEdits.GetDocumentEditsAsync(pending.Solution, apply.ChangedSolution, cancellationToken).ConfigureAwait(false);
            }
        }

        return [];
    }

    /// <summary>
    /// The diagnostics a fix can be offered for: those at the selection or caret, or, when there are none at the
    /// caret, those on its line. Errors come first.
    /// </summary>
    private static List<Diagnostic> DiagnosticsAt(SemanticModel model, SourceText text, TextSpan span, CancellationToken cancellationToken)
    {
        var all = model.GetDiagnostics(cancellationToken: cancellationToken)
            .Where(d => d.Location.IsInSource)
            .ToList();

        var matching = all
            .Where(d => span.IsEmpty ? d.Location.SourceSpan.IntersectsWith(span.Start) : d.Location.SourceSpan.IntersectsWith(span))
            .ToList();

        if (matching.Count == 0 && span.IsEmpty)
        {
            var line = text.Lines.GetLineFromPosition(span.Start).Span;
            matching = all.Where(d => d.Location.SourceSpan.IntersectsWith(line)).ToList();
        }

        return matching.OrderByDescending(d => d.Severity).ToList();
    }

#pragma warning disable CA1031 // A provider that throws is skipped, whatever it throws; the other providers still answer.
    private static async Task<List<CodeAction>> GetFixesAsync(Document document, Diagnostic diagnostic, CodeActionProviderCatalog catalog, CancellationToken cancellationToken)
    {
        var actions = new List<CodeAction>();
        foreach (var provider in catalog.GetFixProviders(document.Project.Language, diagnostic.Id))
        {
            var registered = new List<CodeAction>();
            var context = new CodeFixContext(
                document,
                diagnostic.Location.SourceSpan,
                [diagnostic],
                (action, _) =>
                {
                    lock (registered)
                    {
                        registered.Add(action);
                    }
                },
                cancellationToken);

            try
            {
                await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                continue;
            }

            actions.AddRange(registered);
        }

        return actions;
    }

    private static async Task<List<CodeAction>> GetRefactoringsAsync(Document document, TextSpan span, CodeActionProviderCatalog catalog, CancellationToken cancellationToken)
    {
        var providers = catalog.GetRefactorings(document.Project.Language);
        var results = new List<CodeAction>?[providers.Count];
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
        };

        await Parallel.ForEachAsync(Enumerable.Range(0, providers.Count), options, async (index, token) =>
        {
            var registered = new List<CodeAction>();
            var context = new CodeRefactoringContext(
                document,
                span,
                action =>
                {
                    lock (registered)
                    {
                        registered.Add(action);
                    }
                },
                token);

            try
            {
                await providers[index].ComputeRefactoringsAsync(context).ConfigureAwait(false);
                results[index] = registered;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
            {
                results[index] = null;
            }
        }).ConfigureAwait(false);

        return results.OfType<List<CodeAction>>().SelectMany(r => r).ToList();
    }
#pragma warning restore CA1031

    /// <summary>Expands submenus into their entries, each with the title of the submenu it is in.</summary>
    private static IEnumerable<(CodeAction Action, string? Group)> Flatten(CodeAction action, string? group, int depth)
    {
        var nested = action.NestedActions;
        if (nested.IsDefaultOrEmpty || depth >= MaxNestingDepth)
        {
            yield return (action, group);
            yield break;
        }

        var childGroup = group is null ? action.Title : group + " › " + action.Title;
        foreach (var child in nested)
        {
            foreach (var leaf in Flatten(child, childGroup, depth + 1))
            {
                yield return leaf;
            }
        }
    }

    /// <summary>A listed action and the solution it was computed for.</summary>
    private sealed record PendingAction(Solution Solution, CodeAction Action);
}
