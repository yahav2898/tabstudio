using System.Text;
using System.Xml;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Parser;
using TextMateSharp.Internal.Types;
using TextMateSharp.Themes;

namespace TabStudio.App.Editing.Extensions;

/// <summary>
/// Hands the grammars and the theme of installed extensions to <see cref="TextMateHighlighting"/>: grammars written as
/// JSON (<c>.tmLanguage.json</c>) or as TextMate property lists (<c>.tmLanguage</c>), injection grammars, and the
/// chosen color theme.
/// </summary>
internal sealed class ExtensionTextMate : ITextMateContributions
{
    private ExtensionTextMate()
    {
    }

    /// <summary>The one instance, which always reflects <see cref="EditorExtensions"/>.</summary>
    public static ExtensionTextMate Instance { get; } = new();

    /// <inheritdoc />
    public IRawTheme? Theme => EditorExtensions.Theme?.RawTheme;

    /// <inheritdoc />
    public string? ScopeFor(string fileName)
    {
        var contributions = EditorExtensions.Contributions;
        return contributions.FindLanguageForFile(fileName) is { } language
            ? contributions.FindGrammarForLanguage(language.Id)?.ScopeName
            : null;
    }

    /// <inheritdoc />
    public IRawGrammar? GetGrammar(string scopeName) =>
        EditorExtensions.Contributions.FindGrammar(scopeName) is { } grammar ? Read(grammar.Path) : null;

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetInjections(string scopeName) => EditorExtensions.Contributions.FindInjections(scopeName);

    /// <summary>Reads a grammar file, or returns <see langword="null"/> when it cannot be read; the file then shows as plain text.</summary>
    /// <param name="path">The grammar file.</param>
    internal static IRawGrammar? Read(string path)
    {
        try
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            if (text.TrimStart().StartsWith('<'))
            {
                return ReadPropertyList(text);
            }

            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(text)), Encoding.UTF8);
            return GrammarReader.ReadGrammarSync(reader);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or System.Text.Json.JsonException or InvalidCastException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Reads a TextMate grammar written as an XML property list, with TextMateSharp's own property list builder.</summary>
    private static IRawGrammar? ReadPropertyList(string xml)
    {
        var plist = new PList<IRawGrammar>(false);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, settings);
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    var empty = reader.IsEmptyElement;
                    plist.StartElement(reader.LocalName);
                    if (empty)
                    {
                        plist.EndElement(reader.LocalName);
                    }

                    break;
                case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace:
                    plist.AddString(reader.Value);
                    break;
                case XmlNodeType.EndElement:
                    plist.EndElement(reader.LocalName);
                    break;
            }
        }

        return plist.GetResult();
    }
}
