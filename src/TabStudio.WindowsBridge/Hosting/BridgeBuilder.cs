using System.Diagnostics;
using System.Text.Json;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.WindowsBridge.Builds;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>
/// Builds a project in the mirror with the PC's own tools, as <see cref="BridgeBuildPlan"/> says: every line of
/// output goes to the tablet with its paths mapped, every error and warning becomes an Error List entry on the
/// tablet's file, and a successful build names the program it made.
/// </summary>
internal static class BridgeBuilder
{
    /// <summary>Reported when the PC lacks a tool the project needs.</summary>
    public const string MissingToolId = "TS3001";

    /// <summary>Reported when the build tool could not be started.</summary>
    public const string ToolFailedId = "TS3002";

    /// <summary>Builds a project.</summary>
    /// <param name="request">What to build, on the tablet's paths.</param>
    /// <param name="paths">The workspace's paths.</param>
    /// <param name="tools">The PC's tools.</param>
    /// <param name="output">Receives each line of output, on the tablet's paths.</param>
    /// <param name="cancellationToken">Stops the build.</param>
    public static async Task<BuildOutcome> BuildAsync(BuildRequest request, BridgePathMapper paths, BridgeToolset tools, Action<string> output, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var hostProject = paths.ToHost(request.ProjectPath)
            ?? throw new TabStudio.Link.LinkRemoteException(TabStudio.Link.LinkErrorCodes.InvalidRequest, $"{request.ProjectPath} is not in the workspace the bridge has.");
        if (!File.Exists(hostProject))
        {
            throw new TabStudio.Link.LinkRemoteException(TabStudio.Link.LinkErrorCodes.NoWorkspace, $"The bridge has no {Path.GetFileName(hostProject)}; send the workspace first.");
        }

        if (MplabBuilder.IsDescriptor(hostProject)) return await MplabBuilder.BuildAsync(request, hostProject, paths, tools.Mplab, output, cancellationToken).ConfigureAwait(false);
        var project = ProjectParser.Load(hostProject);
        BridgeBuildPlan plan;
        try
        {
            plan = BridgeBuildPlan.Create(project, request.Configuration, request.SkipReferences, tools);
        }
        catch (BridgeBuildException ex)
        {
            output($"error {MissingToolId}: {ex.Message}");
            return Failed(project, MissingToolId, ex.Message, clock.Elapsed, "the Windows Bridge");
        }

        output($"Building {project.Name} with {plan.Tool} on the PC...");
        var diagnostics = new List<BuildDiagnostic>();
        var seen = new HashSet<(string?, int, int, string, string)>();
        var arrows = new List<string>();
        var gate = new Lock();
        void OnLine(string line)
        {
            output(paths.ToClientText(line));
            var message = MsBuildMessage.Parse(line);
            lock (gate)
            {
                if (message is not null && seen.Add((message.HasFile ? message.Origin.ToUpperInvariant() : null, message.Line, message.Column, message.Code, message.Text)))
                {
                    diagnostics.Add(paths.ToDiagnostic(message, project.DirectoryPath, project.Name));
                }
                else if (message is null && line.Contains(" -> ", StringComparison.Ordinal))
                {
                    arrows.Add(line);
                }
            }
        }

        int exitCode;
        try
        {
            exitCode = await RunAsync(plan.FileName, plan.Arguments, project.DirectoryPath, OnLine, cancellationToken).ConfigureAwait(false);
        }
        catch (BridgeProcessException ex)
        {
            output($"error {ToolFailedId}: {ex.Message}");
            return Failed(project, ToolFailedId, ex.Message, clock.Elapsed, plan.Tool);
        }

        List<BuildDiagnostic> found;
        lock (gate)
        {
            found = [.. diagnostics.OrderByDescending(d => d.Severity)];
        }

        var success = exitCode == 0 && found.All(d => d.Severity != DiagnosticSeverityLevel.Error);
        if (!success && found.All(d => d.Severity != DiagnosticSeverityLevel.Error))
        {
            found.Insert(0, new BuildDiagnostic("MSB0000", DiagnosticSeverityLevel.Error, $"{plan.Tool} stopped with exit code {exitCode} without naming an error; see the Output panel.", null, project.Name));
        }

        var program = success ? await FindProgramAsync(plan, project, paths, arrows, cancellationToken).ConfigureAwait(false) : null;
        return new BuildOutcome(success, found, clock.Elapsed, plan.Tool, program);
    }

    /// <summary>Runs a tool to its end, passing its lines on, and stops it when canceled.</summary>
    internal static async Task<int> RunAsync(string fileName, IReadOnlyList<string> arguments, string directory, Action<string> line, CancellationToken cancellationToken, IReadOnlyDictionary<string, string?>? environment = null, string? windowsCommandLine = null)
    {
        using var process = BridgeProcess.Start(new BridgeProcessStart(fileName, arguments, directory, environment) { WindowsCommandLine = windowsCommandLine });
        process.OutputLine += (_, text) => line(text);
        process.ErrorLine += (_, text) => line(text);
        process.BeginReading();
        using (cancellationToken.Register(process.Kill))
        {
            var code = await process.Exited.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return code;
        }
    }

    /// <summary>
    /// The program a build made: MSBuild's <c>TargetPath</c> and <c>RunCommand</c> properties, or else the last
    /// <c>Project -&gt; path</c> line of the build's output.
    /// </summary>
    private static async Task<BuiltProgram?> FindProgramAsync(BridgeBuildPlan plan, ProjectInfo project, BridgePathMapper paths, List<string> arrows, CancellationToken cancellationToken)
    {
        var json = new System.Text.StringBuilder();
        string? targetPath = null;
        string? runCommand = null;
        try
        {
            await RunAsync(plan.FileName, plan.PropertyArguments, project.DirectoryPath, l => json.AppendLine(l), cancellationToken).ConfigureAwait(false);
            var text = json.ToString();
            var start = text.IndexOf('{', StringComparison.Ordinal);
            var end = text.LastIndexOf('}');
            using var document = JsonDocument.Parse(start >= 0 && end > start ? text[start..(end + 1)] : "{}");
            if (document.RootElement.TryGetProperty("Properties", out var properties))
            {
                targetPath = NonEmpty(properties, "TargetPath");
                runCommand = NonEmpty(properties, "RunCommand");
            }
        }
        catch (Exception ex) when (ex is BridgeProcessException or JsonException)
        {
            // An MSBuild older than 17.8 has no -getProperty; the output's arrows name the program instead.
        }

        targetPath ??= arrows
            .Select(line => line[(line.LastIndexOf(" -> ", StringComparison.Ordinal) + 4)..].Trim())
            .LastOrDefault(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        if (targetPath is null || !File.Exists(targetPath))
        {
            return null;
        }

        var executable = plan.IsNative
            ? targetPath
            : runCommand is { } command && command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(command)
                ? command
                : targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? targetPath : null;
        return new BuiltProgram(paths.ToClient(targetPath), executable is null ? null : paths.ToClient(executable), IsDotNet: !plan.IsNative);
    }

    private static string? NonEmpty(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    private static BuildOutcome Failed(ProjectInfo project, string id, string message, TimeSpan duration, string tool) =>
        new(false, [new BuildDiagnostic(id, DiagnosticSeverityLevel.Error, message, null, project.Name)], duration, tool, null);
}
