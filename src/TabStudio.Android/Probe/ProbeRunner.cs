using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;

namespace TabStudio.Android.Probe;

/// <summary>One completed step of the probe.</summary>
/// <param name="Name">What was exercised, such as <c>CSharpCompiler</c>.</param>
/// <param name="Detail">What it produced.</param>
/// <param name="Duration">How long it took.</param>
internal sealed record ProbeStep(string Name, string Detail, TimeSpan Duration);

/// <summary>The outcome of a probe that passed.</summary>
/// <param name="Output">What the compiled program printed.</param>
/// <param name="Classifications">The semantic classification of the program's source, for coloring it.</param>
/// <param name="Duration">How long the whole probe took.</param>
internal sealed record ProbeReport(string Output, IReadOnlyList<ClassifiedRange> Classifications, TimeSpan Duration);

/// <summary>
/// Exercises TabStudio.Compilation on the device the way the IDE will: maps the reference assemblies from
/// storage, compiles a library and a program with <see cref="CSharpCompiler"/>, runs the program with
/// <see cref="ProgramRunner"/>, then builds the same code as a solution on disk through
/// <see cref="RoslynWorkspaceHost"/> and <see cref="ProjectBuilder"/>, classifying it on the way.
/// </summary>
/// <remarks>Runs on a background thread. Any failure surfaces as an exception.</remarks>
internal sealed class ProbeRunner
{
    private readonly string _cacheDirectory;

    /// <summary>Creates a runner that keeps its files in the app's cache folder.</summary>
    /// <param name="cacheDirectory">A folder the app owns, such as <c>Context.CacheDir</c>.</param>
    public ProbeRunner(string cacheDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheDirectory);
        _cacheDirectory = cacheDirectory;
    }

    /// <summary>Runs every step and checks the program's output.</summary>
    /// <param name="onStep">Called on the background thread after each step.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <exception cref="InvalidOperationException">A step did not produce what it should.</exception>
    public async Task<ProbeReport> RunAsync(Action<ProbeStep> onStep, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onStep);

        var total = Stopwatch.StartNew();
        var watch = Stopwatch.StartNew();
        void Completed(string name, string detail)
        {
            onStep(new ProbeStep(name, detail, watch.Elapsed));
            watch.Restart();
        }

        // 1. The .NET reference assemblies, extracted once and memory-mapped, as on a 3 GB tablet.
        var references = ReferenceAssemblies.LoadMapped(Path.Combine(_cacheDirectory, "reference-assemblies"));
        Completed("ReferenceAssemblies", $"{references.Count} assemblies memory-mapped");

        // 2. A one-off compilation, as the unit tests do it: the library, then the program that references it.
        var compiler = new CSharpCompiler(references);
        var library = Require(compiler.Compile(
            new CompilationRequest
            {
                AssemblyName = "Greetings",
                Kind = AssemblyKind.Library,
                Documents = [new SourceDocument("Greeter.cs", ProbeSources.Greeter)],
            },
            cancellationToken));
        var program = Require(compiler.Compile(
            new CompilationRequest
            {
                AssemblyName = "Probe",
                Documents = [new SourceDocument("Program.cs", ProbeSources.Program)],
                AdditionalReferences = [MetadataReference.CreateFromImage(library.Image)],
            },
            cancellationToken));
        Completed("CSharpCompiler", $"{program.Name}.dll and {library.Name}.dll, {(program.Image.Length + library.Image.Length) / 1024} KB in memory");

        // 3. Run it in-process, in its own load context, with the console captured.
        var output = await RunProgramAsync(program, [library], cancellationToken);
        Completed("ProgramRunner", $"printed \"{output}\"");

        // 4. The same code as a solution on disk, through the IDE's language service and build engine.
        var root = WriteWorkspace();
        var programPath = Path.Combine(root, "Probe", "Program.cs");
        using var host = new RoslynWorkspaceHost(references);
        host.Load(WorkspaceLoader.Load(root), FileSystemTextProvider.Instance);
        Completed("RoslynWorkspaceHost", $"{host.Projects.Count} projects loaded");

        var classifications = await host.GetClassificationsAsync(programPath, 0, ProbeSources.Program.Length, cancellationToken);
        if (classifications.Count == 0)
        {
            throw new InvalidOperationException("Semantic classification returned nothing for Program.cs.");
        }

        Completed("Classification", $"{classifications.Count} semantic spans, {SyntaxClassifier.Classify(ProbeSources.Program, cancellationToken).Count} syntactic");

        var project = host.FindProject(Path.Combine(root, "Probe", "Probe.csproj"))
            ?? throw new InvalidOperationException("The workspace did not load Probe.csproj.");
        var build = await new ProjectBuilder(host).BuildAsync(project, cancellationToken);
        if (build.EntryAssembly is not { } entry)
        {
            throw new InvalidOperationException($"ProjectBuilder failed:{Environment.NewLine}{string.Join(Environment.NewLine, build.Diagnostics)}");
        }

        Completed("ProjectBuilder", $"{build.Dependencies.Count + 1} projects built, {build.WarningCount} warnings");

        var workspaceOutput = await RunProgramAsync(entry, build.Dependencies, cancellationToken);
        Completed("Workspace run", $"printed \"{workspaceOutput}\"");

        return new ProbeReport(output, classifications, total.Elapsed);
    }

    private static CompiledAssembly Require(CompilationResult result) =>
        result.Assembly ?? throw new InvalidOperationException(
            $"{result.AssemblyName} did not compile:{Environment.NewLine}{string.Join(Environment.NewLine, result.Diagnostics)}");

    private static async Task<string> RunProgramAsync(CompiledAssembly program, IReadOnlyList<CompiledAssembly> dependencies, CancellationToken cancellationToken)
    {
        var console = new CapturingConsole();
        var result = await new ProgramRunner().RunAsync(program, dependencies, [], console, cancellationToken: cancellationToken);
        var output = console.Output.Trim();

        if (result.Outcome != RunOutcome.Completed || result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{program.Name} ended with {result.Outcome} (exit code {result.ExitCode}).{Environment.NewLine}{console.Error}",
                result.Exception);
        }

        if (output != ProbeSources.ExpectedOutput)
        {
            throw new InvalidOperationException($"{program.Name} printed \"{output}\" instead of \"{ProbeSources.ExpectedOutput}\".");
        }

        return output;
    }

    /// <summary>Writes the probe solution to a fresh folder in the cache and returns that folder.</summary>
    private string WriteWorkspace()
    {
        var root = Path.GetFullPath(Path.Combine(_cacheDirectory, "probe-workspace"));
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Write(root, "Probe.slnx", ProbeSources.Solution);
        Write(root, Path.Combine("Probe", "Probe.csproj"), ProbeSources.ProgramProject);
        Write(root, Path.Combine("Probe", "Program.cs"), ProbeSources.Program);
        Write(root, Path.Combine("Greetings", "Greetings.csproj"), ProbeSources.LibraryProject);
        Write(root, Path.Combine("Greetings", "Greeter.cs"), ProbeSources.Greeter);
        return root;
    }

    private static void Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>Collects what a program writes to the console.</summary>
    private sealed class CapturingConsole : IConsoleSink
    {
        private readonly Lock _gate = new();
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();

        public string Output
        {
            get
            {
                lock (_gate)
                {
                    return _output.ToString();
                }
            }
        }

        public string Error
        {
            get
            {
                lock (_gate)
                {
                    return _error.ToString();
                }
            }
        }

        public void WriteOutput(string text)
        {
            lock (_gate)
            {
                _output.Append(text);
            }
        }

        public void WriteError(string text)
        {
            lock (_gate)
            {
                _error.Append(text);
            }
        }
    }
}
