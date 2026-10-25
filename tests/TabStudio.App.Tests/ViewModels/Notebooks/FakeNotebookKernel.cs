using TabStudio.App.Services;
using TabStudio.App.ViewModels.Notebooks;
using TabStudio.Core.Diagnostics;
using TabStudio.Notebooks;
using TabStudio.Notebooks.Execution;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.App.Tests.ViewModels.Notebooks;

/// <summary>
/// A kernel that answers instantly from the cell's text, so notebook editor tests run without compiling C#:
/// <c>fail</c> does not compile, <c>throw</c> throws, <c>wait</c> runs until <see cref="Release"/> or an interrupt,
/// and anything else prints its text.
/// </summary>
internal sealed class FakeNotebookKernel : INotebookKernel
{
    private TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _count;

    public event EventHandler<CellOutputEventArgs>? OutputReceived;

    /// <summary>The source of every cell run, in order.</summary>
    public List<string> Ran { get; } = [];

    /// <summary>How many times the kernel was restarted.</summary>
    public int Restarts { get; private set; }

    /// <summary>Whether a <c>wait</c> cell is running.</summary>
    public bool IsWaiting { get; private set; }

    /// <summary>Lets a running <c>wait</c> cell finish.</summary>
    public void Release() => _waiting.TrySetResult();

    public async Task<CellExecutionResult> ExecuteAsync(NotebookCell cell, CancellationToken cancellationToken)
    {
        Ran.Add(cell.Source);
        var count = ++_count;
        switch (cell.Source)
        {
            case "fail":
                var location = new DiagnosticLocation(cell.Id, 0, 4, 1, 1);
                var diagnostic = new BuildDiagnostic("CS0103", DiagnosticSeverityLevel.Error, "The name 'fail' does not exist in the current context", location);
                return new CellExecutionResult
                {
                    CellId = cell.Id,
                    Status = CellExecutionStatus.CompilationFailed,
                    ExecutionCount = count,
                    Diagnostics = [diagnostic],
                    Outputs = [new ErrorOutput("CompilationError", "(1,1): error CS0103: The name 'fail' does not exist", ["(1,1): error CS0103: The name 'fail' does not exist"])],
                };
            case "throw":
                return new CellExecutionResult
                {
                    CellId = cell.Id,
                    Status = CellExecutionStatus.Failed,
                    ExecutionCount = count,
                    Outputs = [new ErrorOutput("System.InvalidOperationException", "Boom", ["System.InvalidOperationException: Boom", "   at Submission#1.<<Initialize>>d__0.MoveNext()"])],
                };
            case "wait":
                OutputReceived?.Invoke(this, new CellOutputEventArgs(cell.Id, new StreamOutput(StreamOutput.StandardOutput, "started\n")));
                IsWaiting = true;
                try
                {
                    await _waiting.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return new CellExecutionResult { CellId = cell.Id, Status = CellExecutionStatus.Interrupted, ExecutionCount = count };
                }
                finally
                {
                    IsWaiting = false;
                    _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                return Succeeded(cell, count, "finished\n");
            default:
                await Task.Yield();
                return Succeeded(cell, count, cell.Source + "\n");
        }
    }

    public void Interrupt()
    {
    }

    public Task RestartAsync(CancellationToken cancellationToken)
    {
        Restarts++;
        _count = 0;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }

    private static CellExecutionResult Succeeded(NotebookCell cell, int count, string text) => new()
    {
        CellId = cell.Id,
        Status = CellExecutionStatus.Succeeded,
        ExecutionCount = count,
        Outputs = [new StreamOutput(StreamOutput.StandardOutput, text)],
        Duration = TimeSpan.FromMilliseconds(120),
    };
}

/// <summary>A notebook host over a <see cref="FakeNotebookKernel"/>.</summary>
internal sealed class FakeNotebookHost : INotebookHost
{
    public FakeNotebookKernel Kernel { get; } = new();

    public IUiDispatcher Ui { get; } = new AvaloniaUiDispatcher();

    public string? CannotRunCellsReason { get; set; }

    public int BusyChanges { get; private set; }

    public INotebookKernel CreateKernel() => Kernel;

    public void OnNotebookBusyChanged(NotebookViewModel notebook) => BusyChanges++;
}
