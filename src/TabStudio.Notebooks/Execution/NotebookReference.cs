namespace TabStudio.Notebooks.Execution;

/// <summary>What a <c>#r</c> line in a cell refers to.</summary>
public enum NotebookReferenceKind
{
    /// <summary>A NuGet package: <c>#r "nuget: Newtonsoft.Json, 13.0.3"</c>.</summary>
    NuGetPackage,

    /// <summary>An assembly file: <c>#r "path/to/Library.dll"</c>.</summary>
    AssemblyFile,
}

/// <summary>
/// A reference that a cell asks for with <c>#r</c>, passed to an <see cref="INotebookReferenceResolver"/>.
/// </summary>
/// <param name="Kind">Whether this is a NuGet package or an assembly file.</param>
/// <param name="Name">The package id, or the assembly path exactly as the cell wrote it.</param>
/// <param name="Version">For a package, the version or range the cell asked for, such as <c>13.0.3</c> or <c>13.*</c>; <see langword="null"/> for the latest.</param>
public sealed record NotebookReference(NotebookReferenceKind Kind, string Name, string? Version = null)
{
    /// <summary>The reference as a cell writes it, such as <c>nuget: Newtonsoft.Json, 13.0.3</c>.</summary>
    public override string ToString() => Kind switch
    {
        NotebookReferenceKind.NuGetPackage when Version is not null => $"nuget: {Name}, {Version}",
        NotebookReferenceKind.NuGetPackage => $"nuget: {Name}",
        _ => Name,
    };
}
