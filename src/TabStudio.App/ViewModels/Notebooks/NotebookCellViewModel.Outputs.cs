using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.Core.Diagnostics;
using TabStudio.Notebooks.Execution;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>A cell's runs: whether it waits or runs, its outputs, compile errors, execution count and time.</summary>
public sealed partial class NotebookCellViewModel
{
    private IReadOnlyList<NotebookOutput> _outputs;
    private CellRunState _stateBeforeQueue;

    /// <summary>What the cell produced when it last ran.</summary>
    public ObservableCollection<NotebookOutputViewModel> Outputs { get; } = [];

    /// <summary>The compile errors and warnings of the last run, listed under the cell.</summary>
    public ObservableCollection<CellDiagnosticViewModel> DiagnosticItems { get; } = [];

    /// <summary>The diagnostics of the last run with a location, for squiggles. Positions are relative to the cell.</summary>
    public IReadOnlyList<BuildDiagnostic> Diagnostics { get; private set; } = [];

    /// <summary>Whether the cell has outputs.</summary>
    public bool HasOutputs => Outputs.Count > 0;

    /// <summary>Whether compile errors or warnings are listed under the cell.</summary>
    public bool HasDiagnostics => DiagnosticItems.Count > 0;

    /// <summary>The kernel's execution count when the cell last ran.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExecutionLabel))]
    public partial int? ExecutionCount { get; private set; }

    /// <summary>How the last run went.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExecutionLabel), nameof(IsRunning), nameof(IsQueued), nameof(IsWorking), nameof(HasSucceeded), nameof(HasFailed), nameof(WasInterrupted), nameof(HasRunStatus))]
    public partial CellRunState RunState { get; private set; }

    /// <summary>Whether the cell is running.</summary>
    public bool IsRunning => RunState == CellRunState.Running;

    /// <summary>Whether the cell waits for the cells before it.</summary>
    public bool IsQueued => RunState == CellRunState.Queued;

    /// <summary>Whether the cell is running or waiting to run.</summary>
    public bool IsWorking => RunState is CellRunState.Running or CellRunState.Queued;

    /// <summary>Whether the last run succeeded.</summary>
    public bool HasSucceeded => RunState == CellRunState.Succeeded;

    /// <summary>Whether the last run failed.</summary>
    public bool HasFailed => RunState == CellRunState.Failed;

    /// <summary>Whether the last run was interrupted.</summary>
    public bool WasInterrupted => RunState == CellRunState.Interrupted;

    /// <summary>Whether the cell ran in this session, so its outcome and time are shown.</summary>
    public bool HasRunStatus => RunState is CellRunState.Succeeded or CellRunState.Failed or CellRunState.Interrupted;

    /// <summary>The execution count in brackets, as Jupyter shows it: <c>[3]</c>, <c>[*]</c> while running, <c>[ ]</c> before.</summary>
    public string ExecutionLabel => IsWorking
        ? "[*]"
        : ExecutionCount is { } count ? string.Create(CultureInfo.InvariantCulture, $"[{count}]") : "[ ]";

    /// <summary>How long the last run took, such as "0.4s".</summary>
    [ObservableProperty]
    public partial string? DurationText { get; private set; }

    /// <summary>Raised when <see cref="Diagnostics"/> changes.</summary>
    public event EventHandler? DiagnosticsChanged;

    /// <summary>Shows that the cell waits for its turn.</summary>
    internal void MarkQueued()
    {
        if (!IsWorking)
        {
            _stateBeforeQueue = RunState;
        }

        RunState = CellRunState.Queued;
    }

    /// <summary>Takes the cell out of the queue without running it.</summary>
    internal void CancelQueued()
    {
        if (RunState == CellRunState.Queued)
        {
            RunState = _stateBeforeQueue;
        }
    }

    /// <summary>Shows that the cell started running: its old outputs and errors go away.</summary>
    internal void BeginRun()
    {
        RunState = CellRunState.Running;
        DurationText = null;
        Outputs.Clear();
        SetDiagnostics([]);
    }

    /// <summary>Adds output that arrived while the cell runs. Console text joins the text before it.</summary>
    /// <param name="output">The new output.</param>
    internal void AppendLiveOutput(NotebookOutput output)
    {
        if (output is StreamOutput stream && Outputs.Count > 0 && Outputs[^1] is StreamOutputViewModel last && last.Stream == stream.Name)
        {
            last.Append(stream.Text);
        }
        else
        {
            Outputs.Add(NotebookOutputViewModel.Create(output));
        }
    }

    /// <summary>Shows the outcome of a run: its outputs, errors, execution count and time.</summary>
    /// <param name="result">What the kernel returned.</param>
    internal void CompleteRun(CellExecutionResult result)
    {
        var didNotRun = result.Status == CellExecutionStatus.Skipped
            || (result.Status == CellExecutionStatus.Interrupted && result.ExecutionCount is null);
        if (didNotRun)
        {
            // Stopped before it started: it looks as it did before.
            RunState = _stateBeforeQueue;
            ShowOutputs(_outputs, hideCompilationErrors: false);
            return;
        }

        _outputs = result.Outputs;
        ExecutionCount = result.ExecutionCount ?? ExecutionCount;
        var located = result.Diagnostics
            .Where(d => d.Severity >= DiagnosticSeverityLevel.Warning && d.Location is not null)
            .ToList();
        SetDiagnostics(located);

        // Compile errors are listed with their locations under the cell; the error output would repeat them.
        var everyErrorListed = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverityLevel.Error)
            .All(d => d.Location is not null);
        ShowOutputs(result.Outputs, hideCompilationErrors: everyErrorListed && located.Count > 0);

        RunState = result.Status switch
        {
            CellExecutionStatus.Succeeded => CellRunState.Succeeded,
            CellExecutionStatus.Interrupted => CellRunState.Interrupted,
            _ => CellRunState.Failed,
        };
        DurationText = FormatDuration(result.Duration);
    }

    /// <summary>Removes the outputs and the execution count, as if the cell had never run.</summary>
    /// <returns>Whether there was anything to clear.</returns>
    internal bool ClearOutputs()
    {
        var hadOutputs = _outputs.Count > 0 || ExecutionCount is not null || Outputs.Count > 0;
        _outputs = [];
        ExecutionCount = null;
        Outputs.Clear();
        SetDiagnostics([]);
        if (!IsWorking)
        {
            RunState = CellRunState.Idle;
        }

        DurationText = null;
        return hadOutputs;
    }

    private void ShowOutputs(IReadOnlyList<NotebookOutput> outputs, bool hideCompilationErrors)
    {
        Outputs.Clear();
        foreach (var output in outputs)
        {
            if (hideCompilationErrors && output is ErrorOutput { Name: ErrorOutputViewModel.CompilationErrorName })
            {
                continue;
            }

            Outputs.Add(NotebookOutputViewModel.Create(output));
        }
    }

    private void SetDiagnostics(IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        Diagnostics = diagnostics;
        DiagnosticItems.Clear();
        foreach (var diagnostic in diagnostics.OrderByDescending(d => d.Severity).ThenBy(d => d.Location?.Start ?? 0))
        {
            DiagnosticItems.Add(new CellDiagnosticViewModel(diagnostic, this));
        }

        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnOutputsChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(HasOutputs));

    private void OnDiagnosticItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(HasDiagnostics));

    private static string FormatDuration(TimeSpan duration) => duration.TotalSeconds < 60
        ? string.Create(CultureInfo.CurrentCulture, $"{duration.TotalSeconds:0.0}s")
        : string.Create(CultureInfo.CurrentCulture, $"{(int)duration.TotalMinutes}m {duration.Seconds}s");
}
