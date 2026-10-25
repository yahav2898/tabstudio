using TabStudio.Compilation.Building;

namespace TabStudio.Link.Language;

/// <summary>A project and its build.</summary>
/// <param name="ProjectFilePath">The project file.</param>
/// <param name="Build">The build's result.</param>
public sealed record LinkProjectBuild(string ProjectFilePath, BuildResult Build);
