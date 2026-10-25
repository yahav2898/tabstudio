using System.Globalization;
using System.Xml;

namespace TabStudio.Extensions.Themes;

/// <summary>
/// Reads an XML property list, the format of TextMate's <c>.tmTheme</c> and <c>.tmLanguage</c> files, into plain values:
/// dictionaries, lists, strings, numbers and Booleans.
/// </summary>
internal static class PlistReader
{
    /// <summary>Reads the property list's root value.</summary>
    /// <param name="xml">The file's text.</param>
    /// <exception cref="XmlException">The text is not a property list.</exception>
    public static object? Read(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
        };
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, settings);
        reader.MoveToContent();
        if (reader.LocalName != "plist")
        {
            throw new XmlException("The file is not a property list.");
        }

        if (reader.IsEmptyElement)
        {
            return null;
        }

        reader.Read();
        reader.MoveToContent();
        return reader.NodeType == XmlNodeType.Element ? ReadValue(reader) : null;
    }

    /// <summary>Reads the value at the current element and moves past it.</summary>
    private static object? ReadValue(XmlReader reader)
    {
        switch (reader.LocalName)
        {
            case "dict":
                return ReadDictionary(reader);
            case "array":
                return ReadArray(reader);
            case "true":
                reader.Skip();
                return true;
            case "false":
                reader.Skip();
                return false;
            case "integer":
            case "real":
                var number = reader.ReadElementContentAsString();
                return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
            case "string":
            case "data":
            case "date":
                return reader.ReadElementContentAsString();
            default:
                reader.Skip();
                return null;
        }
    }

    private static Dictionary<string, object?> ReadDictionary(XmlReader reader)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return result;
        }

        reader.Read();
        string? key = null;
        while (reader.NodeType != XmlNodeType.EndElement && !reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();
                continue;
            }

            if (reader.LocalName == "key")
            {
                key = reader.ReadElementContentAsString();
                continue;
            }

            var value = ReadValue(reader);
            if (key is not null)
            {
                result[key] = value;
                key = null;
            }
        }

        reader.Read();
        return result;
    }

    private static List<object?> ReadArray(XmlReader reader)
    {
        var result = new List<object?>();
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return result;
        }

        reader.Read();
        while (reader.NodeType != XmlNodeType.EndElement && !reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                result.Add(ReadValue(reader));
            }
            else
            {
                reader.Read();
            }
        }

        reader.Read();
        return result;
    }
}
