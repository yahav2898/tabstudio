using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Core.Diagnostics;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation.Uno;

/// <summary>
/// Places diagnostics in the code Uno's XAML generator wrote in the XAML they came from. The generator does not write
/// <c>#line</c> directives; before the code for each element it writes <c>// Source ../../../MainPage.xaml (Line 6:4)</c>,
/// relative to the intermediate output folder, and it passes each element's file, line and column to
/// <c>FrameworkElementHelper.SetBaseUri</c>. The nearest of those before a diagnostic is the element it is about.
/// </summary>
internal sealed partial class UnoXamlSourceMap
{
    private readonly TsProject _project;
    private readonly IReadOnlyDictionary<string, SourceText> _xaml;

    /// <summary>Creates a map for a project.</summary>
    /// <param name="project">The project.</param>
    /// <param name="xaml">The current text of each of its XAML files, by full path.</param>
    public UnoXamlSourceMap(TsProject project, IReadOnlyDictionary<string, SourceText> xaml)
    {
        _project = project;
        _xaml = xaml;
    }

    /// <summary>Converts a diagnostic, in its XAML file when it is in the generator's code for one.</summary>
    /// <param name="diagnostic">A diagnostic of the project's compilation or generators.</param>
    public BuildDiagnostic Map(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        var mapped = DiagnosticMapper.Map(diagnostic, _project.Name);
        if (!diagnostic.Location.IsInSource || diagnostic.Location.SourceTree is not { } tree || !_xaml.Keys.Any())
        {
            return mapped;
        }

        // The generator reports XAML errors at their place in the XAML file already.
        if (_xaml.ContainsKey(tree.FilePath))
        {
            return mapped;
        }

        if (Find(tree, diagnostic.Location.SourceSpan.Start) is not { } source
            || !_xaml.TryGetValue(source.Path, out var xaml))
        {
            return mapped;
        }

        var line = Math.Clamp(source.Line - 1, 0, Math.Max(0, xaml.Lines.Count - 1));
        var lineText = xaml.Lines[line];
        var column = Math.Clamp(source.Column - 1, 0, lineText.Span.Length);
        var start = lineText.Start + column;
        var length = ElementNameLength(xaml, start);
        return mapped with { Location = new DiagnosticLocation(source.Path, start, length, line + 1, column + 1) };
    }

    /// <summary>
    /// The XAML place of the element whose code a position is in: the <c>SetBaseUri</c> call of the method that applies
    /// the element's properties, or the <c>// Source</c> comment among the members of the initializer that creates it;
    /// failing both, the last one written before the position.
    /// </summary>
    private (string Path, int Line, int Column)? Find(SyntaxTree tree, int position)
    {
        var root = tree.GetRoot();
        var node = root.FindNode(new TextSpan(Math.Clamp(position, 0, Math.Max(0, root.FullSpan.End - 1)), 0), getInnermostNodeForTie: true);
        // Uno puts event callbacks in nested helper classes, away from the element's ApplyTo method.
        // Follow the helper's construction back to that method, whose SetBaseUri names the original element.
        if (node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.ValueText.EndsWith("_Handler", StringComparison.Ordinal)) is { } handler
            && root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().FirstOrDefault(c =>
                c.Type.ToString().EndsWith("." + handler.Identifier.ValueText, StringComparison.Ordinal)) is { } creation)
        {
            return Find(tree, creation.SpanStart);
        }

        foreach (var ancestor in node.AncestorsAndSelf())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax method when BaseUriCall().Match(method.ToString()) is { Success: true } call:
                    return Parse(call);

                case ObjectCreationExpressionSyntax { Initializer: { } initializer }:
                    var trivia = initializer.Expressions.SelectMany(e => e.GetLeadingTrivia()).Concat(initializer.CloseBraceToken.LeadingTrivia);
                    foreach (var comment in trivia.Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)))
                    {
                        if (SourceComment().Match(comment.ToString()) is { Success: true } source)
                        {
                            return Parse(source);
                        }
                    }

                    break;
            }
        }

        var before = tree.GetText().ToString(new TextSpan(0, Math.Clamp(position, 0, tree.Length)));
        var comments = SourceComment().Matches(before);
        var calls = BaseUriCall().Matches(before);
        var last = comments.Count == 0 ? (calls.Count == 0 ? null : calls[^1])
            : calls.Count == 0 || comments[^1].Index > calls[^1].Index ? comments[^1] : calls[^1];
        return last is null ? null : Parse(last);
    }

    private (string Path, int Line, int Column) Parse(Match match)
    {
        var path = match.Groups["path"].Value;
        var resolved = path.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            ? path["file://".Length..].Replace("//", "/", StringComparison.Ordinal)
            : Path.GetFullPath(Path.Combine(_project.DirectoryPath, "obj", "Debug", UnoBuild.TargetFramework, path.Replace('\\', Path.DirectorySeparatorChar)));
        return (resolved,
            int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["column"].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>The length of the element name at a position, such as <c>TitleBar</c> after <c>&lt;</c>.</summary>
    private static int ElementNameLength(SourceText xaml, int start)
    {
        var end = start;
        while (end < xaml.Length && !char.IsWhiteSpace(xaml[end]) && xaml[end] is not ('>' or '/'))
        {
            end++;
        }

        return Math.Max(1, end - start);
    }

    [GeneratedRegex(@"// Source (?<path>[^\r\n]+?) \(Line (?<line>\d+):(?<column>\d+)\)")]
    private static partial Regex SourceComment();

    [GeneratedRegex(@"SetBaseUri\([^;]*?""(?<path>file://[^""]+)"",\s*(?<line>\d+),\s*(?<column>\d+)\)")]
    private static partial Regex BaseUriCall();
}
