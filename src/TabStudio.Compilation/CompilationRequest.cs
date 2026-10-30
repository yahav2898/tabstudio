using Microsoft.CodeAnalysis;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Compilation;

/// <summary>What kind of assembly a compilation produces.</summary>
public enum AssemblyKind
{
    /// <summary>A program with an entry point.</summary>
    ConsoleApplication,

    /// <summary>A library without an entry point.</summary>
    Library,
}

/// <summary>
/// Everything needed to compile one assembly.
/// </summary>
public sealed record CompilationRequest
{
    /// <summary>The assembly name, usually the project name.</summary>
    public required string AssemblyName { get; init; }

    /// <summary>The source files.</summary>
    public required IReadOnlyList<SourceDocument> Documents { get; init; }

    /// <summary>Whether to produce a program or a library.</summary>
    public AssemblyKind Kind { get; init; } = AssemblyKind.ConsoleApplication;

    /// <summary>Whether to add the SDK's implicit global usings, as <c>&lt;ImplicitUsings&gt;enable&lt;/ImplicitUsings&gt;</c> does.</summary>
    public bool ImplicitUsings { get; init; } = true;

    /// <summary>Whether nullable reference types are enabled.</summary>
    public bool Nullable { get; init; } = true;

    /// <summary>Whether to optimize the generated code. Debug builds do not.</summary>
    public bool Optimize { get; init; }

    /// <summary>Whether <c>unsafe</c> code is allowed.</summary>
    public bool AllowUnsafe { get; init; }

    /// <summary>Assemblies this one references, beyond the .NET reference assemblies: usually other projects in the solution.</summary>
    public IReadOnlyList<MetadataReference> AdditionalReferences { get; init; } = [];
}

/// <summary>
/// An assembly produced in memory.
/// </summary>
/// <param name="Name">The assembly name.</param>
/// <param name="Image">The assembly bytes.</param>
/// <param name="Symbols">The portable PDB bytes, for stack traces with line numbers.</param>
public sealed record CompiledAssembly(string Name, byte[] Image, byte[]? Symbols)
{
    /// <summary>The exact compilation emitted into this image, retained for local Debug Hot Reload only.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Microsoft.CodeAnalysis.Compilation? HotReloadCompilation { get; init; }
}

/// <summary>
/// The outcome of compiling one assembly.
/// </summary>
/// <param name="AssemblyName">The assembly that was compiled.</param>
/// <param name="Diagnostics">Errors, warnings and messages, most severe first.</param>
/// <param name="Assembly">The compiled assembly, or <see langword="null"/> when compilation failed.</param>
/// <param name="Duration">How long compilation took.</param>
public sealed record CompilationResult(
    string AssemblyName,
    IReadOnlyList<BuildDiagnostic> Diagnostics,
    CompiledAssembly? Assembly,
    TimeSpan Duration)
{
    /// <summary>Whether an assembly was produced.</summary>
    public bool Success => Assembly is not null;

    /// <summary>The number of errors.</summary>
    public int ErrorCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverityLevel.Error);

    /// <summary>The number of warnings.</summary>
    public int WarningCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverityLevel.Warning);
}
