namespace TabStudio.Notebooks.Execution;

/// <summary>
/// Supplies the assemblies behind a cell's <c>#r</c> lines, such as the packages of <c>#r "nuget: ..."</c>.
/// </summary>
/// <remarks>
/// <para>
/// The kernel takes every <c>#r</c> line out of a cell before compiling it and asks the resolver for each one.
/// What the resolver returns is added to the compilation of that cell and every cell after it, until the
/// kernel restarts. Without a resolver, <c>#r</c> lines are reported as not supported yet.
/// </para>
/// <para>
/// A NuGet implementation restores the package and its dependencies on the device, then returns their
/// reference assemblies (<c>ref/</c> or <c>lib/</c> for <c>net10.0</c>) as
/// <see cref="NotebookReferenceResolution.References"/> and their <c>lib/</c> assemblies as
/// <see cref="NotebookReferenceResolution.RuntimeAssemblyPaths"/>.
/// </para>
/// </remarks>
public interface INotebookReferenceResolver
{
    /// <summary>Resolves one reference.</summary>
    /// <param name="reference">The reference as the cell wrote it.</param>
    /// <param name="cancellationToken">Cancelled when the cell is interrupted.</param>
    /// <returns>The assemblies, or the reason they could not be found.</returns>
    Task<NotebookReferenceResolution> ResolveAsync(NotebookReference reference, CancellationToken cancellationToken);
}
