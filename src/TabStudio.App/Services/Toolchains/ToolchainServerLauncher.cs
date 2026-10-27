using System.Text.Json;
using System.Text.RegularExpressions;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.App.Services.Toolchains;

/// <summary>
/// Starts language servers and debug adapters where the toolchains run their programs: in TabStudio's Linux
/// environment on Android, with the servers and adapters the Toolchain Store installed, and on a desktop computer
/// from its <c>PATH</c>. A thin adapter from the language servers' <see cref="IServerProcessLauncher"/> to the
/// Toolchain Store's <see cref="IToolchainRuntime"/>.
/// </summary>
/// <remarks>
/// In the Linux environment, programs see their own files at other paths than TabStudio does: <see cref="Resolve"/>
/// returns the path a program sees, such as <c>/usr/local/bin/pyright-langserver</c>, and
/// <see cref="ToLocalPath"/> finds that file on the device. Workspace folders are shared with the environment
/// before a server starts in them, so they are at the same path for the server as for the IDE.
/// </remarks>
/// <param name="runtime">Where the programs run.</param>
public sealed class ToolchainServerLauncher(IToolchainRuntime runtime) : IServerProcessLauncher
{
    private readonly IToolchainRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    /// <inheritdoc />
    public string? Resolve(string command) => string.IsNullOrWhiteSpace(command) ? null : _runtime.Resolve(command);

    /// <inheritdoc />
    public string? ResolveForDirectory(string command, string workingDirectory) =>
        string.IsNullOrWhiteSpace(command) ? null : _runtime.ResolveForDirectory(command, workingDirectory);

    /// <inheritdoc />
    public IServerProcess Start(ServerStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        if (Directory.Exists(startInfo.WorkingDirectory))
        {
            _runtime.ShareFolder(startInfo.WorkingDirectory);
        }

        foreach (var address in startInfo.Arguments.Where(a => a.StartsWith("unix:", StringComparison.Ordinal)))
        {
            if (Path.GetDirectoryName(address[5..]) is { } folder && Directory.Exists(folder))
            {
                _runtime.ShareFolder(folder);
            }
        }

        foreach (var folder in startInfo.Arguments.Where(a => Path.IsPathRooted(a) && Directory.Exists(a)))
        {
            _runtime.ShareFolder(folder);
        }

        try
        {
            var environment = startInfo.Environment.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
            if (_runtime.Kind == RuntimeKind.Linux && Path.GetFileName(startInfo.FileName) == "jdtls")
            {
                environment.TryAdd("JAVA_HOME", "/usr/lib/jvm/java-21-openjdk");
            }

            var process = _runtime.Start(new ProcessStartSpec
            {
                Program = startInfo.FileName,
                Arguments = EmbeddedClangdArguments(startInfo),
                WorkingDirectory = startInfo.WorkingDirectory,
                Environment = environment,
            });
            return new ToolchainServerProcess(process);
        }
        catch (ToolchainRuntimeException ex)
        {
            throw new ServerStartException(ex.Message, ex);
        }
    }

    private static IReadOnlyList<string> EmbeddedClangdArguments(ServerStartInfo startInfo)
    {
        var executable = Path.GetFileName(startInfo.FileName);
        if (executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) executable = executable[..^4];
        var clangd = executable == "clangd" || executable.StartsWith("clangd-", StringComparison.Ordinal)
            && executable.Length > 7 && executable[7..].All(char.IsAsciiDigit);
        if (!clangd
            || startInfo.Arguments.Any(argument => argument is "--compile-commands-dir" or "-compile-commands-dir"
                || argument.StartsWith("--compile-commands-dir=", StringComparison.Ordinal)
                || argument.StartsWith("-compile-commands-dir=", StringComparison.Ordinal)))
        {
            return startInfo.Arguments;
        }
        try
        {
            var root = Path.GetFullPath(startInfo.WorkingDirectory);
            if (!Directory.Exists(root) || HasAncestorCompilationDatabase(root)
                || HasUserCompilationDatabaseConfiguration(root))
            {
                return startInfo.Arguments;
            }
            if (EmbeddedProjectDetector.Read(root) is not { } project
                || EmbeddedBuildPlanner.Plan(project).CompilationDatabase is not { } database
                || Path.GetDirectoryName(database) is not { } directory)
            {
                return startInfo.Arguments;
            }
            directory = Path.GetFullPath(directory);
            var relative = Path.GetRelativePath(root, directory);
            // Only planner-owned paths inside this shared workspace are created.
            // No SDK configuration, build script or compiler driver runs on open.
            if (relative != "." && !relative.StartsWith(".tabstudio" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return startInfo.Arguments;
            }
            for (var ancestor = directory; ; ancestor = Path.GetDirectoryName(ancestor)!)
            {
                if (Path.Exists(ancestor) && (File.GetAttributes(ancestor) & FileAttributes.ReparsePoint) != 0)
                {
                    return startInfo.Arguments;
                }
                if (string.Equals(ancestor, root, StringComparison.Ordinal)) break;
            }
            // Clangd ignores --compile-commands-dir when the directory does not
            // exist at startup. Keep an empty generated directory ready so its
            // normal database revalidation can observe the first successful build.
            Directory.CreateDirectory(directory);
            return [.. startInfo.Arguments, "--compile-commands-dir=" + directory];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or JsonException or InvalidOperationException or NotSupportedException or RegexMatchTimeoutException)
        {
            // Optional project metadata cannot stop a generic language server.
            // The build/configure command still reports its specific error.
            return startInfo.Arguments;
        }
    }

    private static bool HasAncestorCompilationDatabase(string root)
    {
        // Match clangd's ordinary ancestor discovery without reading its JSON.
        // Bound metadata probes even for an unusually deep workspace path.
        var directory = root;
        for (var count = 0; count < 64; count++)
        {
            if (File.Exists(Path.Combine(directory, "compile_commands.json"))) return true;
            if (Path.GetDirectoryName(directory) is not { } parent) return false;
            directory = parent;
        }
        return true; // Leave an unresolved deeper user search authoritative.
    }

    private static bool HasUserCompilationDatabaseConfiguration(string root)
    {
        var configuration = new FileInfo(Path.Combine(root, ".clangd"));
        if (!configuration.Exists) return false;
        // Preserve an explicit YAML database choice, including inline mappings.
        // This is a conservative ownership check, not another YAML interpreter.
        return (configuration.Attributes & FileAttributes.ReparsePoint) != 0 || configuration.Length > 64 * 1024
            || File.ReadAllText(configuration.FullName).Contains("CompilationDatabase", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public string? ToLocalPath(string programPath)
    {
        ArgumentNullException.ThrowIfNull(programPath);
        return _runtime.ToDevicePath(programPath, workingDirectory: null);
    }

    /// <summary>A server's or adapter's program, as the toolchain runtime started it.</summary>
    /// <param name="process">The program.</param>
    private sealed class ToolchainServerProcess(ToolchainProcess process) : IServerProcess
    {
        public Stream StandardInput => process.Input;

        public Stream StandardOutput => process.Output;

        // Servers always run on pipes, so there is a standard error; a terminal would have merged it into Output.
        public Stream StandardError => process.Error ?? Stream.Null;

        public Task<int> Exited => process.Exited;

        public void Kill() => process.Stop();

        public void Dispose() => process.Dispose();
    }
}
