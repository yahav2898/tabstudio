using System.Collections.Concurrent;
using System.Xml;
using Microsoft.CodeAnalysis;
using TabStudio.Compilation.Features;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.Compilation.Wpf.Editing;

/// <summary>
/// The summary of a type or member XAML names: from its documentation comment when it is in source, otherwise from
/// the XML documentation file beside its assembly, as NuGet packages such as OpenSilver ship them.
/// </summary>
internal static class XamlDocumentation
{
    /// <summary>A documentation file larger than this is not read.</summary>
    private const long MaxFileLength = 64L * 1024 * 1024;

    /// <summary>The summaries of each documentation file, by documentation id; <see langword="null"/> when there is no file.</summary>
    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, string>?>> Files = new(StringComparer.Ordinal);

    /// <summary>The summary of a symbol, or an empty string.</summary>
    public static string Summary(ISymbol symbol, RoslynCompilation compilation, CancellationToken cancellationToken)
    {
        var target = symbol.OriginalDefinition;
        var source = DocumentationComment.For(target, cancellationToken).Summary;
        if (source.Length > 0
            || target.ContainingAssembly is not { } assembly
            || compilation.GetMetadataReference(assembly) is not PortableExecutableReference { FilePath: { } path }
            || target.GetDocumentationCommentId() is not { } id)
        {
            return source;
        }

        var file = Files.GetOrAdd(Path.ChangeExtension(path, ".xml"), xml => new Lazy<IReadOnlyDictionary<string, string>?>(() => Read(xml))).Value;
        return file?.GetValueOrDefault(id) ?? string.Empty;
    }

    private static Dictionary<string, string>? Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileLength)
            {
                return null;
            }

            var summaries = new Dictionary<string, string>(StringComparer.Ordinal);
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreComments = true });
            reader.MoveToContent();
            while (!reader.EOF)
            {
                if (reader.NodeType != XmlNodeType.Element || reader.Name != "member")
                {
                    reader.Read();
                    continue;
                }

                // Reading the element moves past it, onto whatever follows, which may be the next member.
                var name = reader.GetAttribute("name");
                var summary = DocumentationComment.Parse(reader.ReadOuterXml()).Summary;
                if (name is { Length: > 0 } && summary.Length > 0)
                {
                    summaries[name] = summary;
                }
            }

            return summaries;
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
