using System.Security;
using Microsoft.CodeAnalysis;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Notebooks.Execution;
using TabStudio.Packages;

namespace TabStudio.App.Services;

/// <summary>
/// Resolves a notebook's <c>#r</c> lines: <c>#r "nuget: Humanizer, 2.14.1"</c> restores the package on the device
/// with TabStudio's own restorer, into the packages folder every project shares, and <c>#r "Library.dll"</c>
/// references an assembly file.
/// </summary>
/// <remarks>
/// The packages a notebook asks for are listed in a small project of its own, so each restore includes the
/// packages before it and versions are resolved as for any project. Each assembly is handed to the kernel once;
/// after the kernel restarts, <see cref="ForgetReferences"/> makes the next <c>#r</c> hand them over again. The
/// kernel resolves one cell at a time, so resolutions do not overlap.
/// </remarks>
public sealed class NotebookPackageResolver : INotebookReferenceResolver
{
    private const string LatestVersion = "*";

    private readonly PackageRestorer _restorer;
    private readonly string _folder;
    private readonly string? _baseDirectory;
    private readonly Dictionary<string, string> _packages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MetadataReference> _references = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _given = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    /// <summary>Creates the resolver.</summary>
    /// <param name="restorer">The app's package restorer.</param>
    /// <param name="folder">A folder of the resolver's own, for the project that lists the notebook's packages.</param>
    /// <param name="baseDirectory">The folder relative assembly paths start from, such as the workspace's.</param>
    public NotebookPackageResolver(PackageRestorer restorer, string folder, string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(restorer);
        ArgumentException.ThrowIfNullOrEmpty(folder);
        _restorer = restorer;
        _folder = folder;
        _baseDirectory = baseDirectory;
    }

    /// <inheritdoc />
    public Task<NotebookReferenceResolution> ResolveAsync(NotebookReference reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Kind == NotebookReferenceKind.NuGetPackage
            ? RestoreAsync(reference, cancellationToken)
            : Task.FromResult(ResolveFile(reference));
    }

    /// <summary>Called when the kernel restarts and forgets its references, so they are handed over again.</summary>
    public void ForgetReferences()
    {
        lock (_gate)
        {
            _given.Clear();
        }
    }

    private async Task<NotebookReferenceResolution> RestoreAsync(NotebookReference reference, CancellationToken cancellationToken)
    {
        var version = reference.Version ?? LatestVersion;
        var previous = _packages.GetValueOrDefault(reference.Name);
        if (previous is not null && !string.Equals(previous, version, StringComparison.OrdinalIgnoreCase) && HasGivenReferences())
        {
            return NotebookReferenceResolution.Failed($"{reference.Name} {previous} is already loaded. Restart the kernel to use {version}.");
        }

        _packages[reference.Name] = version;
        var result = await _restorer.RestoreAsync(WriteProject(), progress: null, cancellationToken).ConfigureAwait(false);
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverityLevel.Error)
            .Select(d => $"{d.Id}: {d.Message}")
            .ToList();
        if (errors.Count == 0)
        {
            return Hand(result.CompileAssemblies, result.RuntimeAssemblies);
        }

        // A package that cannot be restored is not asked for again by later #r lines.
        if (previous is null)
        {
            _packages.Remove(reference.Name);
        }
        else
        {
            _packages[reference.Name] = previous;
        }

        return new NotebookReferenceResolution { Errors = errors };
    }

    private NotebookReferenceResolution ResolveFile(NotebookReference reference)
    {
        var path = Path.IsPathRooted(reference.Name) || _baseDirectory is null
            ? reference.Name
            : Path.Combine(_baseDirectory, reference.Name);
        if (!File.Exists(path))
        {
            return NotebookReferenceResolution.Failed($"The file '{reference.Name}' was not found.");
        }

        var fullPath = Path.GetFullPath(path);
        return Hand([fullPath], [fullPath]);
    }

    private bool HasGivenReferences()
    {
        lock (_gate)
        {
            return _given.Count > 0;
        }
    }

    /// <summary>The references the kernel does not have yet, and every assembly it may need to load.</summary>
    private NotebookReferenceResolution Hand(IReadOnlyList<string> compile, IReadOnlyList<string> runtime)
    {
        var references = new List<MetadataReference>();
        lock (_gate)
        {
            foreach (var path in compile.Where(_given.Add))
            {
                if (!_references.TryGetValue(path, out var metadata))
                {
                    metadata = MetadataReference.CreateFromFile(path);
                    _references[path] = metadata;
                }

                references.Add(metadata);
            }
        }

        return new NotebookReferenceResolution { References = references, RuntimeAssemblyPaths = runtime };
    }

    private TabStudio.Core.Workspaces.ProjectInfo WriteProject()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "Notebook.csproj");
        var items = string.Join(
            "\n",
            _packages.Select(p => $"    <PackageReference Include=\"{SecurityElement.Escape(p.Key)}\" Version=\"{SecurityElement.Escape(p.Value)}\" />"));
        File.WriteAllText(path, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
            {items}
              </ItemGroup>
            </Project>

            """);
        return ProjectParser.Load(path);
    }
}
