using System.Diagnostics;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    private static async Task<SelfTestResult> RecordSampleAsync(WorkspaceViewModel workspace, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var program = workspace.Documents.FirstOrDefault(d => d.FileName == "Program.cs");
        if (program is null) return new(false, "Recording: Program.cs is not open.", clock.Elapsed);
        var original = program.Document.Text;
        try
        {
            program.Document.Text = "int total = 0; for (int i = 0; i < 4; i++) { total += i; } Console.WriteLine(total);";
            await workspace.StartWithRecordingCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
            await WaitAsync(() => !workspace.IsDebugging || workspace.IsPaused, cancellationToken);
            if (!workspace.HasRecording || workspace.IsPaused) return new(false, "Recording: the loop did not finish.", clock.Elapsed);
            await workspace.StepBackCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
            await workspace.JumpToLastWriteCommand.ExecuteAsync("total").WaitAsync(cancellationToken);
            await workspace.JumpToLastWriteCommand.ExecuteAsync("total").WaitAsync(cancellationToken);
            var value = workspace.Locals.Tree.Rows.FirstOrDefault(r => r.Name == "total")?.Value;
            var success = workspace.IsHistorical && workspace.Locals.Tree.IsHistorical && value == "3";
            await workspace.ReturnToLiveCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
            return new(success, $"Recording: historical loop total = {value ?? "missing"} (expected 3).", clock.Elapsed);
        }
        finally
        {
            if (workspace.IsDebugging) workspace.StopDebuggingCommand.Execute(null);
            program.Document.Text = original;
        }
    }
}
