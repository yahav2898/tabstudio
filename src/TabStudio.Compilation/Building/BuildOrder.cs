using TabStudio.Core.Workspaces;

namespace TabStudio.Compilation.Building;

/// <summary>
/// The order to compile a project and the projects it references: dependencies first, the project last. Taken
/// from the project files rather than Roslyn's graph, which leaves out references that are compiled as
/// skeleton metadata.
/// </summary>
internal static class BuildOrder
{
    /// <summary>Lists a project and every project it transitively references, dependencies first.</summary>
    /// <param name="start">The project.</param>
    /// <param name="loaded">The loaded projects by file path; references to other projects are skipped.</param>
    public static IReadOnlyList<ProjectInfo> For(ProjectInfo start, IReadOnlyDictionary<string, ProjectInfo> loaded)
    {
        var order = new List<ProjectInfo>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        Visit(start);
        return order;

        void Visit(ProjectInfo project)
        {
            if (!visited.Add(project.FilePath))
            {
                return;
            }

            foreach (var reference in project.ProjectReferences)
            {
                // A project referenced only as a source generator runs inside the compiler, compiled by the host for
                // the projects that use it (ProjectGeneratorReference); it is not part of the program.
                var item = project.GetProjectReference(reference);
                if (item.IsAnalyzer && !item.ReferenceOutputAssembly)
                {
                    continue;
                }

                if (loaded.TryGetValue(reference, out var dependency))
                {
                    Visit(dependency);
                }
            }

            order.Add(project);
        }
    }
}
