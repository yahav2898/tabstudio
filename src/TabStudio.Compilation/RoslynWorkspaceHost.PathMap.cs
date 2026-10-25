using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

public sealed partial class RoslynWorkspaceHost
{
    private ImmutableArray<KeyValuePair<string, string>> _pathMap = [];

    /// <summary>
    /// Maps source paths in what the compiler emits, as the compiler's <c>-pathmap</c> option does: the documents of
    /// PDBs and the values of <c>[CallerFilePath]</c>. A TabStudio Link host compiles a copy of a tablet's workspace
    /// in a folder of its own and maps it back to the tablet's paths, so stack traces point at the tablet's files.
    /// Takes effect at the next <see cref="Load"/>.
    /// </summary>
    /// <param name="pathMap">
    /// Pairs of a path prefix and its replacement, each ending with a directory separator; empty to map nothing.
    /// </param>
    public void SetPathMap(IEnumerable<KeyValuePair<string, string>> pathMap)
    {
        ArgumentNullException.ThrowIfNull(pathMap);
        lock (_gate)
        {
            _pathMap = [.. pathMap];
        }
    }

    /// <summary>Applies the path map to a project's compilation options. The caller holds the gate.</summary>
    private CompilationOptions WithPathMap(CompilationOptions options, TsProject project) =>
        _pathMap.IsEmpty ? options : options.WithSourceReferenceResolver(new SourceFileResolver([], project.DirectoryPath, _pathMap));
}
