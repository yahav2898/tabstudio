using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TabStudio.Compilation.Razor;

/// <summary>
/// Connects a Razor file with the C# the Razor compiler generates for it. The generated file starts with
/// <c>#pragma checksum "&lt;razor file&gt;"</c>, and every piece of the user's C# in it, such as an expression or an
/// <c>@code</c> block, sits under a <c>#line (line,column)-(line,column) "&lt;razor file&gt;"</c> directive and is copied
/// verbatim. Roslyn exposes those directives as line mappings, which this class turns into offsets both ways.
/// </summary>
internal static class RazorSourceMap
{
    private const string ChecksumPragma = "#pragma checksum \"";

    private static readonly ConditionalWeakTable<SyntaxTree, SourceFileBox> SourceFiles = new();

    /// <summary>The Razor file a generated tree was compiled from, or <see langword="null"/> when it is not Razor's.</summary>
    /// <param name="tree">A syntax tree of a compilation.</param>
    public static string? SourceFileOf(SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return SourceFiles.GetValue(tree, static t => new SourceFileBox(ReadSourceFile(t))).Path;
    }

    /// <summary>
    /// Maps an offset in a Razor file to the generated C#, when it is inside a piece of C# the file contains.
    /// </summary>
    /// <param name="generated">The generated tree.</param>
    /// <param name="razorPath">The Razor file.</param>
    /// <param name="razorText">Its text.</param>
    /// <param name="razorPosition">The offset in the Razor file.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The offset in the generated tree, or <see langword="null"/> when the position is markup.</returns>
    public static int? ToGenerated(SyntaxTree generated, string razorPath, SourceText razorText, int razorPosition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generated);
        var generatedText = generated.GetText(cancellationToken);
        foreach (var region in Regions(generated, generatedText, razorPath, razorText, cancellationToken))
        {
            if (razorPosition >= region.RazorStart && razorPosition <= region.RazorStart + region.Length)
            {
                return region.GeneratedStart + (razorPosition - region.RazorStart);
            }
        }

        return null;
    }

    /// <summary>Maps a span of generated C# back to the Razor file, when it is inside a piece of the file's C#.</summary>
    /// <param name="generated">The generated tree.</param>
    /// <param name="razorPath">The Razor file.</param>
    /// <param name="razorText">Its text.</param>
    /// <param name="span">The span in the generated tree.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The span in the Razor file, or <see langword="null"/> when the span is code the compiler wrote.</returns>
    public static TextSpan? ToRazor(SyntaxTree generated, string razorPath, SourceText razorText, TextSpan span, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generated);
        var generatedText = generated.GetText(cancellationToken);
        foreach (var region in Regions(generated, generatedText, razorPath, razorText, cancellationToken))
        {
            if (span.Start >= region.GeneratedStart && span.End <= region.GeneratedStart + region.Length)
            {
                return new TextSpan(region.RazorStart + (span.Start - region.GeneratedStart), span.Length);
            }
        }

        return null;
    }

    /// <summary>The pieces of a Razor file's C# in a generated tree: where each starts in both files, and its length.</summary>
    private static IEnumerable<MappedRegion> Regions(SyntaxTree generated, SourceText generatedText, string razorPath, SourceText razorText, CancellationToken cancellationToken)
    {
        foreach (var mapping in generated.GetLineMappings(cancellationToken))
        {
            if (mapping.IsHidden || !mapping.MappedSpan.HasMappedPath
                || !string.Equals(Normalize(mapping.MappedSpan.Path), Normalize(razorPath), StringComparison.Ordinal))
            {
                continue;
            }

            var razorStart = Position(razorText, mapping.MappedSpan.StartLinePosition);
            var razorEnd = Position(razorText, mapping.MappedSpan.EndLinePosition);
            var generatedStart = Position(generatedText, new LinePosition(mapping.Span.Start.Line, mapping.CharacterOffset ?? mapping.Span.Start.Character));
            var length = Math.Min(razorEnd - razorStart, generatedText.Length - generatedStart);
            if (length >= 0)
            {
                yield return new MappedRegion(razorStart, generatedStart, length);
            }
        }
    }

    /// <summary>An offset for a line and column, clamped to the text.</summary>
    private static int Position(SourceText text, LinePosition position)
    {
        if (position.Line >= text.Lines.Count)
        {
            return text.Length;
        }

        var line = text.Lines[Math.Max(0, position.Line)];
        return Math.Min(line.Start + Math.Max(0, position.Character), line.EndIncludingLineBreak);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    /// <summary>Reads the path from the <c>#pragma checksum</c> line that starts every file the Razor compiler writes.</summary>
    private static string? ReadSourceFile(SyntaxTree tree)
    {
        var text = tree.GetText();
        if (text.Lines.Count == 0)
        {
            return null;
        }

        var first = text.Lines[0].ToString().TrimStart('﻿', ' ');
        if (!first.StartsWith(ChecksumPragma, StringComparison.Ordinal))
        {
            return null;
        }

        var end = first.IndexOf('"', ChecksumPragma.Length);
        if (end < 0)
        {
            return null;
        }

        var path = first[ChecksumPragma.Length..end].Replace(@"\\", @"\", StringComparison.Ordinal);
        return RazorProjects.IsRazorFile(path) ? path : null;
    }

    /// <summary>A piece of C# from the Razor file, at <see cref="RazorStart"/> there and <see cref="GeneratedStart"/> in the generated file.</summary>
    private readonly record struct MappedRegion(int RazorStart, int GeneratedStart, int Length);

    /// <summary>Holds a tree's source file in the weak table, including "none".</summary>
    private sealed class SourceFileBox(string? path)
    {
        public string? Path { get; } = path;
    }
}
