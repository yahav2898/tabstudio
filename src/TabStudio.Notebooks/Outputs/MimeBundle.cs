using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace TabStudio.Notebooks.Outputs;

/// <summary>
/// One value in several representations, keyed by MIME type: how Jupyter stores rich output.
/// </summary>
/// <remarks>
/// A front end shows the richest representation it can render, such as <c>text/html</c> in a web view or
/// <c>text/plain</c> in a native text view. Values are text: binary data such as <c>image/png</c> is base64, and
/// JSON types such as <c>application/json</c> hold the JSON as text. Bundles are immutable and keep their
/// entries in the order they were added.
/// </remarks>
public sealed class MimeBundle : IEquatable<MimeBundle>
{
    private readonly KeyValuePair<string, string>[] _entries;

    private MimeBundle(KeyValuePair<string, string>[] entries)
    {
        _entries = entries;
        Entries = new ReadOnlyCollection<KeyValuePair<string, string>>(entries);
    }

    /// <summary>A bundle without any representations.</summary>
    public static MimeBundle Empty { get; } = new([]);

    /// <summary>The representations, as MIME type and value, in the order they were added.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Entries { get; }

    /// <summary>The number of representations.</summary>
    public int Count => _entries.Length;

    /// <summary>The <c>text/plain</c> representation, if there is one.</summary>
    public string? PlainText => this[MimeTypes.PlainText];

    /// <summary>The <c>text/html</c> representation, if there is one.</summary>
    public string? Html => this[MimeTypes.Html];

    /// <summary>The value for a MIME type, or <see langword="null"/> when the bundle has none.</summary>
    /// <param name="mimeType">The MIME type.</param>
    public string? this[string mimeType] => TryGetValue(mimeType, out var value) ? value : null;

    /// <summary>Creates a bundle. A MIME type that appears twice keeps its last value.</summary>
    /// <param name="entries">MIME types and their values.</param>
    public static MimeBundle Create(IEnumerable<KeyValuePair<string, string>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var list = new List<KeyValuePair<string, string>>();
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry.Key, nameof(entries));
            ArgumentNullException.ThrowIfNull(entry.Value, nameof(entries));

            var index = list.FindIndex(e => e.Key == entry.Key);
            if (index >= 0)
            {
                list[index] = entry;
            }
            else
            {
                list.Add(entry);
            }
        }

        return list.Count == 0 ? Empty : new MimeBundle([.. list]);
    }

    /// <summary>Creates a bundle with only plain text.</summary>
    /// <param name="text">The text.</param>
    public static MimeBundle FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new MimeBundle([new(MimeTypes.PlainText, text)]);
    }

    /// <summary>Looks up the value for a MIME type.</summary>
    /// <param name="mimeType">The MIME type.</param>
    /// <param name="value">The value, when the bundle has one.</param>
    public bool TryGetValue(string mimeType, [NotNullWhen(true)] out string? value)
    {
        ArgumentNullException.ThrowIfNull(mimeType);
        foreach (var entry in _entries)
        {
            if (entry.Key == mimeType)
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>Whether the bundle has a value for a MIME type.</summary>
    /// <param name="mimeType">The MIME type.</param>
    public bool Contains(string mimeType) => TryGetValue(mimeType, out _);

    /// <summary>Returns a bundle with a representation added or replaced.</summary>
    /// <param name="mimeType">The MIME type.</param>
    /// <param name="value">The value.</param>
    public MimeBundle With(string mimeType, string value)
    {
        ArgumentNullException.ThrowIfNull(mimeType);
        ArgumentNullException.ThrowIfNull(value);
        return Create([.. _entries, new(mimeType, value)]);
    }

    /// <summary>Returns a bundle with only the given MIME types, in this bundle's order.</summary>
    /// <param name="mimeTypes">The MIME types to keep.</param>
    public MimeBundle Only(IEnumerable<string> mimeTypes)
    {
        ArgumentNullException.ThrowIfNull(mimeTypes);
        var keep = mimeTypes.ToHashSet(StringComparer.Ordinal);
        return Create(_entries.Where(e => keep.Contains(e.Key)));
    }

    /// <inheritdoc />
    public bool Equals(MimeBundle? other)
    {
        if (other is null || other._entries.Length != _entries.Length)
        {
            return false;
        }

        foreach (var entry in _entries)
        {
            if (!other.TryGetValue(entry.Key, out var value) || value != entry.Value)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as MimeBundle);

    /// <inheritdoc />
    public override int GetHashCode() => _entries.Length;

    /// <inheritdoc />
    public override string ToString() => string.Join(", ", _entries.Select(e => e.Key));
}
