using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace TabStudio.App.Previews;

/// <summary>
/// Expands local style and resource includes from the current build. Avalonia's runtime compiler otherwise looks
/// for the post-build !AvaloniaResources type, which an in-process Roslyn build does not emit.
/// </summary>
internal static class RuntimeXamlIncludes
{
    public static string Expand(string xaml, Uri address, Func<Uri, string?> read)
    {
        var document = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);
        ExpandDocument(document, address, read, new HashSet<string>(StringComparer.Ordinal) { address.AbsoluteUri });
        HoistNamespaces(document);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static void HoistNamespaces(XDocument document)
    {
        var root = document.Root!;
        // Avalonia's runtime parser accepts namespace declarations only on the outermost element. Included
        // dictionaries may use the same prefix for different CLR namespaces; remap their selectors and markup.
        foreach (var element in root.Descendants().Reverse().ToList())
        {
            foreach (var declaration in element.Attributes().Where(a => a.IsNamespaceDeclaration).ToList())
            {
                var prefix = declaration.Name.LocalName == "xmlns" ? string.Empty : declaration.Name.LocalName;
                var ns = XNamespace.Get(declaration.Value);
                var existing = root.GetPrefixOfNamespace(ns);
                var assigned = existing;
                if (assigned is null)
                {
                    assigned = prefix;
                    if (assigned.Length == 0 || root.GetNamespaceOfPrefix(assigned) is not null)
                    {
                        var index = 1;
                        do
                        {
                            assigned = "include" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            index++;
                        }
                        while (root.GetNamespaceOfPrefix(assigned) is not null);
                    }

                    root.Add(new XAttribute(XNamespace.Xmlns + assigned, ns.NamespaceName));
                }

                if (prefix.Length > 0 && assigned != prefix)
                {
                    foreach (var attribute in element.DescendantsAndSelf().Attributes().Where(a => !a.IsNamespaceDeclaration))
                    {
                        attribute.Value = Regex.Replace(attribute.Value, @"(?<![\w])" + Regex.Escape(prefix) + @"(?=[:|])", assigned);
                    }
                }

                declaration.Remove();
            }
        }
    }

    private static void ExpandDocument(XDocument document, Uri address, Func<Uri, string?> read, HashSet<string> chain)
    {
        foreach (var include in document.Descendants().Where(e => e.Name.NamespaceName == "https://github.com/avaloniaui"
            && e.Name.LocalName is "StyleInclude" or "ResourceInclude" or "MergeResourceInclude").ToList())
        {
            if (include.Attribute("Source") is not { } source || source.Value.StartsWith('{'))
            {
                continue;
            }

            var location = new Uri(address, source.Value);
            if (read(location) is not { } text)
            {
                continue;
            }

            if (!chain.Add(location.AbsoluteUri))
            {
                throw new InvalidOperationException("Circular XAML include: " + location.AbsoluteUri);
            }

            var nested = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            ExpandDocument(nested, location, read, chain);
            chain.Remove(location.AbsoluteUri);
            if (nested.Root is { } root)
            {
                // Asset paths in the included file resolve against that file, even after it is nested in App.axaml.
                foreach (var attribute in root.DescendantsAndSelf().Attributes().Where(a => a.Name.LocalName == "Source"
                    && !a.Value.StartsWith('{') && !Uri.TryCreate(a.Value, UriKind.Absolute, out _)))
                {
                    attribute.Value = new Uri(location, attribute.Value).AbsoluteUri;
                }

                include.ReplaceWith(root);
            }
        }
    }
}
