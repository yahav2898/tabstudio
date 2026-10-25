using System.Diagnostics;
using TabStudio.Core.Diagnostics;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Execution;

/// <summary>
/// Runs C# notebook cells inside TabStudio, one after another, with state carried from cell to cell.
/// </summary>
/// <remarks>
/// <para>
/// Like Polyglot Notebooks, every cell sees what earlier cells declared: variables, functions, types and
/// <c>using</c> directives. A cell that ends with an expression without a semicolon shows its value. Cells can
/// call <c>display(value)</c> (or <c>Display</c>) to show values as they go, <c>"&lt;b&gt;hi&lt;/b&gt;".DisplayAs("text/html")</c>
/// to show a string as HTML, and read <c>CancellationToken</c>, which is cancelled when the cell is interrupted.
/// </para>
/// <para>
/// Cells run one at a time, in the order they are submitted. Each runs on its own thread with a large stack,
/// with the console redirected to the cell. Compiling happens off the calling thread, so it is safe to call
/// the kernel from the UI thread. Events are raised on the cell's thread or a thread-pool thread, not on the
/// UI thread.
/// </para>
/// <para>
/// .NET cannot stop a thread from outside. <see cref="Interrupt"/> cancels the cell's token and, if the cell
/// has not stopped after <see cref="CSharpKernelOptions.InterruptGracePeriod"/>, stops waiting for it and
/// detaches its console; a cell that ignores its token keeps running in the background until it finishes.
/// <see cref="RestartAsync"/> starts over with no state.
/// </para>
/// <para>
/// Cells run inside TabStudio's process, so <c>Environment.Exit</c> and <c>Environment.FailFast</c> are rejected
/// before a cell runs, as for programs started with F5.
/// </para>
/// </remarks>
public sealed class CSharpKernel : IDisposable
{
    /// <summary>The cell whose code is running on the current thread or task, read by <c>display</c> and <c>CancellationToken</c>.</summary>
    private static readonly AsyncLocal<CellRun?> CurrentRun = new();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _stateGate = new();
    private CancellationTokenSource _interruption = new();
    private NotebookHostImage? _host;
    private SubmissionSession? _session;
    private string? _runningCellId;
    private int _executionCount;
    private bool _disposed;

    /// <summary>Creates a kernel. Nothing is compiled until the first cell runs.</summary>
    /// <param name="options">How cells are compiled and run, or <see langword="null"/> for the defaults.</param>
    public CSharpKernel(CSharpKernelOptions? options = null)
    {
        Options = options ?? new CSharpKernelOptions();
    }

    /// <summary>Raised when a cell starts running, after it waited for its turn.</summary>
    public event EventHandler<CellExecutionStartedEventArgs>? ExecutionStarted;

    /// <summary>Raised for each output as a cell produces it, on the thread that produced it.</summary>
    public event EventHandler<CellOutputEventArgs>? OutputReceived;

    /// <summary>Raised once for every call to <see cref="ExecuteAsync(NotebookCell, CancellationToken)"/>, however it ended.</summary>
    public event EventHandler<CellExecutionCompletedEventArgs>? ExecutionCompleted;

    /// <summary>Raised after <see cref="RestartAsync"/> has cleared the state.</summary>
    public event EventHandler? Restarted;

    /// <summary>How cells are compiled and run.</summary>
    public CSharpKernelOptions Options { get; }

    /// <summary>The execution count of the last cell that ran: 0 before the first one and after a restart.</summary>
    public int ExecutionCount => Volatile.Read(ref _executionCount);

    /// <summary>The id of the cell that is running, or <see langword="null"/> when the kernel is idle.</summary>
    public string? RunningCellId
    {
        get
        {
            lock (_stateGate)
            {
                return _runningCellId;
            }
        }
    }

    /// <summary>Whether a cell is running.</summary>
    public bool IsBusy => RunningCellId is not null;

    private CancellationToken InterruptionToken
    {
        get
        {
            lock (_stateGate)
            {
                return _interruption.Token;
            }
        }
    }

    /// <summary>Runs C# code as a cell of its own.</summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">Interrupts the cell.</param>
    public Task<CellExecutionResult> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        return ExecuteAsync(NotebookCell.Code(code), cancellationToken);
    }

    /// <summary>
    /// Runs a cell after the cells submitted before it. Markdown and raw cells are skipped, and cells in other
    /// languages are reported as unsupported.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <param name="cancellationToken">Interrupts this cell, whether it is running or still waiting its turn.</param>
    /// <returns>The outcome. Failing cells do not throw; their errors are in the result.</returns>
    public async Task<CellExecutionResult> ExecuteAsync(NotebookCell cell, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = await ExecuteCoreAsync(cell, cancellationToken).ConfigureAwait(false);
        ExecutionCompleted?.Invoke(this, new CellExecutionCompletedEventArgs(result));
        return result;
    }

    /// <summary>Runs every code cell of a notebook in order, stopping at the first that fails or is interrupted.</summary>
    /// <param name="document">The notebook.</param>
    /// <param name="cancellationToken">Interrupts the run.</param>
    /// <returns>The notebook with the new outputs, and the result of each cell.</returns>
    public Task<NotebookRunResult> RunAllAsync(NotebookDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return RunCellsAsync(document, document.Cells.Select(c => c.Id), cancellationToken);
    }

    /// <summary>Runs the code cells before a cell, not including it.</summary>
    /// <param name="document">The notebook.</param>
    /// <param name="cellId">The cell to stop before.</param>
    /// <param name="cancellationToken">Interrupts the run.</param>
    public Task<NotebookRunResult> RunCellsAboveAsync(NotebookDocument document, string cellId, CancellationToken cancellationToken = default)
    {
        var index = RequireIndex(document, cellId);
        return RunCellsAsync(document, document.Cells.Take(index).Select(c => c.Id), cancellationToken);
    }

    /// <summary>Runs a cell and every code cell after it.</summary>
    /// <param name="document">The notebook.</param>
    /// <param name="cellId">The first cell to run.</param>
    /// <param name="cancellationToken">Interrupts the run.</param>
    public Task<NotebookRunResult> RunCellAndBelowAsync(NotebookDocument document, string cellId, CancellationToken cancellationToken = default)
    {
        var index = RequireIndex(document, cellId);
        return RunCellsAsync(document, document.Cells.Skip(index).Select(c => c.Id), cancellationToken);
    }

    /// <summary>
    /// Runs cells of a notebook in the given order, stopping at the first that fails to compile, throws or is
    /// interrupted. Cells that are not code are skipped; cells in other languages are skipped and keep their outputs.
    /// </summary>
    /// <param name="document">The notebook.</param>
    /// <param name="cellIds">The ids of the cells to run.</param>
    /// <param name="cancellationToken">Interrupts the run.</param>
    public async Task<NotebookRunResult> RunCellsAsync(NotebookDocument document, IEnumerable<string> cellIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(cellIds);

        var current = document;
        var results = new List<CellExecutionResult>();
        foreach (var cellId in cellIds.ToList())
        {
            if (current.FindCell(cellId) is not { Kind: NotebookCellKind.Code } cell)
            {
                continue;
            }

            var result = await ExecuteAsync(cell, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            if (result.Status != CellExecutionStatus.Unsupported)
            {
                current = current.ReplaceCell(result.ApplyTo(cell));
            }

            if (result.Status is CellExecutionStatus.CompilationFailed or CellExecutionStatus.Failed or CellExecutionStatus.Interrupted)
            {
                break;
            }
        }

        return new NotebookRunResult(current, results);
    }

    /// <summary>
    /// Interrupts the running cell and every cell waiting its turn. Returns at once; the interrupted calls
    /// complete with <see cref="CellExecutionStatus.Interrupted"/>.
    /// </summary>
    public void Interrupt()
    {
        CancellationTokenSource interrupted;
        lock (_stateGate)
        {
            interrupted = _interruption;
            _interruption = new CancellationTokenSource();
        }

        // The old source is not disposed: cells that are still winding down may read its token.
        interrupted.Cancel();
    }

    /// <summary>
    /// Interrupts any running cell and clears all state: variables, types, <c>using</c> directives, references
    /// and the execution count.
    /// </summary>
    /// <param name="cancellationToken">Stops waiting for the running cell to be interrupted.</param>
    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Interrupt();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _session?.Dispose();
            _session = null;
            Interlocked.Exchange(ref _executionCount, 0);
        }
        finally
        {
            _gate.Release();
        }

        Restarted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Interrupts any running cell and releases the session. Call it when the notebook closes.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interrupt();
        _session?.Dispose();
        _session = null;
        lock (_stateGate)
        {
            _interruption.Dispose();
        }

        _gate.Dispose();
    }

    private static void Display(object? value, string[] mimeTypes) => CurrentRun.Value?.Display(value, mimeTypes);

    private static CancellationToken CurrentCancellationToken() => CurrentRun.Value?.CancellationToken ?? CancellationToken.None;

    private static int RequireIndex(NotebookDocument document, string cellId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(cellId);
        var index = document.IndexOf(cellId);
        return index >= 0 ? index : throw new ArgumentException($"The notebook has no cell with id '{cellId}'.", nameof(cellId));
    }

    private async Task<CellExecutionResult> ExecuteCoreAsync(NotebookCell cell, CancellationToken cancellationToken)
    {
        if (cell.Kind != NotebookCellKind.Code)
        {
            return new CellExecutionResult { CellId = cell.Id, Status = CellExecutionStatus.Skipped };
        }

        if (!NotebookLanguages.IsCSharp(cell.Language))
        {
            return Unsupported(cell);
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, InterruptionToken);
        var token = cancellation.Token;
        try
        {
            await _gate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new CellExecutionResult { CellId = cell.Id, Status = CellExecutionStatus.Interrupted };
        }

        try
        {
            var executionCount = Interlocked.Increment(ref _executionCount);
            SetRunningCell(cell.Id);
            ExecutionStarted?.Invoke(this, new CellExecutionStartedEventArgs(cell.Id, executionCount));
            return await RunCellAsync(cell, executionCount, token).ConfigureAwait(false);
        }
        finally
        {
            SetRunningCell(null);
            if (!_disposed)
            {
                _gate.Release();
            }
        }
    }

    private async Task<CellExecutionResult> RunCellAsync(NotebookCell cell, int executionCount, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var diagnostics = new List<BuildDiagnostic>();
        var result = new CellExecutionResult { CellId = cell.Id, Status = CellExecutionStatus.CompilationFailed, ExecutionCount = executionCount };

        Func<Task<object?>>? run;
        bool hasValue;
        try
        {
            var session = await GetSessionAsync(cancellationToken).ConfigureAwait(false);
            var directives = CellDirectiveParser.Parse(cell.Source, cell.Id, SubmissionSession.ParseOptions, cancellationToken);
            diagnostics.AddRange(directives.Diagnostics);
            await ResolveReferencesAsync(session, directives.References, diagnostics, cancellationToken).ConfigureAwait(false);
            if (HasErrors(diagnostics))
            {
                return CompilationFailed(result, diagnostics, stopwatch);
            }

            var compiled = await Task.Run(() => session.Compile(directives.Source, cell.Id, cancellationToken), cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(compiled.Diagnostics);
            if (HasErrors(diagnostics))
            {
                return CompilationFailed(result, diagnostics, stopwatch);
            }

            hasValue = compiled.HasValue;
            if (compiled.HasCode)
            {
                run = await Task.Run(() => session.Load(compiled, diagnostics, cancellationToken), cancellationToken).ConfigureAwait(false);
                if (run is null)
                {
                    return CompilationFailed(result, diagnostics, stopwatch);
                }
            }
            else
            {
                session.Commit(compiled);
                run = null;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return result with { Status = CellExecutionStatus.Interrupted, Diagnostics = diagnostics, Duration = stopwatch.Elapsed };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var diagnostic = new BuildDiagnostic(NotebookDiagnosticIds.InternalError, DiagnosticSeverityLevel.Error, $"TabStudio could not prepare the cell: {ex.Message}", null);
            diagnostics.Add(diagnostic);
            return CompilationFailed(result, diagnostics, stopwatch) with { Exception = ex };
        }

        var compilationDuration = stopwatch.Elapsed;
        var cellRun = new CellRun(Options.Formatter, cancellationToken, output => OutputReceived?.Invoke(this, new CellOutputEventArgs(cell.Id, output)));
        var (status, exception) = run is null
            ? (CellExecutionStatus.Succeeded, null)
            : await RunOnThreadAsync(run, hasValue, executionCount, cellRun, cancellationToken).ConfigureAwait(false);

        var outputs = cellRun.Complete();
        if (exception is not null)
        {
            outputs.Add(CellErrors.FromException(exception));
        }

        return result with
        {
            Status = status,
            Outputs = outputs,
            Diagnostics = diagnostics,
            CompilationDuration = compilationDuration,
            Duration = stopwatch.Elapsed,
            Exception = exception,
        };
    }

    /// <summary>
    /// Runs a cell's code on a thread of its own with the console redirected to the cell, and waits for it to
    /// finish or to be interrupted.
    /// </summary>
    private async Task<(CellExecutionStatus Status, Exception? Exception)> RunOnThreadAsync(
        Func<Task<object?>> run,
        bool hasValue,
        int executionCount,
        CellRun cellRun,
        CancellationToken cancellationToken)
    {
        IDisposable redirection;
        try
        {
            redirection = await ConsoleRedirection.RedirectAsync(cellRun, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return (CellExecutionStatus.Interrupted, null);
        }

        try
        {
            var completion = new TaskCompletionSource<(CellExecutionStatus, Exception?)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() => completion.TrySetResult(RunCell(run, hasValue, executionCount, cellRun)), Options.StackSize)
            {
                IsBackground = true,
                Name = "TabStudio notebook cell",
            };

            var gracePeriod = Options.InterruptGracePeriod;
            using var registration = cancellationToken.Register(() => _ = StopWaitingAfterAsync(completion, gracePeriod));
            thread.Start();

            var outcome = await completion.Task.ConfigureAwait(false);
            return cancellationToken.IsCancellationRequested ? (CellExecutionStatus.Interrupted, null) : outcome;
        }
        finally
        {
            // Detach first, so that a cell still running in the background cannot write to the next one.
            cellRun.Complete();
            redirection.Dispose();
        }
    }

    /// <summary>The body of a cell's thread: runs the code, waits for it, and formats its result.</summary>
    private static (CellExecutionStatus Status, Exception? Exception) RunCell(Func<Task<object?>> run, bool hasValue, int executionCount, CellRun cellRun)
    {
        CurrentRun.Value = cellRun;
        try
        {
            var value = run().GetAwaiter().GetResult();
            if (hasValue)
            {
                cellRun.AddResult(value, executionCount);
            }

            return (CellExecutionStatus.Succeeded, null);
        }
        catch (Exception ex)
        {
            return (CellExecutionStatus.Failed, ex);
        }
        finally
        {
            Console.Out.Flush();
            Console.Error.Flush();
        }
    }

    private static async Task StopWaitingAfterAsync(TaskCompletionSource<(CellExecutionStatus, Exception?)> completion, TimeSpan gracePeriod)
    {
        if (gracePeriod > TimeSpan.Zero)
        {
            await Task.Delay(gracePeriod).ConfigureAwait(false);
        }

        completion.TrySetResult((CellExecutionStatus.Interrupted, null));
    }

    private async Task<SubmissionSession> GetSessionAsync(CancellationToken cancellationToken)
    {
        if (_session is { } existing)
        {
            return existing;
        }

        var frameworkReferences = Options.FrameworkReferences;
        _host ??= await Task.Run(() => NotebookHostImage.Compile(frameworkReferences, cancellationToken), cancellationToken).ConfigureAwait(false);
        _session = new SubmissionSession(_host, Options, Display, CurrentCancellationToken);
        return _session;
    }

    private async Task ResolveReferencesAsync(
        SubmissionSession session,
        List<CellReference> references,
        List<BuildDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var (reference, location) in references)
        {
            if (Options.ReferenceResolver is not { } resolver)
            {
                diagnostics.Add(NotSupported(reference, location));
                continue;
            }

            NotebookReferenceResolution resolution;
            try
            {
                resolution = await resolver.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                resolution = NotebookReferenceResolution.Failed(ex.Message);
            }

            if (resolution.Succeeded)
            {
                session.AddReferences(resolution);
            }
            else
            {
                diagnostics.Add(new BuildDiagnostic(
                    NotebookDiagnosticIds.ReferenceNotResolved,
                    DiagnosticSeverityLevel.Error,
                    $"#r \"{reference}\" could not be resolved: {string.Join(" ", resolution.Errors)}",
                    location));
            }
        }
    }

    private void SetRunningCell(string? cellId)
    {
        lock (_stateGate)
        {
            _runningCellId = cellId;
        }
    }

    private static BuildDiagnostic NotSupported(NotebookReference reference, DiagnosticLocation location) =>
        reference.Kind == NotebookReferenceKind.NuGetPackage
            ? new BuildDiagnostic(
                NotebookDiagnosticIds.NuGetReferenceNotSupported,
                DiagnosticSeverityLevel.Error,
                $"NuGet packages cannot be used in TabStudio notebooks yet (#r \"{reference}\").",
                location)
            : new BuildDiagnostic(
                NotebookDiagnosticIds.ReferenceNotResolved,
                DiagnosticSeverityLevel.Error,
                $"Referencing assemblies with #r is not supported in TabStudio notebooks yet (#r \"{reference}\").",
                location);

    private static bool HasErrors(List<BuildDiagnostic> diagnostics) =>
        diagnostics.Exists(d => d.Severity == DiagnosticSeverityLevel.Error);

    private static CellExecutionResult CompilationFailed(CellExecutionResult result, List<BuildDiagnostic> diagnostics, Stopwatch stopwatch) => result with
    {
        Status = CellExecutionStatus.CompilationFailed,
        Outputs = [CellErrors.FromDiagnostics(diagnostics)],
        Diagnostics = diagnostics,
        CompilationDuration = stopwatch.Elapsed,
        Duration = stopwatch.Elapsed,
    };

    private static CellExecutionResult Unsupported(NotebookCell cell)
    {
        var message = $"TabStudio notebooks run C# cells only; {NotebookLanguages.GetDisplayName(cell.Language)} cells are not supported yet.";
        return new CellExecutionResult
        {
            CellId = cell.Id,
            Status = CellExecutionStatus.Unsupported,
            Outputs = [new ErrorOutput("NotSupported", message, [message])],
            Diagnostics = [new BuildDiagnostic(NotebookDiagnosticIds.LanguageNotSupported, DiagnosticSeverityLevel.Error, message, null)],
        };
    }
}
