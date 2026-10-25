using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Notebooks.Execution;

/// <summary>A <c>#r</c> reference in a cell and where it is.</summary>
/// <param name="Reference">What the line asks for.</param>
/// <param name="Location">The line, for diagnostics.</param>
internal sealed record CellReference(NotebookReference Reference, DiagnosticLocation Location);

/// <summary>A cell's source with its directives taken out, and what they asked for.</summary>
/// <param name="Source">
/// The source with every directive line the kernel handles replaced by spaces, so that positions in the code
/// are unchanged.
/// </param>
/// <param name="References">The <c>#r</c> references, in order.</param>
/// <param name="Diagnostics">Errors for directives that are not supported.</param>
internal sealed record CellDirectives(string Source, List<CellReference> References, List<BuildDiagnostic> Diagnostics);

/// <summary>
/// Finds the lines of a cell that are for the kernel rather than the C# compiler: <c>#r</c>, <c>#load</c> and
/// Polyglot Notebooks <c>#!</c> magic commands.
/// </summary>
/// <remarks>
/// The cell is parsed with Roslyn, so only real directives count: a line that starts with <c>#r</c> inside a
/// string literal is left alone. Directive lines are blanked out rather than removed, which keeps every line
/// and column of the remaining code where it was.
/// </remarks>
internal static class CellDirectiveParser
{
    private const string NuGetPrefix = "nuget:";
    private const string MagicPrefix = "#!";

    /// <summary>Polyglot Notebooks' <c>#!time</c>, harmless here because every run reports its duration.</summary>
    private const string TimeMagic = "time";

    private static readonly char[] MagicSeparators = [' ', '\t'];

    public static CellDirectives Parse(string source, string path, CSharpParseOptions options, CancellationToken cancellationToken)
    {
        var text = SourceText.From(source);
        var root = CSharpSyntaxTree.ParseText(text, options, path, cancellationToken).GetCompilationUnitRoot(cancellationToken);

        var references = new List<CellReference>();
        var diagnostics = new List<BuildDiagnostic>();
        char[]? blanked = null;

        for (var directive = root.GetFirstDirective(); directive is not null; directive = directive.GetNextDirective())
        {
            var line = text.Lines.GetLineFromPosition(directive.SpanStart);
            var span = TextSpan.FromBounds(directive.SpanStart, line.End);
            var location = new DiagnosticLocation(path, span.Start, span.Length, line.LineNumber + 1, span.Start - line.Start + 1);
            var directiveText = text.ToString(span);

            switch (directive)
            {
                case ReferenceDirectiveTriviaSyntax reference:
                    references.Add(new CellReference(ParseReference(reference.File.ValueText), location));
                    break;

                case LoadDirectiveTriviaSyntax:
                    diagnostics.Add(Error(
                        NotebookDiagnosticIds.LoadNotSupported,
                        "#load is not supported in TabStudio notebooks yet. Paste the script into a cell instead.",
                        location));
                    break;

                case ShebangDirectiveTriviaSyntax or BadDirectiveTriviaSyntax when directiveText.StartsWith(MagicPrefix, StringComparison.Ordinal):
                    if (CheckMagicCommand(directiveText, location) is { } diagnostic)
                    {
                        diagnostics.Add(diagnostic);
                    }

                    break;

                default:
                    // #if, #region, #nullable and friends are for the compiler.
                    continue;
            }

            blanked ??= source.ToCharArray();
            Array.Fill(blanked, ' ', span.Start, span.Length);
        }

        return new CellDirectives(blanked is null ? source : new string(blanked), references, diagnostics);
    }

    private static NotebookReference ParseReference(string value)
    {
        if (!value.StartsWith(NuGetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new NotebookReference(NotebookReferenceKind.AssemblyFile, value.Trim());
        }

        var parts = value[NuGetPrefix.Length..].Split(',', 2);
        var version = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return new NotebookReference(NotebookReferenceKind.NuGetPackage, parts[0].Trim(), version.Length > 0 ? version : null);
    }

    private static BuildDiagnostic? CheckMagicCommand(string directiveText, DiagnosticLocation location)
    {
        var name = directiveText[MagicPrefix.Length..].Split(MagicSeparators, 2)[0];
        if (NotebookLanguages.IsCSharp(name) || name == TimeMagic)
        {
            return null;
        }

        return NotebookLanguages.TryNormalize(name, out var language)
            ? Error(
                NotebookDiagnosticIds.LanguageNotSupported,
                $"'#!{name}' switches to {NotebookLanguages.GetDisplayName(language)}, but TabStudio notebooks run only C# for now.",
                location)
            : Error(
                NotebookDiagnosticIds.MagicCommandNotSupported,
                $"The magic command '#!{name}' is not supported in TabStudio notebooks yet.",
                location);
    }

    private static BuildDiagnostic Error(string id, string message, DiagnosticLocation location) =>
        new(id, DiagnosticSeverityLevel.Error, message, location);
}
