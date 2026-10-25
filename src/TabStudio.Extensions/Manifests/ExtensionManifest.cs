namespace TabStudio.Extensions.Manifests;

/// <summary>
/// An extension's <c>package.json</c>, as far as TabStudio uses it: who made it, what it is, whether it has code, and
/// what it contributes. Strings that the manifest localizes with <c>%key%</c> are already replaced by their English text.
/// </summary>
public sealed record ExtensionManifest
{
    /// <summary>The extension's identifier, <c>publisher.name</c>.</summary>
    public required ExtensionIdentifier Id { get; init; }

    /// <summary>The version, such as <c>1.95.3</c>.</summary>
    public required string Version { get; init; }

    /// <summary>The name shown to people; the extension's name when the manifest has no display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A one-line description, or an empty string.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The VS Code versions the extension asks for (<c>engines.vscode</c>), such as <c>^1.80.0</c>, or <see langword="null"/>.</summary>
    public string? EngineVersion { get; init; }

    /// <summary>Marketplace categories, such as <c>Programming Languages</c> or <c>Themes</c>.</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>Search keywords.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>The extension's icon, relative to the extension folder, or <see langword="null"/>.</summary>
    public string? Icon { get; init; }

    /// <summary>The license, such as <c>MIT</c>, or <see langword="null"/>.</summary>
    public string? License { get; init; }

    /// <summary>The project's home page, or <see langword="null"/>.</summary>
    public Uri? Homepage { get; init; }

    /// <summary>The source repository, or <see langword="null"/>.</summary>
    public Uri? Repository { get; init; }

    /// <summary>The JavaScript entry point that runs in Node (<c>main</c>), or <see langword="null"/> for a declarative extension.</summary>
    public string? Main { get; init; }

    /// <summary>The JavaScript entry point that runs in a browser (<c>browser</c>), or <see langword="null"/>.</summary>
    public string? Browser { get; init; }

    /// <summary>When VS Code starts the extension's code, such as <c>onLanguage:perl</c>.</summary>
    public IReadOnlyList<string> ActivationEvents { get; init; } = [];

    /// <summary>Where the extension prefers to run in remote setups: <c>ui</c>, <c>workspace</c> or <c>web</c>.</summary>
    public IReadOnlyList<string> ExtensionKind { get; init; } = [];

    /// <summary>Extensions this one needs, as <c>publisher.name</c>.</summary>
    public IReadOnlyList<string> ExtensionDependencies { get; init; } = [];

    /// <summary>Extensions this one installs together, for an extension pack.</summary>
    public IReadOnlyList<string> ExtensionPack { get; init; } = [];

    /// <summary>Whether the publisher marks it as a preview.</summary>
    public bool IsPreview { get; init; }

    /// <summary>What the extension contributes.</summary>
    public ExtensionContributions Contributes { get; init; } = ExtensionContributions.None;

    /// <summary>Whether the extension has JavaScript that VS Code would run, in Node or in a browser.</summary>
    public bool HasCode => Main is not null || Browser is not null;
}
