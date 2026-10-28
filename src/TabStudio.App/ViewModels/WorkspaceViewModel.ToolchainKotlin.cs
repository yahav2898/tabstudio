using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.JsonRpc;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Kotlin on the JVM, debugged with the Java toolchain's debugger (java-debug in Eclipse JDT): JDT serves only the
/// small Java sources that tell java-debug which class each breakpoint's line is in, and java-debug finds Kotlin
/// files by their package path in the project's source folders.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private LanguageServerDocument? _kotlinDebugDocument;

    private async Task<LaunchConfiguration?> PrepareKotlinDebugAsync(ToolchainProject project, LaunchConfiguration configuration)
    {
        var stubs = Path.Combine(project.Directory, ".tabstudio", "kotlin-debug");
        Directory.CreateDirectory(stubs);
        KotlinProject.WriteBreakpointStubs(KotlinProject.MainSource(project.Directory) ?? Path.Combine(project.Directory, "Main.kt"), string.Empty, [], stubs);
        var anchor = Path.Combine(stubs, "TabStudioKotlinDebug.java");
        const string AnchorText = "class TabStudioKotlinDebug {\n}\n";
        await File.WriteAllTextAsync(anchor, AnchorText);
        _kotlinDebugDocument?.Dispose();
        _kotlinDebugDocument = _languageServers?.OpenDocument(anchor, AnchorText);
        if (_kotlinDebugDocument is not { } server)
        {
            Output.WriteLine("Kotlin debugging uses the Java debugger: install Java from the Toolchain Store, which brings Eclipse JDT and java-debug.", OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = "Kotlin debugging needs Java's debugger";
            return null;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var port = await server.ExecuteServerCommandAsync("vscode.java.startDebugSession", [], timeout.Token);
            if (!port.TryGetInt32(out var number) || number is < 1 or > 65535)
            {
                throw new InvalidOperationException("JDT did not return a Java debug adapter port.");
            }

            var properties = (JsonObject)configuration.Properties.DeepClone();
            properties["debugServer"] = number;
            var sourcePaths = new JsonArray();
            foreach (var path in KotlinProject.SourcePaths(project.Directory))
            {
                sourcePaths.Add(path);
            }

            properties["sourcePaths"] = sourcePaths;
            _pendingBreakpointSources = (file, breakpoints) => KotlinBreakpointSources(file, breakpoints, stubs);
            return configuration with { Properties = properties };
        }
        catch (Exception ex) when (ex is JsonRpcException or InvalidOperationException or OperationCanceledException)
        {
            var detail = server.ServerMessage is { Length: > 0 } message ? $" {server.ServerName}: {message}" : string.Empty;
            Output.WriteLine($"Kotlin debugging could not start Java's debugger: {ex.Message}.{detail} Install or update Java from the Toolchain Store. See Language Servers output for details.", OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = "Java debugger did not start";
            return null;
        }
    }

    /// <summary>The Java sources a Kotlin file's breakpoints are sent for, one per class; <see langword="null"/> for any other file.</summary>
    private static IReadOnlyList<DapBreakpointSource>? KotlinBreakpointSources(string file, IReadOnlyList<DapSourceBreakpoint> breakpoints, string stubs)
    {
        if (!file.EndsWith(".kt", StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
        {
            return null;
        }

        var lines = breakpoints.Select(b => b.Line).Distinct().ToList();
        var sources = KotlinProject.WriteBreakpointStubs(file, File.ReadAllText(file), lines, stubs);
        return [.. sources.Select(source => new DapBreakpointSource(
            source.StubFile,
            [.. breakpoints.Select((breakpoint, index) => (breakpoint, index)).Where(p => source.Lines.Contains(p.breakpoint.Line)).Select(p => p.index)]))];
    }
}
