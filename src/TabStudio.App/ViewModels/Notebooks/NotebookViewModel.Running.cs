using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.Notebooks;
using TabStudio.Notebooks.Execution;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>
/// Running cells: one at a time, in the order they were asked for, with their output appearing as it is written.
/// </summary>
/// <remarks>
/// Run requests queue up while a cell runs, as in Visual Studio Code. A run of several cells (Run All, Run Cells
/// Above) stops at the first cell that fails, so later cells do not run on missing state.
/// </remarks>
public sealed partial class NotebookViewModel
{
    private readonly List<CellRunRequest> _pending = [];
    private readonly ConcurrentQueue<CellOutputEventArgs> _liveOutputs = new();
    private CancellationTokenSource _interruption = new();
    private INotebookKernel? _kernel;
    private CellRunRequest? _current;
    private int _flushScheduled;

    /// <summary>Whether cells are running or waiting to run.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KernelStatusText))]
    [NotifyCanExecuteChangedFor(nameof(InterruptCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>The kernel's language and state, as Visual Studio Code shows it: "C# (idle)" or "C# (busy)".</summary>
    public string KernelStatusText => IsBusy ? "C# (busy)" : "C# (idle)";

    /// <summary>Runs a code cell after any cells already running. For a Markdown cell, shows it rendered.</summary>
    /// <param name="cell">The cell.</param>
    /// <returns>A task that completes when the cell has run, or was skipped or interrupted.</returns>
    public Task RunCellAsync(NotebookCellViewModel cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        if (!cell.IsCode)
        {
            cell.IsEditing = false;
            return Task.CompletedTask;
        }

        return RunCellsAsync([cell], stopOnFailure: true);
    }

    /// <summary>Shift+Enter: runs a cell and moves to the next one, adding a code cell after the last.</summary>
    /// <param name="cell">The cell.</param>
    public Task RunCellAndSelectNextAsync(NotebookCellViewModel cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var run = RunCellAsync(cell);
        var index = Cells.IndexOf(cell);
        if (index == Cells.Count - 1)
        {
            InsertCell(Cells.Count, NotebookCellKind.Code);
        }
        else if (index >= 0)
        {
            var next = Cells[index + 1];
            SelectedCell = next;
            if (next.IsCode)
            {
                next.RequestFocus();
            }
        }

        return run;
    }

    /// <summary>Alt+Enter: runs a cell and inserts a new code cell below it.</summary>
    /// <param name="cell">The cell.</param>
    public Task RunCellAndInsertBelowAsync(NotebookCellViewModel cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var run = RunCellAsync(cell);
        InsertCellBelow(cell, NotebookCellKind.Code);
        return run;
    }

    /// <summary>Runs every code cell from the top, stopping at the first that fails.</summary>
    public Task RunAllAsync() => RunCellsAsync(Cells.Where(c => c.IsCode).ToList(), stopOnFailure: true);

    /// <summary>Runs the code cells above a cell, stopping at the first that fails.</summary>
    /// <param name="cell">The cell to stop before.</param>
    public Task RunCellsAboveAsync(NotebookCellViewModel cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return RunCellsAsync(Cells.TakeWhile(c => !ReferenceEquals(c, cell)).Where(c => c.IsCode).ToList(), stopOnFailure: true);
    }

    /// <summary>Runs a cell and every code cell below it, stopping at the first that fails.</summary>
    /// <param name="cell">The first cell to run.</param>
    public Task RunCellAndBelowAsync(NotebookCellViewModel cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return RunCellsAsync(Cells.SkipWhile(c => !ReferenceEquals(c, cell)).Where(c => c.IsCode).ToList(), stopOnFailure: true);
    }

    /// <summary>Queues code cells to run in order.</summary>
    /// <param name="cells">The cells. Cells already queued or running are not queued again.</param>
    /// <param name="stopOnFailure">Whether the remaining cells are skipped when one fails.</param>
    /// <returns>A task that completes when every cell has run, or was skipped or interrupted.</returns>
    public Task RunCellsAsync(IReadOnlyList<NotebookCellViewModel> cells, bool stopOnFailure)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        if (_host.CannotRunCellsReason is { } reason)
        {
            StatusMessage = reason;
            return Task.CompletedTask;
        }

        var batch = new RunBatch(stopOnFailure);
        var completions = new List<Task>();
        foreach (var cell in cells)
        {
            if (!cell.IsCode || IsScheduled(cell))
            {
                continue;
            }

            var request = new CellRunRequest(cell, batch);
            _pending.Add(request);
            cell.MarkQueued();
            completions.Add(request.Completion.Task);
        }

        if (completions.Count == 0)
        {
            return Task.CompletedTask;
        }

        if (!IsBusy)
        {
            _ = DrainAsync();
        }

        return Task.WhenAll(completions);
    }

    [RelayCommand]
    private void RunAll() => _ = RunAllAsync();

    /// <summary>Interrupts the running cell and takes every waiting cell out of the queue.</summary>
    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Interrupt()
    {
        if (_disposed)
        {
            return;
        }

        CancelPending();

        // The old source is not disposed: the cell that is winding down may still read its token.
        var interrupted = _interruption;
        _interruption = new CancellationTokenSource();
        interrupted.Cancel();
        _kernel?.Interrupt();
        StatusMessage = "Interrupted";
    }

    /// <summary>Starts the kernel over: every variable, type and <c>using</c> of earlier cells is forgotten.</summary>
    [RelayCommand]
    private async Task RestartAsync()
    {
        if (IsBusy)
        {
            Interrupt();
        }

        if (_kernel is { } kernel)
        {
            StatusMessage = "Restarting the kernel";
            await kernel.RestartAsync(CancellationToken.None);
        }

        StatusMessage = "Restarted: variables from earlier cells are gone";
    }

    private bool IsScheduled(NotebookCellViewModel cell) =>
        ReferenceEquals(_current?.Cell, cell) || _pending.Exists(r => ReferenceEquals(r.Cell, cell));

    private async Task DrainAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        _host.OnNotebookBusyChanged(this);
        var clock = Stopwatch.StartNew();
        var ran = 0;
        try
        {
            while (_pending.Count > 0 && !_disposed)
            {
                var request = _pending[0];
                _pending.RemoveAt(0);
                if (request.Batch.IsStopped)
                {
                    request.Cell.CancelQueued();
                    request.Completion.TrySetResult();
                    continue;
                }

                _current = request;
                var result = await ExecuteAsync(request.Cell);
                _current = null;
                ran++;
                if (request.Batch.StopOnFailure && result.Status is CellExecutionStatus.CompilationFailed or CellExecutionStatus.Failed or CellExecutionStatus.Interrupted)
                {
                    request.Batch.IsStopped = true;
                }

                request.Completion.TrySetResult();
            }
        }
        finally
        {
            _current = null;
            CancelPending();
            IsBusy = false;
            _host.OnNotebookBusyChanged(this);
        }

        if (ran > 0 && StatusMessage is null)
        {
            var seconds = clock.Elapsed.TotalSeconds;
            StatusMessage = ran == 1
                ? string.Create(CultureInfo.CurrentCulture, $"Ran 1 cell in {seconds:0.0} s")
                : string.Create(CultureInfo.CurrentCulture, $"Ran {ran} cells in {seconds:0.0} s");
        }
    }

    private async Task<CellExecutionResult> ExecuteAsync(NotebookCellViewModel cell)
    {
        var token = _interruption.Token;
        cell.BeginRun();
        CellExecutionResult result;
        try
        {
            result = await GetKernel().ExecuteAsync(cell.ToCell(), token);
        }
        catch (OperationCanceledException)
        {
            result = new CellExecutionResult { CellId = cell.Id, Status = CellExecutionStatus.Interrupted };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The kernel itself failed, for example because the reference assemblies could not be loaded.
            var message = $"TabStudio could not run the cell: {ex.Message}";
            result = new CellExecutionResult
            {
                CellId = cell.Id,
                Status = CellExecutionStatus.Failed,
                Outputs = [new ErrorOutput(ex.GetType().FullName ?? ex.GetType().Name, ex.Message, [message])],
                Exception = ex,
            };
        }

        // Output still on its way is part of the result already.
        _liveOutputs.Clear();
        cell.CompleteRun(result);
        if (KeepsOutputs)
        {
            MarkChanged();
        }

        return result;
    }

    private INotebookKernel GetKernel()
    {
        if (_kernel is null)
        {
            _kernel = _host.CreateKernel();
            _kernel.OutputReceived += OnKernelOutput;
        }

        return _kernel;
    }

    private void OnKernelOutput(object? sender, CellOutputEventArgs e)
    {
        _liveOutputs.Enqueue(e);
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 0)
        {
            _host.Ui.Post(FlushLiveOutputs);
        }
    }

    private void FlushLiveOutputs()
    {
        Interlocked.Exchange(ref _flushScheduled, 0);
        while (_liveOutputs.TryDequeue(out var output))
        {
            if (_current?.Cell is { IsRunning: true } cell && cell.Id == output.CellId)
            {
                cell.AppendLiveOutput(output.Output);
            }
        }
    }

    private void RemoveFromQueue(NotebookCellViewModel cell)
    {
        foreach (var request in _pending.Where(r => ReferenceEquals(r.Cell, cell)).ToList())
        {
            _pending.Remove(request);
            cell.CancelQueued();
            request.Completion.TrySetResult();
        }
    }

    private void CancelPending()
    {
        foreach (var request in _pending)
        {
            request.Cell.CancelQueued();
            request.Completion.TrySetResult();
        }

        _pending.Clear();
    }

    private void StopRunning()
    {
        CancelPending();
        _interruption.Cancel();
        _interruption.Dispose();
        if (_kernel is { } kernel)
        {
            kernel.OutputReceived -= OnKernelOutput;
            kernel.Dispose();
            _kernel = null;
        }
    }

    /// <summary>Cells queued together, such as by Run All.</summary>
    /// <param name="stopOnFailure">Whether a failing cell stops the rest.</param>
    private sealed class RunBatch(bool stopOnFailure)
    {
        public bool StopOnFailure { get; } = stopOnFailure;

        public bool IsStopped { get; set; }
    }

    /// <summary>A cell waiting for its turn.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="batch">The cells it was queued with.</param>
    private sealed class CellRunRequest(NotebookCellViewModel cell, RunBatch batch)
    {
        public NotebookCellViewModel Cell { get; } = cell;

        public RunBatch Batch { get; } = batch;

        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
