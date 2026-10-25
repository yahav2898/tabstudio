using System.Text.Json.Serialization;

namespace TabStudio.Toolchains.Catalog;

/// <summary>What a toolchain is.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolchainKind>))]
public enum ToolchainKind
{
    /// <summary>A language's compilers, runtimes, language server and debugger.</summary>
    Language,

    /// <summary>A platform apps of other languages run on, such as Windows Compatibility for Windows desktop apps.</summary>
    Platform,
}

/// <summary>What a toolchain installs on one processor besides what it installs everywhere.</summary>
public sealed record ArchitectureDefinition
{
    /// <summary>More Alpine Linux packages.</summary>
    public IReadOnlyList<string> Packages { get; init; } = [];

    /// <summary>More setup steps, such as an archive built for the processor.</summary>
    public IReadOnlyList<SetupStepDefinition> Setup { get; init; } = [];

    /// <summary>The package whose version is the toolchain's on this processor, when it is another one.</summary>
    public string? VersionPackage { get; init; }
}
