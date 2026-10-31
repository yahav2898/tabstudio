using System.Runtime.CompilerServices;
using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>Hours of work open and close the IDE many times: a closed one must not stay in memory.</summary>
public sealed class WorkspaceLifetimeTests
{
    [AvaloniaFact]
    public async Task Closed_workspaces_are_collected_after_opening_and_editing_many_times()
    {
        using var fixture = new WorkspaceFixture();
        var solution = await fixture.OpenSampleAsync();
        var closed = new List<WeakReference>();
        for (var i = 0; i < 4; i++)
        {
            closed.Add(await OpenEditAndCloseAsync(fixture, solution));
        }

        // Pending posts and timers of a closed workspace (a session save two seconds after an edit) may hold it for a
        // moment; after that, nothing should.
        await Task.Delay(TimeSpan.FromSeconds(3));
        for (var attempt = 0; attempt < 10 && closed.Any(w => w.IsAlive); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(200);
        }

        Assert.True(closed.Count(w => w.IsAlive) <= 1, $"{closed.Count(w => w.IsAlive)} of {closed.Count} closed workspaces are still in memory.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> OpenEditAndCloseAsync(WorkspaceFixture fixture, string solution)
    {
        var workspace = new WorkspaceViewModel(fixture.Compiler, fixture.DeviceStatus, fixture.Profile, new AvaloniaUiDispatcher(), fixture.Sessions, fixture.Paths);
        await workspace.LoadAsync(solution);
        var document = workspace.ActiveDocument!;
        document.Document.Insert(0, "// edit\n");
        await document.AnalyzeNowAsync();
        workspace.SaveSessionNow();
        workspace.Dispose();
        return new WeakReference(workspace);
    }
}
