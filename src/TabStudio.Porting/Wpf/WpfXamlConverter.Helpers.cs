using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Wpf;

/// <summary>Helpers of the XAML converter: notes, attributes kept on their lines, names and URIs.</summary>
internal sealed partial class WpfXamlConverter
{
    private static readonly char[] PointSeparators = [',', ' '];

    /// <summary>Adds a <c>TODO(port)</c> comment in front of an element, and a port note pointing at it.</summary>
    /// <param name="element">The element the note is about.</param>
    /// <param name="what">What is there.</param>
    /// <param name="why">Why it was not converted, and what would finish it.</param>
    /// <param name="original">The original markup, kept in the comment, for a removed element or attribute.</param>
    private void Note(XElement element, string what, string why, string? original = null)
    {
        var text = $" TODO(port): {what.TrimEnd('.')}. {why.TrimEnd('.')}." + (original is null ? " " : $" Was: {original} ");
        var comment = new XComment(text.Replace("--", "- -", StringComparison.Ordinal));
        element.AddBeforeSelf(comment);
        _report.Note(comment, what, why);
    }

    /// <summary>Replaces an attribute in place with others, which are written on the line it was on.</summary>
    private static void ReplaceAttribute(XElement element, XAttribute old, params XAttribute[] replacements)
    {
        var line = ((IXmlLineInfo)old).HasLineInfo() ? ((IXmlLineInfo)old).LineNumber : old.Annotation<AttributeLine>()?.Line;
        var attributes = element.Attributes().ToList();
        var index = attributes.IndexOf(old);
        if (index < 0)
        {
            return;
        }

        foreach (var replacement in replacements)
        {
            if (line is { } number && replacement.Annotation<AttributeLine>() is null)
            {
                replacement.AddAnnotation(new AttributeLine(number));
            }
        }

        attributes.RemoveAt(index);
        attributes.InsertRange(index, replacements.Where(r => attributes.All(a => a.Name != r.Name)));
        element.ReplaceAttributes(attributes);
    }

    /// <summary>Renames an attribute, keeping its value and place.</summary>
    private static XAttribute Rename(XElement element, XAttribute attribute, XName name, string? value = null)
    {
        var renamed = new XAttribute(name, value ?? attribute.Value);
        ReplaceAttribute(element, attribute, renamed);
        return renamed;
    }

    /// <summary>Sets an attribute, adding it at the end when it is missing.</summary>
    private static void Set(XElement element, string name, string value)
    {
        if (element.Attribute(name) is { } existing)
        {
            existing.Value = value;
        }
        else
        {
            element.Add(new XAttribute(name, value));
        }
    }

    /// <summary>The value of an attribute without a namespace, or <see langword="null"/>.</summary>
    private static string? Value(XElement element, string name) => element.Attribute(name)?.Value;

    private static bool IsTrue(string? value) => string.Equals(value?.Trim(), "True", StringComparison.OrdinalIgnoreCase);

    /// <summary>An element's markup as one would read it in a comment: without namespace declarations.</summary>
    private static string Original(XElement element)
    {
        var text = element.ToString(SaveOptions.DisableFormatting);
        return XmlnsDeclaration().Replace(text, string.Empty);
    }

    /// <summary>An attribute as it was written.</summary>
    private static string Original(XAttribute attribute) => $"{attribute.Name.LocalName}=\"{attribute.Value}\"";

    /// <summary>The property element <c>Owner.Property</c> of an element, created after <paramref name="after"/> when missing.</summary>
    private static XElement PropertyElement(XElement owner, string property, XElement? after = null)
    {
        var name = owner.Name.Namespace + (owner.Name.LocalName + "." + property);
        if (owner.Element(name) is { } existing)
        {
            return existing;
        }

        var created = new XElement(name);
        if (after is not null && after.Parent == owner)
        {
            after.AddAfterSelf(created);
        }
        else if (owner.Elements().FirstOrDefault(e => !e.Name.LocalName.Contains('.', StringComparison.Ordinal)) is { } content)
        {
            content.AddBeforeSelf(created);
        }
        else
        {
            owner.Add(created);
        }

        return created;
    }

    /// <summary>The element's name, giving it one when code must reach it.</summary>
    private string EnsureName(XElement element)
    {
        var existing = element.Attribute(NameAttribute)?.Value ?? element.Attribute("Name")?.Value;
        if (existing is not null)
        {
            return existing;
        }

        _generatedNames++;
        var generated = char.ToLowerInvariant(element.Name.LocalName[0]) + element.Name.LocalName[1..] + _generatedNames.ToString(CultureInfo.InvariantCulture);
        element.Add(new XAttribute(NameAttribute, generated));
        return generated;
    }

    /// <summary>
    /// The Avalonia name of a type named in an attribute value, such as a <c>TargetType</c>: WPF's controls get
    /// Avalonia's names, a window becomes the port's <c>AppWindow</c>, and prefixed types stay as they are.
    /// </summary>
    private string MapTypeName(string name)
    {
        var type = WpfProject.TypeName(name);
        if (type.Contains(':', StringComparison.Ordinal))
        {
            return type;
        }

        if (type is "Window")
        {
            return SupportPrefixOf() + ":AppWindow";
        }

        return WpfElements.AvaloniaName(type) ?? type;
    }

    /// <summary>The prefix the support namespace has in this file.</summary>
    private string SupportPrefixOf() => _root.GetPrefixOfNamespace(Support) ?? SupportPrefix;

    /// <summary>
    /// An address in the app as Avalonia reads it: WPF's pack URIs (<c>pack://application:,,,/Images/a.png</c> and
    /// <c>/App;component/Images/a.png</c>) become <c>/Images/a.png</c>; addresses of another assembly use avares://.
    /// </summary>
    private string AppUri(string value)
    {
        var text = value.Trim();
        const string Pack = "pack://application:,,,";
        if (text.StartsWith(Pack, StringComparison.OrdinalIgnoreCase))
        {
            text = text[Pack.Length..];
        }

        var component = text.IndexOf(";component/", StringComparison.OrdinalIgnoreCase);
        if (component > 0)
        {
            var assembly = text[..component].TrimStart('/');
            var path = text[(component + ";component".Length)..];
            return assembly.Equals(_project.Context.Source.Name, StringComparison.OrdinalIgnoreCase) ? path : $"avares://{assembly}{path}";
        }

        return text;
    }

    /// <summary>The project path of a file another file names, as WPF resolves it: relative to the naming file.</summary>
    private string ResolvePath(string uri)
    {
        var text = AppUri(uri);
        if (text.StartsWith('/'))
        {
            return text.TrimStart('/');
        }

        var folder = Path.GetDirectoryName(_sourcePath)?.Replace('\\', '/') ?? string.Empty;
        var parts = folder.Length == 0 ? new List<string>() : folder.Split('/').ToList();
        foreach (var part in text.Split('/'))
        {
            if (part == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
            }
            else if (part is not ("." or ""))
            {
                parts.Add(part);
            }
        }

        return string.Join('/', parts);
    }

    /// <summary>A WPF relative point (<c>0.5,1</c>) as an Avalonia relative point (<c>50%,100%</c>).</summary>
    private static string? RelativePoint(string value)
    {
        var parts = value.Split(PointSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !TryNumber(parts[0], out var x) || !TryNumber(parts[1], out var y))
        {
            return null;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{x * 100:0.##}%,{y * 100:0.##}%");
    }

    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    [GeneratedRegex(@"\s+xmlns(:\w+)?=""[^""]*""")]
    private static partial Regex XmlnsDeclaration();
}
