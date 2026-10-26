using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Porting.Output;

namespace TabStudio.Porting.CSharp;

/// <summary>
/// A C# file being ported. Rewriters change its syntax tree; spots that need a person or Claude are marked with
/// annotations, and when the file is written they become <c>TODO(port)</c> comments, the code that could not be
/// ported commented out beneath them, so the file still compiles.
/// </summary>
internal sealed class CodeFile
{
    /// <summary>The kind of the TODO annotations.</summary>
    public const string TodoKind = "TabStudio.Porting.Todo";

    /// <summary>The kind of the annotations of code to comment out.</summary>
    public const string CommentOutKind = "TabStudio.Porting.CommentOut";

    private readonly Dictionary<string, (string What, string Why)> _todos = new(StringComparer.Ordinal);
    private int _next;

    /// <summary>Parses a file.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="report">Its report in the new project.</param>
    public CodeFile(string text, FileReport report)
    {
        Report = report;
        Root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text, Options).GetRoot();
        Newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    }

    /// <summary>The C# version of ported code: the newest, as in the Avalonia template's .NET.</summary>
    public static CSharpParseOptions Options { get; } = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    /// <summary>The file's syntax, which the rewriters replace.</summary>
    public CompilationUnitSyntax Root { get; set; }

    /// <summary>The file's report.</summary>
    public FileReport Report { get; }

    /// <summary>The file's line ending.</summary>
    public string Newline { get; }

    /// <summary>Marks a node for a <c>TODO(port)</c> comment above it, and a port note.</summary>
    /// <param name="node">The node.</param>
    /// <param name="what">What is there.</param>
    /// <param name="why">What to do.</param>
    /// <returns>The node with the annotation, to put in the tree.</returns>
    public T Todo<T>(T node, string what, string why) where T : SyntaxNode => node.WithAdditionalAnnotations(Annotation(TodoKind, what, why));

    /// <summary>Marks a statement or member to comment out under a <c>TODO(port)</c> comment, with a port note.</summary>
    public T CommentOut<T>(T node, string what, string why) where T : SyntaxNode => node.WithAdditionalAnnotations(Annotation(CommentOutKind, what, why));

    /// <summary>Whether a node is marked to be commented out, itself or inside a marked node.</summary>
    public static bool IsCommentedOut(SyntaxNode node) => node.AncestorsAndSelf().Any(n => n.HasAnnotations(CommentOutKind));

    /// <summary>
    /// The file's final text: the TODO comments written, and the marked code commented out. The port notes get the
    /// lines of their TODO comments.
    /// </summary>
    public string Finish()
    {
        var text = SourceText.From(Root.ToFullString());
        var edits = new List<Edit>();
        var nested = new List<(Edit Outer, string What, string Why)>();
        foreach (var kind in (string[])[CommentOutKind, TodoKind])
        {
            foreach (var node in Root.GetAnnotatedNodes(kind))
            {
                var annotation = node.GetAnnotations(kind).First();
                var (what, why) = _todos[annotation.Data!];
                var first = text.Lines.GetLineFromPosition(node.SpanStart).LineNumber;
                var last = kind == TodoKind ? first - 1 : text.Lines.GetLineFromPosition(Math.Max(node.Span.End - 1, node.SpanStart)).LineNumber;

                // A spot inside code that is commented out anyway is told at that comment.
                if (edits.Find(e => e.Last >= e.First && first >= e.First && first <= e.Last) is { } outer)
                {
                    nested.Add((outer, what, why));
                    continue;
                }

                var indent = Indentation(text.Lines[first].ToString());
                var lines = new List<string> { $"{indent}// TODO(port): {what.TrimEnd('.')}. {why.TrimEnd('.')}." };
                for (var line = first; line <= last; line++)
                {
                    var original = text.Lines[line].ToString();
                    lines.Add(original.Trim().Length == 0 ? indent + "//"
                        : indent + "// " + (original.StartsWith(indent, StringComparison.Ordinal) ? original[indent.Length..] : original.TrimStart()));
                }

                edits.Add(new Edit(first, last, lines, what, why));
            }
        }

        // The notes' lines in the final file: each edit moves the lines below it.
        edits.Sort((a, b) => a.First != b.First ? a.First.CompareTo(b.First) : b.Last.CompareTo(a.Last));
        var shift = 0;
        foreach (var edit in edits)
        {
            edit.FinalLine = edit.First + shift + 1;
            Report.Note(edit.FinalLine, edit.What, edit.Why);
            shift += edit.Lines.Count - (edit.Last - edit.First + 1);
        }

        foreach (var (outer, what, why) in nested)
        {
            Report.Note(outer.FinalLine, what, why);
        }

        var output = new List<string>();
        var index = 0;
        var next = 0;
        while (index < text.Lines.Count)
        {
            if (next < edits.Count && edits[next].First == index)
            {
                output.AddRange(edits[next].Lines);
                if (edits[next].Last >= edits[next].First)
                {
                    index = edits[next].Last + 1;
                }

                next++;
                continue;
            }

            output.Add(text.Lines[index].ToString());
            index++;
        }

        return string.Join(Newline, output);
    }

    private SyntaxAnnotation Annotation(string kind, string what, string why)
    {
        var id = (_next++).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _todos[id] = (what, why);
        return new SyntaxAnnotation(kind, id);
    }

    private static string Indentation(string line) => line[..(line.Length - line.TrimStart().Length)];

    /// <summary>A change to the file's lines: lines replaced by commented ones, or a TODO comment inserted.</summary>
    /// <param name="First">The first line it replaces, or the line it goes above.</param>
    /// <param name="Last">The last line it replaces, or <c>First - 1</c> for an insertion.</param>
    /// <param name="Lines">The new lines.</param>
    /// <param name="What">The note's what.</param>
    /// <param name="Why">The note's why.</param>
    private sealed record Edit(int First, int Last, List<string> Lines, string What, string Why)
    {
        /// <summary>The 1-based line of the TODO comment in the final file.</summary>
        public int FinalLine { get; set; }
    }
}
