using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace TabStudio.Porting.Output;

/// <summary>A port note while its file is being converted: anchored to a XAML node or a C# syntax node.</summary>
/// <param name="What">What is there.</param>
/// <param name="Why">Why it was not converted, and what would finish it.</param>
internal sealed record PendingNote(string What, string Why)
{
    /// <summary>The XAML node the note is about (usually the TODO comment in front of it).</summary>
    public XObject? XamlAnchor { get; init; }

    /// <summary>The annotation of the C# node the note is about.</summary>
    public SyntaxAnnotation? CodeAnchor { get; init; }

    /// <summary>A fixed line, for notes about generated files.</summary>
    public int? Line { get; init; }
}

/// <summary>What the converter did in one file of the new project: its conversions and its port notes.</summary>
internal sealed class FileReport
{
    private readonly Dictionary<string, int> _conversions = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    /// <summary>Starts the report of a file.</summary>
    /// <param name="path">The file in the new project, relative with <c>/</c>.</param>
    public FileReport(string path) => Path = path;

    /// <summary>The file in the new project.</summary>
    public string Path { get; }

    /// <summary>The notes, anchored, in the order they were found.</summary>
    public List<PendingNote> Notes { get; } = [];

    /// <summary>Counts one conversion.</summary>
    /// <param name="description">What changed, such as <c>Visibility → IsVisible</c>.</param>
    /// <param name="count">How many times.</param>
    public void Converted(string description, int count = 1)
    {
        if (count <= 0)
        {
            return;
        }

        if (!_conversions.TryAdd(description, count))
        {
            _conversions[description] += count;
        }
        else
        {
            _order.Add(description);
        }
    }

    /// <summary>Adds a note anchored to a XAML node.</summary>
    public void Note(XObject anchor, string what, string why) => Notes.Add(new PendingNote(what, why) { XamlAnchor = anchor });

    /// <summary>Adds a note anchored to a C# node's annotation.</summary>
    public void Note(SyntaxAnnotation anchor, string what, string why) => Notes.Add(new PendingNote(what, why) { CodeAnchor = anchor });

    /// <summary>Adds a note at a fixed line.</summary>
    public void Note(int line, string what, string why) => Notes.Add(new PendingNote(what, why) { Line = line });

    /// <summary>The conversions, in the order they first happened.</summary>
    public List<PortConversion> Conversions() => [.. _order.Select(d => new PortConversion(d, _conversions[d]))];
}
