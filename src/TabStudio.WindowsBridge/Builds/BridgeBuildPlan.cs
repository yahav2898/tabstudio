using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;

namespace TabStudio.WindowsBridge.Builds;

/// <summary>A project the PC's tools cannot build, with what to install.</summary>
public sealed class BridgeBuildException : Exception
{
    /// <summary>Creates the exception.</summary>
    public BridgeBuildException()
        : base("This PC cannot build the project.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What is missing and how to install it, in words for the user.</param>
    public BridgeBuildException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong, in words for the user.</param>
    /// <param name="innerException">The cause.</param>
    public BridgeBuildException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// How the bridge builds one project with the PC's own tools: <c>dotnet build</c> for SDK-style .NET projects,
/// and MSBuild from Visual Studio for Visual C++, .NET Framework and WinUI 3, as Visual Studio does. Output is
/// minimal and unaligned, one message per line, so errors and warnings can be read from it.
/// </summary>
/// <param name="FileName">The program that builds.</param>
/// <param name="Arguments">Its arguments for the build.</param>
/// <param name="PropertyArguments">Its arguments for reading the built program's paths afterwards.</param>
/// <param name="Tool">What builds it, for the Output panel, such as <c>dotnet build</c> or <c>MSBuild 17.14</c>.</param>
/// <param name="IsNative">Whether it builds a native program, which the .NET debugger cannot debug.</param>
public sealed record BridgeBuildPlan(string FileName, IReadOnlyList<string> Arguments, IReadOnlyList<string> PropertyArguments, string Tool, bool IsNative)
{
    /// <summary>The properties read after the build: the program, and what starts it.</summary>
    public static IReadOnlyList<string> Properties { get; } = ["TargetPath", "RunCommand", "RunArguments"];

    /// <summary>Plans the build of a project.</summary>
    /// <param name="project">The project, as read from the mirror.</param>
    /// <param name="configuration"><c>Debug</c> or <c>Release</c>.</param>
    /// <param name="skipReferences">Whether only this project changed, so the projects it references are not built.</param>
    /// <param name="tools">The PC's tools.</param>
    /// <exception cref="BridgeBuildException">The PC lacks a tool the project needs.</exception>
    public static BridgeBuildPlan Create(ProjectInfo project, string configuration, bool skipReferences, BridgeToolset tools)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrEmpty(configuration);
        ArgumentNullException.ThrowIfNull(tools);

        if (project.Kind == ProjectKind.NativeCpp)
        {
            var msbuild = tools.MsBuild ?? throw new BridgeBuildException(
                "This PC has no MSBuild for C++. Install Visual Studio 2022 or its Build Tools with the \"Desktop development with C++\" workload.");
            if (!tools.HasMsvc)
            {
                throw new BridgeBuildException(
                    "This PC's Visual Studio has no C++ compiler. Add the \"Desktop development with C++\" workload in the Visual Studio Installer.");
            }

            var platform = project.GetProperty("Platform") is { Length: > 0 } declared && !declared.Contains("$(", StringComparison.Ordinal) ? declared : "x64";
            return MsBuild(msbuild, tools, project, [$"-p:Configuration={configuration}", $"-p:Platform={platform}"], skipReferences, restore: false, isNative: true);
        }

        if (WindowsBridgeCompatibility.IsNetFramework(project))
        {
            var msbuild = tools.MsBuild ?? throw new BridgeBuildException(
                ".NET Framework projects build with MSBuild. Install Visual Studio 2022 or its Build Tools with the \".NET desktop build tools\" workload.");
            return MsBuild(msbuild, tools, project, [$"-p:Configuration={configuration}"], skipReferences, restore: true, isNative: false);
        }

        if (project.Kind == ProjectKind.WinUI)
        {
            // Run straight from the build output, as an unpackaged app; it carries the Windows App SDK when the PC lacks it.
            var winui = new List<string> { $"-p:Configuration={configuration}", "-p:Platform=x64", "-p:WindowsPackageType=None" };
            if (!tools.HasWindowsAppRuntime)
            {
                winui.Add("-p:WindowsAppSDKSelfContained=true");
            }

            if (tools.MsBuild is { } msbuild)
            {
                return MsBuild(msbuild, tools, project, winui, skipReferences, restore: true, isNative: false);
            }

            return DotNet(tools, project, configuration, skipReferences, winui.Skip(1).ToList());
        }

        return DotNet(tools, project, configuration, skipReferences, []);
    }

    private static BridgeBuildPlan DotNet(BridgeToolset tools, ProjectInfo project, string configuration, bool skipReferences, List<string> properties)
    {
        var dotnet = tools.DotNet ?? throw new BridgeBuildException(
            $"{project.Name} builds with the .NET SDK, and this PC has none. Install it from https://dot.net, then start the Windows Bridge again.");
        List<string> build = ["build", project.FilePath, "-c", configuration, "-nologo", "-v:minimal", "-clp:NoSummary;ForceNoAlign", "-tl:off", .. properties];
        if (skipReferences)
        {
            build.Add("--no-dependencies");
        }

        List<string> query = ["msbuild", project.FilePath, "-nologo", $"-p:Configuration={configuration}", .. properties, .. Properties.Select(p => $"-getProperty:{p}")];
        return new BridgeBuildPlan(dotnet, build, query, "dotnet build", IsNative: false);
    }

    private static BridgeBuildPlan MsBuild(string msbuild, BridgeToolset tools, ProjectInfo project, List<string> properties, bool skipReferences, bool restore, bool isNative)
    {
        List<string> build = [project.FilePath, "-nologo", "-v:minimal", "-clp:NoSummary;ForceNoAlign", "-m", .. properties];
        if (restore)
        {
            build.Insert(1, "-restore");
        }

        if (skipReferences)
        {
            build.Add("-p:BuildProjectReferences=false");
        }

        List<string> query = [project.FilePath, "-nologo", .. properties, .. Properties.Select(p => $"-getProperty:{p}")];
        var version = tools.MsBuildVersion is { } v ? $"MSBuild {string.Join('.', v.Split('.').Take(2))}" : "MSBuild";
        return new BridgeBuildPlan(msbuild, build, query, version, isNative);
    }
}
