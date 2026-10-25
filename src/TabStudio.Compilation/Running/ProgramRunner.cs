using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace TabStudio.Compilation.Running;

/// <summary>How a run ended.</summary>
public enum RunOutcome
{
    /// <summary>The program returned from <c>Main</c>.</summary>
    Completed,

    /// <summary>The program threw an exception that it did not handle.</summary>
    Crashed,

    /// <summary>The user stopped the program.</summary>
    Stopped,

    /// <summary>The assembly has no entry point to call.</summary>
    NoEntryPoint,
}

/// <summary>
/// The result of running a program.
/// </summary>
/// <param name="Outcome">How the run ended.</param>
/// <param name="ExitCode">The value returned from <c>Main</c>, 0 for a <c>void</c> Main, or -1 if the program crashed.</param>
/// <param name="Duration">How long the program ran.</param>
/// <param name="Exception">The unhandled exception, when the program crashed.</param>
public sealed record RunResult(RunOutcome Outcome, int ExitCode, TimeSpan Duration, Exception? Exception = null);

/// <summary>
/// Runs a compiled program inside TabStudio's process, with its console connected to the Output panel.
/// </summary>
/// <remarks>
/// <para>
/// The program is loaded into its own <see cref="AssemblyLoadContext"/> and runs on a dedicated thread with
/// a generous stack. Its references to the .NET libraries bind to the ones TabStudio itself runs on.
/// </para>
/// <para>
/// The console is process-wide, so only one program runs at a time. Stopping a program detaches its
/// console and lets the caller continue immediately; .NET cannot abort a thread, so a program that ignores
/// the end of its input keeps running in the background until it finishes on its own.
/// </para>
/// </remarks>
public sealed class ProgramRunner
{
    /// <summary>The stack size of the program's main thread: 16 MB, generous for deeply recursive code.</summary>
    public const int DefaultStackSize = 16 * 1024 * 1024;

    private static readonly SemaphoreSlim ConsoleGate = new(1, 1);
    private readonly int _stackSize;

    /// <summary>Creates a runner.</summary>
    /// <param name="stackSize">The stack size, in bytes, of the thread that runs <c>Main</c>.</param>
    public ProgramRunner(int stackSize = DefaultStackSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stackSize, 256 * 1024);
        _stackSize = stackSize;
    }

    /// <summary>Runs a program and waits for it to finish or be stopped.</summary>
    /// <param name="entryAssembly">The program's assembly.</param>
    /// <param name="dependencies">Assemblies of referenced projects, loaded on demand.</param>
    /// <param name="arguments">Command-line arguments passed to <c>Main</c>.</param>
    /// <param name="console">Receives the program's output.</param>
    /// <param name="input">Standard input, or <see langword="null"/> for an empty input.</param>
    /// <param name="cancellationToken">Stops the program.</param>
    public Task<RunResult> RunAsync(
        CompiledAssembly entryAssembly,
        IReadOnlyList<CompiledAssembly> dependencies,
        IReadOnlyList<string> arguments,
        IConsoleSink console,
        ConsoleInput? input = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(entryAssembly, dependencies, packageAssemblies: [], arguments, console, input, cancellationToken);

    /// <summary>Runs a program that uses NuGet packages and waits for it to finish or be stopped.</summary>
    /// <param name="entryAssembly">The program's assembly.</param>
    /// <param name="dependencies">Assemblies of referenced projects, loaded on demand.</param>
    /// <param name="packageAssemblies">
    /// Full paths of the package assemblies the program may load, such as <c>RestoreResult.RuntimeAssemblies</c>.
    /// They are found by simple name (the file name without <c>.dll</c>) and loaded from disk on demand.
    /// </param>
    /// <param name="arguments">Command-line arguments passed to <c>Main</c>.</param>
    /// <param name="console">Receives the program's output.</param>
    /// <param name="input">Standard input, or <see langword="null"/> for an empty input.</param>
    /// <param name="cancellationToken">Stops the program.</param>
    public async Task<RunResult> RunAsync(
        CompiledAssembly entryAssembly,
        IReadOnlyList<CompiledAssembly> dependencies,
        IReadOnlyList<string> packageAssemblies,
        IReadOnlyList<string> arguments,
        IConsoleSink console,
        ConsoleInput? input = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entryAssembly);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(packageAssemblies);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(console);

        await ConsoleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        var context = new ProgramLoadContext(entryAssembly.Name, dependencies, packageAssemblies);
        var output = new CallbackTextWriter(console.WriteOutput);
        var error = new CallbackTextWriter(console.WriteError);
        var (previousOut, previousError, previousIn) = (Console.Out, Console.Error, Console.In);

        try
        {
            var assembly = context.LoadFromImage(entryAssembly);
            if (assembly.EntryPoint is not { } entryPoint)
            {
                return new RunResult(RunOutcome.NoEntryPoint, -1, TimeSpan.Zero);
            }

            Console.SetOut(output);
            Console.SetError(error);
            Console.SetIn(input ?? TextReader.Null);

            var completion = new TaskCompletionSource<RunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() => completion.TrySetResult(Invoke(entryPoint, arguments, error)), _stackSize)
            {
                IsBackground = true,
                Name = $"TabStudio: {entryAssembly.Name}",
            };

            var stopwatch = Stopwatch.StartNew();
            using var registration = cancellationToken.Register(() =>
            {
                input?.Complete();
                completion.TrySetResult(new RunResult(RunOutcome.Stopped, -1, stopwatch.Elapsed));
            });

            thread.Start();
            var result = await completion.Task.ConfigureAwait(false);
            return result with { Duration = stopwatch.Elapsed };
        }
        finally
        {
            output.Detach();
            error.Detach();
            Console.SetOut(previousOut);
            Console.SetError(previousError);
            Console.SetIn(previousIn);
            context.UnloadIfSupported();
            ConsoleGate.Release();
        }
    }

    private static RunResult Invoke(MethodInfo entryPoint, IReadOnlyList<string> arguments, TextWriter error)
    {
        try
        {
            var parameters = entryPoint.GetParameters().Length == 0 ? null : new object[] { arguments.ToArray() };
            var returned = entryPoint.Invoke(null, parameters);
            var exitCode = returned switch
            {
                int code => code,
                Task<int> task => task.GetAwaiter().GetResult(),
                Task task => Await(task),
                _ => 0,
            };

            Console.Out.Flush();
            return new RunResult(RunOutcome.Completed, exitCode, TimeSpan.Zero);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return Crashed(ex.InnerException, error);
        }
        catch (Exception ex)
        {
            return Crashed(ex, error);
        }
    }

    private static int Await(Task task)
    {
        task.GetAwaiter().GetResult();
        return 0;
    }

    private static RunResult Crashed(Exception exception, TextWriter error)
    {
        error.WriteLine();
        error.WriteLine($"Unhandled exception. {exception}");
        error.Flush();
        return new RunResult(RunOutcome.Crashed, -1, TimeSpan.Zero, exception);
    }

    /// <summary>
    /// Loads a program and the projects it references from memory, and its NuGet packages from disk. Everything
    /// else, such as the .NET libraries, comes from the default context, which is the runtime TabStudio itself runs on.
    /// </summary>
    private sealed class ProgramLoadContext : AssemblyLoadContext
    {
        private readonly Dictionary<string, CompiledAssembly> _dependencies;
        private readonly Dictionary<string, string> _packageAssemblies = new(StringComparer.OrdinalIgnoreCase);

        public ProgramLoadContext(string name, IReadOnlyList<CompiledAssembly> dependencies, IReadOnlyList<string> packageAssemblies)
            : base($"TabStudio program: {name}", isCollectible: SupportsUnloading)
        {
            _dependencies = dependencies.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var path in packageAssemblies)
            {
                // The first assembly of a name wins, as in the restore's list.
                _packageAssemblies.TryAdd(Path.GetFileNameWithoutExtension(path), path);
            }
        }

        /// <summary>Mono, which runs .NET on Android, does not support unloading assemblies.</summary>
        private static bool SupportsUnloading => !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS();

        public Assembly LoadFromImage(CompiledAssembly compiled)
        {
            using var image = new MemoryStream(compiled.Image, writable: false);
            using var symbols = compiled.Symbols is null ? null : new MemoryStream(compiled.Symbols, writable: false);
            return LoadFromStream(image, symbols);
        }

        public void UnloadIfSupported()
        {
            if (IsCollectible)
            {
                Unload();
            }
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is not { } name)
            {
                return null;
            }

            if (_dependencies.TryGetValue(name, out var compiled))
            {
                return LoadFromImage(compiled);
            }

            return _packageAssemblies.TryGetValue(name, out var path) ? LoadFromAssemblyPath(path) : null;
        }
    }
}
