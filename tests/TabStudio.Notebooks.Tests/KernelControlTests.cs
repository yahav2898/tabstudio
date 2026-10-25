using TabStudio.Notebooks.Execution;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Tests;

[Collection(KernelCollection.Name)]
public sealed class KernelControlTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly CSharpKernel _kernel = new(new CSharpKernelOptions { InterruptGracePeriod = TimeSpan.FromSeconds(5) });

    public void Dispose() => _kernel.Dispose();

    [Fact]
    public async Task Restarting_clears_state_and_the_execution_count()
    {
        await _kernel.RunAsync("var before = 1;");
        var restarted = false;
        _kernel.Restarted += (_, _) => restarted = true;

        await _kernel.RestartAsync();
        var result = await _kernel.ExecuteAsync("before");

        Assert.True(restarted);
        Assert.Equal(CellExecutionStatus.CompilationFailed, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Id == "CS0103");
        Assert.Equal(1, result.ExecutionCount);
    }

    [Fact]
    public async Task Interrupting_a_cell_that_checks_its_token_stops_it()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _kernel.OutputReceived += (_, e) => started.TrySetResult();

        var running = _kernel.ExecuteAsync("""
            Console.WriteLine("started");
            while (!CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10);
            }

            Console.WriteLine("stopped cleanly");
            """);
        await started.Task.WaitAsync(Timeout);
        Assert.True(_kernel.IsBusy);

        _kernel.Interrupt();
        var result = await running.WaitAsync(Timeout);

        Assert.Equal(CellExecutionStatus.Interrupted, result.Status);
        Assert.Equal("started\nstopped cleanly\n", result.StreamText());
        Assert.False(_kernel.IsBusy);
    }

    [Fact]
    public async Task Interrupting_a_cell_that_ignores_its_token_returns_without_waiting_for_it()
    {
        using var kernel = new CSharpKernel(new CSharpKernelOptions { InterruptGracePeriod = TimeSpan.FromMilliseconds(100) });
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        kernel.OutputReceived += (_, e) => started.TrySetResult();

        var running = kernel.ExecuteAsync("""
            Console.WriteLine("started");
            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
            """);
        await started.Task.WaitAsync(Timeout);

        kernel.Interrupt();
        var result = await running.WaitAsync(Timeout);
        var after = await kernel.RunAsync("Console.WriteLine(\"still here\"); 1 + 1");

        Assert.Equal(CellExecutionStatus.Interrupted, result.Status);
        Assert.Equal(1, result.ExecutionCount);
        Assert.Equal("still here\n", after.StreamText());
        Assert.Equal("2", after.ResultText());
    }

    [Fact]
    public async Task Cancelling_the_token_passed_in_interrupts_the_cell()
    {
        using var cancellation = new CancellationTokenSource();
        _kernel.OutputReceived += (_, e) => cancellation.Cancel();

        var result = await _kernel.ExecuteAsync(
            "Console.WriteLine(\"go\"); await Task.Delay(-1, CancellationToken);",
            cancellation.Token).WaitAsync(Timeout);

        Assert.Equal(CellExecutionStatus.Interrupted, result.Status);
    }

    [Fact]
    public async Task Interrupting_also_cancels_cells_waiting_their_turn()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _kernel.OutputReceived += (_, e) => started.TrySetResult();

        var running = _kernel.ExecuteAsync("Console.WriteLine(1); await Task.Delay(-1, CancellationToken);");
        await started.Task.WaitAsync(Timeout);
        var queued = _kernel.ExecuteAsync("2");

        _kernel.Interrupt();
        var queuedResult = await queued.WaitAsync(Timeout);
        await running.WaitAsync(Timeout);

        Assert.Equal(CellExecutionStatus.Interrupted, queuedResult.Status);
        Assert.Null(queuedResult.ExecutionCount);
    }

    [Fact]
    public async Task Run_all_runs_code_cells_in_order_and_fills_in_their_outputs()
    {
        var document = NotebookDocument.Create(
            NotebookCell.Markdown("# Squares"),
            NotebookCell.Code("var numbers = Enumerable.Range(1, 3).ToList();"),
            NotebookCell.Code("Console.WriteLine(numbers.Count);"),
            NotebookCell.Code("numbers.Select(n => n * n).Sum()"));

        var run = await _kernel.RunAllAsync(document);

        Assert.True(run.Succeeded);
        Assert.Equal(3, run.Results.Count);
        var cells = run.Document.Cells;
        Assert.Equal(new int?[] { null, 1, 2, 3 }, cells.Select(c => c.ExecutionCount));
        Assert.Equal("3\n", Assert.IsType<StreamOutput>(Assert.Single(cells[2].Outputs)).Text.ReplaceLineEndings("\n"));
        Assert.Equal("14", Assert.IsType<DisplayOutput>(Assert.Single(cells[3].Outputs)).Data.PlainText);
        Assert.Empty(document.Cells[3].Outputs);
    }

    [Fact]
    public async Task Run_all_stops_at_the_first_cell_that_fails()
    {
        var document = NotebookDocument.Create(
            NotebookCell.Code("var ok = 1;"),
            NotebookCell.Code("throw new InvalidOperationException(\"stop\");"),
            NotebookCell.Code("Console.WriteLine(\"never\");"));

        var run = await _kernel.RunAllAsync(document);

        Assert.False(run.Succeeded);
        Assert.Equal(new[] { CellExecutionStatus.Succeeded, CellExecutionStatus.Failed }, run.Results.Select(r => r.Status));
        Assert.IsType<ErrorOutput>(Assert.Single(run.Document.Cells[1].Outputs));
        Assert.Null(run.Document.Cells[2].ExecutionCount);
    }

    [Fact]
    public async Task Cells_above_and_from_a_cell_down_can_be_run()
    {
        var first = NotebookCell.Code("var total = 10;");
        var second = NotebookCell.Code("total += 5;");
        var third = NotebookCell.Code("total");
        var document = NotebookDocument.Create(first, second, third);

        var above = await _kernel.RunCellsAboveAsync(document, third.Id);
        var below = await _kernel.RunCellAndBelowAsync(above.Document, second.Id);

        Assert.Equal(new[] { first.Id, second.Id }, above.Results.Select(r => r.CellId));
        Assert.Equal(new[] { second.Id, third.Id }, below.Results.Select(r => r.CellId));
        Assert.Equal("20", below.Results[^1].ResultText());
        Assert.Equal(4, below.Document.FindCell(third.Id)?.ExecutionCount);
    }

    [Fact]
    public async Task Cells_in_other_languages_are_skipped_by_run_all_and_keep_their_outputs()
    {
        var fsharp = NotebookCell.Code("let x = 1", NotebookLanguages.FSharp) with
        {
            Outputs = [new StreamOutput(StreamOutput.StandardOutput, "from Polyglot\n")],
        };
        var document = NotebookDocument.Create(fsharp, NotebookCell.Code("1"));

        var run = await _kernel.RunAllAsync(document);

        Assert.True(run.Succeeded);
        Assert.Equal(CellExecutionStatus.Unsupported, run.Results[0].Status);
        Assert.Same(fsharp, run.Document.Cells[0]);
        Assert.Equal(1, run.Document.Cells[1].ExecutionCount);
    }
}
