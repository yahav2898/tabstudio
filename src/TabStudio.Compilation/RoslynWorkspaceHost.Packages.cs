using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>NuGet package references: the restored package assemblies each project compiles against.</summary>
public sealed partial class RoslynWorkspaceHost
{
    /// <summary>The package references of each restored project, keyed by project file path.</summary>
    private readonly Dictionary<string, IReadOnlyList<MetadataReference>> _packageReferences = new(StringComparer.Ordinal);

    /// <summary>
    /// The package references each restored project's last restore could not restore, keyed by project file path;
    /// a project whose restore restored everything has none.
    /// </summary>
    private readonly Dictionary<string, HashSet<string>> _unrestoredPackages = new(StringComparer.Ordinal);

    /// <summary>
    /// One reference per assembly file, shared by every project, so Roslyn sees the same reference object across
    /// restores and can keep reusing its compilations.
    /// </summary>
    private readonly Dictionary<string, MetadataReference> _packageReferenceCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Sets the restored package assemblies a project compiles against, replacing any it had before. The framework
    /// references stay as they are.
    /// </summary>
    /// <remarks>
    /// The references are kept by project file path, so they survive <see cref="Load"/> (for a project with the
    /// same path) and <see cref="TrimMemory"/>. The project counts as restored from then on, even with no
    /// assemblies, so builds stop reporting <c>TS2003</c> for its package references, except for those in
    /// <paramref name="unrestoredPackages"/>. A restore with errors still gives the packages it restored, so one
    /// missing package does not take the others away. The files are memory-mapped, like the framework's reference
    /// assemblies, and must not change while TabStudio runs; the global packages folder never changes a package once
    /// it is extracted.
    /// </remarks>
    /// <param name="projectFilePath">Full path of the project file. The project does not have to be loaded yet.</param>
    /// <param name="compileAssemblyPaths">Full paths of the package assemblies, such as <c>RestoreResult.CompileAssemblies</c>.</param>
    /// <param name="unrestoredPackages">
    /// The ids of the project's package references that the restore could not restore, such as
    /// <c>RestoreResult.UnrestoredReferences</c>; builds report <c>TS2003</c> for them. <see langword="null"/> or empty
    /// when every package was restored.
    /// </param>
    /// <exception cref="FileNotFoundException">An assembly does not exist.</exception>
    public void SetPackageReferences(string projectFilePath, IReadOnlyList<string> compileAssemblyPaths, IReadOnlyCollection<string>? unrestoredPackages = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFilePath);
        ArgumentNullException.ThrowIfNull(compileAssemblyPaths);

        lock (_gate)
        {
            var references = compileAssemblyPaths
                .Distinct(StringComparer.Ordinal)
                .Select(GetOrCreatePackageReference)
                .ToList();

            _packageReferences[projectFilePath] = references;
            if (unrestoredPackages is { Count: > 0 })
            {
                _unrestoredPackages[projectFilePath] = new HashSet<string>(unrestoredPackages, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _unrestoredPackages.Remove(projectFilePath);
            }

            ApplyMetadataReferences(projectFilePath);
            InvalidateGenerators(projectFilePath);
        }
    }

    /// <summary>Forgets the package assemblies of a project, so it compiles against the framework only again.</summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    public void ClearPackageReferences(string projectFilePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFilePath);

        lock (_gate)
        {
            _unrestoredPackages.Remove(projectFilePath);
            if (_packageReferences.Remove(projectFilePath))
            {
                ApplyMetadataReferences(projectFilePath);
                InvalidateGenerators(projectFilePath);
            }
        }
    }

    /// <summary>Whether <see cref="SetPackageReferences"/> was called for a project.</summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    public bool ArePackagesRestored(string projectFilePath)
    {
        lock (_gate)
        {
            return _packageReferences.ContainsKey(projectFilePath);
        }
    }

    /// <summary>
    /// Whether one package reference of a project is restored: <see cref="SetPackageReferences"/> was called for the
    /// project and did not name the package as one the restore could not restore.
    /// </summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    /// <param name="packageId">The id of the package, as the project references it. Ids are case-insensitive.</param>
    public bool IsPackageRestored(string projectFilePath, string packageId)
    {
        lock (_gate)
        {
            return _packageReferences.ContainsKey(projectFilePath) &&
                !(_unrestoredPackages.TryGetValue(projectFilePath, out var unrestored) && unrestored.Contains(packageId));
        }
    }

    /// <summary>The package assemblies a project compiles against, as set by <see cref="SetPackageReferences"/>.</summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    /// <returns>The file paths, or an empty list when the project has none.</returns>
    public IReadOnlyList<string> GetPackageReferencePaths(string projectFilePath)
    {
        lock (_gate)
        {
            return _packageReferences.TryGetValue(projectFilePath, out var references)
                ? references.OfType<PortableExecutableReference>().Select(r => r.FilePath ?? r.Display ?? string.Empty).ToList()
                : [];
        }
    }

    /// <summary>
    /// The framework references plus the project's package references, and the Forms layer for a Windows Forms
    /// project. The caller holds the gate.
    /// </summary>
    private IReadOnlyList<MetadataReference> GetMetadataReferences(TsProject project)
    {
        var packages = _packageReferences.TryGetValue(project.FilePath, out var restored) ? restored : [];
        var skeletons = SkeletonReferencesFor(project).ToList();
        var forms = WindowsFormsReferencesFor(project);
        var basic = VisualBasicReferencesFor(project);
        var hinted = HintPathReferencesFor(project, packages);
        return packages.Count == 0 && skeletons.Count == 0 && forms.Count == 0 && basic.Count == 0 && hinted.Count == 0
            ? _frameworkReferences
            : [.. _frameworkReferences, .. forms, .. basic, .. packages, .. hinted, .. skeletons];
    }

    /// <summary>
    /// The assemblies a project's <c>Reference</c> items name by <c>HintPath</c>, in its own files or its packages'
    /// targets (see <see cref="TabStudio.Core.Workspaces.ProjectParser.WithPackageBuildFiles"/>), as a repository's <c>lib</c> folder or MSTest's
    /// TestFramework.Extensions: the SDK compiles against each file that exists. One the framework or a package already
    /// provides, by name, is left out. The caller holds the gate.
    /// </summary>
    private List<MetadataReference> HintPathReferencesFor(TsProject project, IReadOnlyList<MetadataReference> packages)
    {
        if (!project.AssemblyReferences.Any(r => !string.IsNullOrWhiteSpace(r.HintPath)))
        {
            return [];
        }

        var provided = _frameworkReferences.Concat(packages)
            .OfType<PortableExecutableReference>()
            .Select(r => Path.GetFileNameWithoutExtension(r.FilePath ?? r.Display ?? string.Empty))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();
        foreach (var reference in project.AssemblyReferences)
        {
            if (reference.HintPath is not { Length: > 0 } hint)
            {
                continue;
            }

            var path = TabStudio.Core.Workspaces.PathUtilities.ResolveRelative(project.DirectoryPath, hint);
            if (!File.Exists(path) || !provided.Add(Path.GetFileNameWithoutExtension(path)))
            {
                continue;
            }

            try
            {
                references.Add(GetOrCreatePackageReference(path));
            }
            catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
            {
                // A file that is not an assembly is what the SDK reports too; the code using it says the rest.
            }
        }

        return references;
    }

    /// <summary>Pushes a project's current references into the loaded solution. The caller holds the gate.</summary>
    private void ApplyMetadataReferences(string projectFilePath)
    {
        if (_projectIds.TryGetValue(projectFilePath, out var id))
        {
            var solution = _workspace.CurrentSolution.WithProjectMetadataReferences(id, GetMetadataReferences(_projects[id]));
            _workspace.TryApplyChanges(solution);
        }
    }

    /// <summary>Returns the shared reference for an assembly file, creating it on first use. The caller holds the gate.</summary>
    private MetadataReference GetOrCreatePackageReference(string path)
    {
        if (!_packageReferenceCache.TryGetValue(path, out var reference))
        {
            reference = CreateMappedReference(path);
            _packageReferenceCache[path] = reference;
        }

        return reference;
    }

    private static PortableExecutableReference CreateMappedReference(string path)
    {
        // As in ReferenceAssemblies.LoadMapped: PEStreamOptions.Default reads through a memory map instead of
        // copying the image to the heap, and the metadata keeps the stream open.
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var module = ModuleMetadata.CreateFromStream(stream, PEStreamOptions.Default);
        return AssemblyMetadata.Create(module).GetReference(filePath: path, display: Path.GetFileName(path));
    }
}
