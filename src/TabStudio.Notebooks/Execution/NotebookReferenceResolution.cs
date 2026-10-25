using Microsoft.CodeAnalysis;

namespace TabStudio.Notebooks.Execution;

/// <summary>
/// What an <see cref="INotebookReferenceResolver"/> found for a <c>#r</c> reference: the assemblies to
/// compile against, and the files to load them from when the cell runs.
/// </summary>
public sealed record NotebookReferenceResolution
{
    /// <summary>
    /// The assemblies the cell and the cells after it compile against, such as the reference assemblies of a
    /// package and of its dependencies.
    /// </summary>
    public IReadOnlyList<MetadataReference> References { get; init; } = [];

    /// <summary>
    /// The implementation assemblies to load when running code needs them, as full paths. Assemblies the
    /// running app already has, such as the .NET libraries, are always taken from the app instead.
    /// </summary>
    public IReadOnlyList<string> RuntimeAssemblyPaths { get; init; } = [];

    /// <summary>Why the reference could not be resolved. Empty when it was.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Whether the reference was resolved.</summary>
    public bool Succeeded => Errors.Count == 0;

    /// <summary>Creates a result for a reference that could not be resolved.</summary>
    /// <param name="error">Why, in a sentence the cell's author can act on.</param>
    public static NotebookReferenceResolution Failed(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new NotebookReferenceResolution { Errors = [error] };
    }
}
