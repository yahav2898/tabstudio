using Microsoft.CodeAnalysis;

namespace TabStudio.Compilation;

// Read access to the Roslyn model for the IntelliSense features in TabStudio.Compilation.Features. The main
// part of the class (RoslynWorkspaceHost.cs) holds the documentation.
public sealed partial class RoslynWorkspaceHost
{
    /// <summary>The current immutable Roslyn solution. Queries keep working on it while the host is updated.</summary>
    internal Solution CurrentSolution
    {
        get
        {
            lock (_gate)
            {
                return _workspace.CurrentSolution;
            }
        }
    }

    /// <summary>Returns the current Roslyn document of a source file.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <returns>The document, or <see langword="null"/> when the file is not part of a loaded project.</returns>
    internal Document? GetDocument(string filePath) => Snapshot(filePath).Document;
}
