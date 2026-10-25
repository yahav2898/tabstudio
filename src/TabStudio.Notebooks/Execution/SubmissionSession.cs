using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Notebooks.Execution;

/// <summary>A cell compiled as the next submission of a session.</summary>
/// <param name="Compilation">The Roslyn compilation, which chains to the previous cell's.</param>
/// <param name="ClassName">The name of the class the submission compiles to.</param>
/// <param name="Diagnostics">Errors and warnings, with cell-relative positions.</param>
/// <param name="HasCode">Whether there is code to run; a cell of only <c>using</c> directives has none.</param>
/// <param name="HasValue">Whether the cell ends with an expression whose value is its result.</param>
internal sealed record SubmissionCompilation(
    CSharpCompilation Compilation,
    string ClassName,
    List<BuildDiagnostic> Diagnostics,
    bool HasCode,
    bool HasValue);

/// <summary>
/// The state a kernel carries from cell to cell: the chain of compiled submissions, the objects that hold
/// their variables, and the load context their assemblies live in.
/// </summary>
/// <remarks>
/// <para>
/// This drives Roslyn's interactive submissions directly, as <c>Microsoft.CodeAnalysis.Scripting</c> does
/// internally, rather than using that package: its scripts always reference the running runtime's
/// <c>System.Private.CoreLib</c> by file path, which does not exist on Android, where the app's assemblies are
/// stored inside the APK. Here every reference is in memory.
/// </para>
/// <para>
/// Each cell compiles to a class <c>Submission#N</c> in its own assembly, with the previous cell's compilation
/// as its predecessor, so everything earlier cells declared is in scope. Running a cell creates an instance of
/// that class, which stores itself in a shared array of submission objects; later cells find earlier
/// variables there. Slot 0 holds the globals object, the notebook host's <c>NotebookContext</c>.
/// </para>
/// </remarks>
internal sealed class SubmissionSession : IDisposable
{
    private const string SubmissionClassPrefix = "Submission#";
    private const string InitializeMethodName = "<Initialize>";

    private static readonly EmitOptions EmitOptions = new(debugInformationFormat: DebugInformationFormat.PortablePdb);

    private readonly SubmissionLoadContext _context;
    private readonly List<MetadataReference> _references;
    private readonly CSharpCompilationOptions _compilationOptions;
    private readonly Type _globalsType;
    private readonly string _assemblyPrefix;
    private object?[] _submissions = new object?[16];
    private int _submissionCount = 1;
    private int _compiledCount;
    private CSharpCompilation? _previous;

    /// <param name="host">The compiled notebook host.</param>
    /// <param name="options">The kernel's options.</param>
    /// <param name="display">Called when a cell displays a value.</param>
    /// <param name="cancellationTokenProvider">Returns the running cell's interrupt token.</param>
    public SubmissionSession(
        NotebookHostImage host,
        CSharpKernelOptions options,
        Action<object?, string[]> display,
        Func<CancellationToken> cancellationTokenProvider)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        _assemblyPrefix = $"TabStudio.Notebook.{id}.Cell";
        _context = new SubmissionLoadContext($"TabStudio notebook {id}");

        var hostAssembly = _context.LoadImage(host.Image, symbols: null);
        _globalsType = hostAssembly.GetType(NotebookHostImage.ContextTypeName, throwOnError: true)!;
        _submissions[0] = Activator.CreateInstance(_globalsType, display, cancellationTokenProvider);

        _references = [.. options.FrameworkReferences, host.Reference];
        _compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            usings: [.. options.Imports, NotebookHostImage.Namespace],
            optimizationLevel: OptimizationLevel.Debug,
            allowUnsafe: options.AllowUnsafe,
            nullableContextOptions: options.Nullable ? NullableContextOptions.Enable : NullableContextOptions.Disable);
    }

    /// <summary>How cells are parsed: as C# script, with the preprocessor symbols of a .NET 10 project.</summary>
    public static CSharpParseOptions ParseOptions { get; } = CSharpCompiler.CreateParseOptions().WithKind(SourceCodeKind.Script);

    /// <summary>Adds what a <c>#r</c> reference resolved to, for this cell and every later one.</summary>
    public void AddReferences(NotebookReferenceResolution resolution)
    {
        _references.AddRange(resolution.References);
        foreach (var path in resolution.RuntimeAssemblyPaths)
        {
            _context.AddRuntimeAssembly(path);
        }
    }

    /// <summary>Compiles a cell as the next submission. Nothing changes until it is loaded or committed.</summary>
    /// <param name="source">The cell's code, with kernel directives blanked out.</param>
    /// <param name="path">The path diagnostics and stack traces show for the cell: its id.</param>
    /// <param name="cancellationToken">Cancels compiling.</param>
    public SubmissionCompilation Compile(string source, string path, CancellationToken cancellationToken)
    {
        var number = (_compiledCount++).ToString(CultureInfo.InvariantCulture);
        var className = SubmissionClassPrefix + number;
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), ParseOptions, path, cancellationToken);
        var compilation = CSharpCompilation.CreateScriptCompilation(
            _assemblyPrefix + number,
            tree,
            _references,
            _compilationOptions.WithScriptClassName(className),
            _previous,
            returnType: null,
            globalsType: _globalsType);

        var diagnostics = DiagnosticMapper.Map(compilation.GetDiagnostics(cancellationToken)).ToList();
        diagnostics.AddRange(InProcessSafetyAnalyzer.Analyze(compilation, projectName: null, cancellationToken));

        var root = tree.GetCompilationUnitRoot(cancellationToken);
        var hasErrors = diagnostics.Exists(d => d.Severity == DiagnosticSeverityLevel.Error);
        var hasValue = !hasErrors && HasResult(compilation, root, cancellationToken);
        return new SubmissionCompilation(compilation, className, diagnostics, root.Members.Count > 0, hasValue);
    }

    /// <summary>
    /// Makes a cell without code, such as one with only <c>using</c> directives, part of the session so that
    /// later cells see what it declared.
    /// </summary>
    public void Commit(SubmissionCompilation submission) => _previous = submission.Compilation;

    /// <summary>
    /// Emits and loads a compiled cell, creates its submission object and makes it part of the session.
    /// </summary>
    /// <param name="submission">A compilation without errors.</param>
    /// <param name="diagnostics">Receives errors if emitting fails.</param>
    /// <param name="cancellationToken">Cancels emitting.</param>
    /// <returns>Runs the cell's code and returns the value of its last expression; <see langword="null"/> if emitting failed.</returns>
    public Func<Task<object?>>? Load(SubmissionCompilation submission, List<BuildDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        using var image = new MemoryStream();
        using var symbols = new MemoryStream();
        var emit = submission.Compilation.Emit(image, symbols, options: EmitOptions, cancellationToken: cancellationToken);
        if (!emit.Success)
        {
            diagnostics.AddRange(DiagnosticMapper.Map(emit.Diagnostics).Where(d => d.Severity == DiagnosticSeverityLevel.Error));
            return null;
        }

        var assembly = _context.LoadImage(image.ToArray(), symbols.ToArray());
        var type = assembly.GetType(submission.ClassName, throwOnError: true)!;
        var initialize = type.GetMethod(InitializeMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"The compiled cell has no {InitializeMethodName} method.");

        // The constructor stores the new submission object in its slot of the array and links it to the
        // objects of earlier cells. It runs none of the cell's code, so it is safe to run here.
        if (_submissionCount >= _submissions.Length)
        {
            Array.Resize(ref _submissions, _submissions.Length * 2);
        }

        var instance = Activator.CreateInstance(type, new object?[] { _submissions });
        if (_submissions[_submissionCount] is not null)
        {
            _submissionCount++;
        }

        _previous = submission.Compilation;
        return () => (Task<object?>)initialize.Invoke(instance, null)!;
    }

    /// <summary>Unloads the session's assemblies where the runtime supports it.</summary>
    public void Dispose() => _context.UnloadIfSupported();

    /// <summary>
    /// Whether a cell has a result to show: a top-level <c>return</c>, or a last statement that is an expression
    /// without a semicolon whose type is not <see langword="void"/>. This is the rule the compiler uses for the
    /// submission's return value (<c>CSharpCompilation.HasSubmissionResult</c>, which is not public).
    /// </summary>
    private static bool HasResult(CSharpCompilation compilation, CompilationUnitSyntax root, CancellationToken cancellationToken)
    {
        var hasReturn = root
            .DescendantNodes(n => n is GlobalStatementSyntax or StatementSyntax or CompilationUnitSyntax)
            .Any(n => n.IsKind(SyntaxKind.ReturnStatement));
        if (hasReturn)
        {
            return true;
        }

        var last = root.Members.LastOrDefault(m => m.IsKind(SyntaxKind.GlobalStatement)) as GlobalStatementSyntax;
        if (last?.Statement is not ExpressionStatementSyntax { SemicolonToken.IsMissing: true } statement)
        {
            return false;
        }

        var type = compilation.GetSemanticModel(root.SyntaxTree).GetTypeInfo(statement.Expression, cancellationToken).ConvertedType;
        return type?.SpecialType != SpecialType.System_Void;
    }
}
