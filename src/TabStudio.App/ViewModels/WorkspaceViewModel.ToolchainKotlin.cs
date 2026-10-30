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

    /// <summary>The Kotlin file each breakpoint stub stands for, so a frame Java's debugger reports in a stub shows the Kotlin file.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _kotlinStubs = new(StringComparer.Ordinal);

    /// <summary>The Kotlin file of each JVM class of the project being debugged, by binary and simple name.</summary>
    private Dictionary<string, string> _kotlinClasses = new(StringComparer.Ordinal);

    /// <summary>The Kotlin file a breakpoint stub stands for, or the path itself.</summary>
    private string KotlinSourceOf(string path) => _kotlinStubs.TryGetValue(path, out var kotlin) ? kotlin : path;

    /// <summary>
    /// The Kotlin file of a frame Java's debugger reports without a source, such as <c>MainKt.greet(String)</c>: the
    /// file whose code compiles into the frame's class. The JVM's line numbers are the Kotlin file's.
    /// </summary>
    private string? KotlinFileOfFrame(string frameName)
    {
        var call = frameName.IndexOf('(', StringComparison.Ordinal);
        var qualified = call < 0 ? frameName : frameName[..call];
        var dot = qualified.LastIndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        var type = qualified[..dot];
        return _kotlinClasses.TryGetValue(type, out var file) || _kotlinClasses.TryGetValue(type[(type.LastIndexOf('.') + 1)..], out file) ? file : null;
    }

    private static Dictionary<string, string> KotlinClassesOf(string directory)
    {
        var classes = new Dictionary<string, string>(StringComparer.Ordinal);
        var files = Directory.EnumerateFiles(directory, "*.kt", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 12 })
            .Where(f => Path.GetRelativePath(directory, f).Split(Path.DirectorySeparatorChar) is var parts
                && !parts.Any(p => p is "build" or "out" || p.StartsWith('.')))
            .Take(2000);
        foreach (var file in files)
        {
            try
            {
                foreach (var binary in KotlinProject.LineClasses(file, File.ReadAllText(file)).Values.Distinct(StringComparer.Ordinal))
                {
                    classes.TryAdd(binary, file);
                    classes.TryAdd(binary[(binary.LastIndexOf('.') + 1)..], file);
                }
            }
            catch (IOException)
            {
                // A file removed while the classes are read.
            }
        }

        return classes;
    }

    private async Task<LaunchConfiguration?> PrepareKotlinDebugAsync(ToolchainProject project, LaunchConfiguration configuration)
    {
        _kotlinClasses = await Task.Run(() => KotlinClassesOf(project.Directory));
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
            _pendingBreakpointSources = (file, breakpoints) => KotlinBreakpointSources(file, breakpoints, stubs, _kotlinStubs);
            return configuration with { Properties = properties };
        }
        catch (Exception ex) when (ex is JsonRpcException or InvalidOperationException or OperationCanceledException)
        {
            var detail = server.ServerMessage is { Length: > 0 } message ? $" {server.ServerName}: {message}" : string.Empty;
            Output.WriteLine($"Kotlin debugging could not start Java's debugger: {ex.Message.TrimEnd('.')}. If it keeps happening, update Java in Tools > Toolchain Store and press F5 again.{detail} Language Servers output has the details.", OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = "Java debugger did not start";
            return null;
        }
    }

    /// <summary>The Java sources a Kotlin file's breakpoints are sent for, one per class; <see langword="null"/> for any other file.</summary>
    private static IReadOnlyList<DapBreakpointSource>? KotlinBreakpointSources(string file, IReadOnlyList<DapSourceBreakpoint> breakpoints, string stubs, System.Collections.Concurrent.ConcurrentDictionary<string, string> sourcesOfStubs)
    {
        if (!file.EndsWith(".kt", StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
        {
            return null;
        }

        var lines = breakpoints.Select(b => b.Line).Distinct().ToList();
        var sources = KotlinProject.WriteBreakpointStubs(file, File.ReadAllText(file), lines, stubs);
        foreach (var source in sources)
        {
            sourcesOfStubs[source.StubFile] = file;
        }

        return [.. sources.Select(source => new DapBreakpointSource(
            source.StubFile,
            [.. breakpoints.Select((breakpoint, index) => (breakpoint, index)).Where(p => source.Lines.Contains(p.breakpoint.Line)).Select(p => p.index)]))];
    }
}
