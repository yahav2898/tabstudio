using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace TabStudio.Compilation.Features;

/// <summary>
/// The parts of a symbol's XML documentation comment that signature help shows, as plain text.
/// </summary>
/// <remarks>
/// Only symbols declared in source have documentation: the .NET reference assemblies TabStudio compiles against
/// carry no XML documentation files.
/// </remarks>
internal sealed class DocumentationComment
{
    /// <summary>Where the simple name in a <c>cref</c> ends: before its parameters or its generic arity.</summary>
    private static readonly char[] ReferenceNameEnd = ['(', '`'];

    private readonly Dictionary<string, string> _parameters;

    private DocumentationComment(string summary, Dictionary<string, string> parameters)
    {
        Summary = summary;
        _parameters = parameters;
    }

    /// <summary>A symbol without documentation.</summary>
    public static DocumentationComment Empty { get; } = new(string.Empty, new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>The <c>&lt;summary&gt;</c> text, or an empty string.</summary>
    public string Summary { get; }

    /// <summary>Reads the documentation of a symbol.</summary>
    public static DocumentationComment For(ISymbol symbol, CancellationToken cancellationToken)
    {
        var target = symbol is IMethodSymbol { ReducedFrom: { } reducedFrom } ? reducedFrom : symbol.OriginalDefinition;
        return Parse(target.GetDocumentationCommentXml(cancellationToken: cancellationToken));
    }

    /// <summary>Parses documentation XML as Roslyn returns it: a <c>&lt;member&gt;</c> element.</summary>
    public static DocumentationComment Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Empty;
        }

        XElement root;
        try
        {
            root = XElement.Parse("<doc>" + xml + "</doc>", LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return Empty;
        }

        var member = root.Element("member") ?? root;
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in member.Elements("param"))
        {
            if (parameter.Attribute("name")?.Value is { Length: > 0 } name)
            {
                parameters[name] = Render(parameter);
            }
        }

        return new DocumentationComment(Render(member.Element("summary")), parameters);
    }

    /// <summary>The <c>&lt;param&gt;</c> text of a parameter, or an empty string.</summary>
    public string ParameterText(string name) => _parameters.GetValueOrDefault(name, string.Empty);

    private static string Render(XElement? element)
    {
        if (element is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        AppendNodes(element, builder);
        return CollapseWhitespace(builder.ToString());
    }

    private static void AppendNodes(XElement element, StringBuilder builder)
    {
        foreach (var node in element.Nodes())
        {
            if (node is XText text)
            {
                builder.Append(text.Value);
            }
            else if (node is XElement child)
            {
                switch (child.Name.LocalName)
                {
                    case "see" or "seealso":
                        builder.Append(ReferenceText(child));
                        break;
                    case "paramref" or "typeparamref":
                        builder.Append(child.Attribute("name")?.Value);
                        break;
                    case "para" or "br":
                        builder.Append(' ');
                        AppendNodes(child, builder);
                        builder.Append(' ');
                        break;
                    default:
                        AppendNodes(child, builder);
                        break;
                }
            }
        }
    }

    /// <summary>The text of a <c>&lt;see&gt;</c>: its own text, its keyword, or the simple name of its target.</summary>
    private static string ReferenceText(XElement reference)
    {
        if (reference.Attribute("langword")?.Value is { Length: > 0 } keyword)
        {
            return keyword;
        }

        if (!reference.IsEmpty && reference.Value.Length > 0)
        {
            return reference.Value;
        }

        var target = reference.Attribute("cref")?.Value ?? reference.Attribute("href")?.Value ?? string.Empty;
        if (target.Length > 2 && target[1] == ':')
        {
            target = target[2..];
        }

        var end = target.IndexOfAny(ReferenceNameEnd);
        if (end >= 0)
        {
            target = target[..end];
        }

        var lastDot = target.LastIndexOf('.');
        return lastDot >= 0 ? target[(lastDot + 1)..] : target;
    }

    private static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
