namespace TabStudio.Core.Workspaces;

/// <summary>Where the .NET SDK puts a project's Debug build: <c>bin/Debug/net8.0</c> under the project, by default.</summary>
public static class ProjectOutputFolder
{
    /// <summary>
    /// The full path of a project's output folder for its Debug build of the framework TabStudio compiles
    /// (<see cref="ProjectParser.EvaluationTargetFramework"/>), as the SDK computes it: <c>OutputPath</c> when the
    /// project sets it, otherwise <c>BaseOutputPath</c> (<c>bin/</c>) and <c>Debug/</c>, then the framework, unless
    /// <c>AppendTargetFrameworkToOutputPath</c> is <c>false</c>.
    /// </summary>
    /// <param name="project">The project.</param>
    public static string For(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var properties = project.Properties;
        var folder = properties.GetValueOrDefault("OutputPath") is { Length: > 0 } output
            ? output
            : Path.Combine(properties.GetValueOrDefault("BaseOutputPath") is { Length: > 0 } baseOutput ? baseOutput : "bin", "Debug");
        var framework = properties.GetValueOrDefault("TargetFramework") is { Length: > 0 } declared && !declared.Contains(';', StringComparison.Ordinal)
            ? declared
            : ProjectParser.EvaluationTargetFramework(project.TargetFrameworks);
        if (framework is not null && !string.Equals(properties.GetValueOrDefault("AppendTargetFrameworkToOutputPath"), "false", StringComparison.OrdinalIgnoreCase))
        {
            folder = Path.Combine(folder, framework);
        }

        return Path.TrimEndingDirectorySeparator(PathUtilities.ResolveRelative(project.DirectoryPath, folder));
    }
}
