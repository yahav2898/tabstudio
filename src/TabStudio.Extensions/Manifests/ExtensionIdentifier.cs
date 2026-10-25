namespace TabStudio.Extensions.Manifests;

/// <summary>
/// Identifies an extension as VS Code and Open VSX do: <c>publisher.name</c>, such as <c>vscode.perl</c>. Open VSX
/// calls the publisher the namespace. Comparisons ignore case, as VS Code's do.
/// </summary>
public readonly record struct ExtensionIdentifier
{
    /// <summary>Creates an identifier.</summary>
    /// <param name="publisher">The publisher, or Open VSX namespace.</param>
    /// <param name="name">The extension's name.</param>
    public ExtensionIdentifier(string publisher, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Publisher = publisher;
        Name = name;
    }

    /// <summary>The publisher, or Open VSX namespace.</summary>
    public string Publisher { get; }

    /// <summary>The extension's name, unique within its publisher.</summary>
    public string Name { get; }

    /// <summary>Reads <c>publisher.name</c>.</summary>
    /// <param name="text">The identifier.</param>
    /// <param name="identifier">The identifier, when the text is one.</param>
    public static bool TryParse(string? text, out ExtensionIdentifier identifier)
    {
        identifier = default;
        var dot = text?.IndexOf('.', StringComparison.Ordinal) ?? -1;
        if (text is null || dot <= 0 || dot == text.Length - 1 || text.AsSpan().ContainsAny(' ', '/', '\\'))
        {
            return false;
        }

        identifier = new ExtensionIdentifier(text[..dot], text[(dot + 1)..]);
        return true;
    }

    /// <inheritdoc />
    public bool Equals(ExtensionIdentifier other) =>
        string.Equals(Publisher, other.Publisher, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(
        StringComparer.OrdinalIgnoreCase.GetHashCode(Publisher ?? string.Empty),
        StringComparer.OrdinalIgnoreCase.GetHashCode(Name ?? string.Empty));

    /// <summary>The identifier as <c>publisher.name</c>.</summary>
    public override string ToString() => $"{Publisher}.{Name}";
}
