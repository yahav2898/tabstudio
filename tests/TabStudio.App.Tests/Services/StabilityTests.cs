using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

/// <summary>
/// The 0.21.5 stability pass: a full storage, Android's low-memory killer, Android 15's foreground-service limit and
/// an app that ended without a word. Each test failed before its fix.
/// </summary>
public sealed class StabilityTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AppPaths _paths;

    public StabilityTests()
    {
        _paths = new AppPaths(_temp.Resolve("data"));
    }

    public void Dispose() => _temp.Dispose();

    private static IOException DiskFull() => new("No space left on device", OperatingSystem.IsWindows() ? unchecked((int)0x80070070) : 28);

    [Fact]
    public void A_full_storage_is_recognized_and_other_errors_are_not()
    {
        Assert.True(StorageErrors.IsFull(DiskFull()));
        Assert.True(StorageErrors.IsFull(new InvalidOperationException("wrapped", DiskFull())));
        Assert.False(StorageErrors.IsFull(new IOException("The file is in use")));
        Assert.False(StorageErrors.IsFull(new UnauthorizedAccessException()));
        Assert.False(StorageErrors.IsFull(null));
        Assert.NotNull(StorageErrors.AvailableBytes(_temp.Resolve("not/yet/there.txt")));
    }

    [Fact]
    public async Task A_full_storage_still_saves_the_unsaved_text_into_the_space_kept_aside()
    {
        // The session is the only copy of unsaved text, and a full storage is when the files themselves cannot be saved:
        // its save failed silently, and the newest text was lost when Android ended the app.
        var full = false;
        var reserve = Path.Combine(_paths.State, "session.reserve");
        var store = new SessionStore(_paths)
        {
            ReserveBytes = 64 * 1024,
            CreateFile = path => full && File.Exists(reserve) ? throw DiskFull() : File.Create(path),
        };
        var solution = _temp.Write("app/App.slnx", "<Solution />");
        var program = _temp.Resolve("app/Program.cs");

        Assert.Equal(SessionSaveResult.Saved, store.Save(new SessionState(solution, [new SessionDocument(program, 0, "first")], null, null)));
        for (var waited = 0; !File.Exists(reserve) && waited < 200; waited++)
        {
            await Task.Delay(50);
        }

        Assert.Equal(64 * 1024, new FileInfo(reserve).Length);

        full = true;
        Assert.Equal(SessionSaveResult.SavedFromReserve, store.Save(new SessionState(solution, [new SessionDocument(program, 0, "second")], null, null)));
        Assert.False(File.Exists(reserve));
        Assert.Equal("second", new SessionStore(_paths).Load()!.Documents.Single().UnsavedText);

        // With the reserve used up, a failure says so and leaves the last session in place, with no half-written copy.
        var failing = new SessionStore(_paths) { CreateFile = _ => throw DiskFull() };
        Assert.Equal(SessionSaveResult.Failed, failing.Save(new SessionState(solution, [new SessionDocument(program, 0, "third")], null, null)));
        Assert.Equal("second", new SessionStore(_paths).Load()!.Documents.Single().UnsavedText);
        Assert.False(File.Exists(_paths.SessionFile + ".tmp"));
    }

    [Fact]
    public void An_app_that_ended_by_itself_is_noticed_once_at_the_next_start()
    {
        var log = new CrashLog(_paths);
        Assert.Null(log.TakeLastExit());

        // Ended in front with nothing written down: a native crash, or Android ending an app that stopped responding.
        log.MarkRunning();
        var silent = new CrashLog(_paths).TakeLastExit();
        Assert.NotNull(silent);
        Assert.Null(silent.Reason);
        Assert.Contains("closed unexpectedly", silent.Message, StringComparison.Ordinal);
        Assert.Null(new CrashLog(_paths).TakeLastExit());

        // Sent to the background first, where Android ends apps as it pleases: a normal end.
        log.MarkRunning();
        log.MarkStopped();
        Assert.Null(new CrashLog(_paths).TakeLastExit());

        // Running another version when it ended: it was updated, not crashed.
        File.WriteAllText(Path.Combine(_paths.State, "running"), "0.19.0.0");
        Assert.Null(new CrashLog(_paths).TakeLastExit());

        // An error that ended it is named, with its report.
        var report = log.WriteFatal(new InsufficientExecutionStackException("Insufficient stack"), "UI thread (the app ended)");
        var fatal = new CrashLog(_paths).TakeLastExit();
        Assert.NotNull(fatal);
        Assert.Equal("InsufficientExecutionStackException: Insufficient stack", fatal.Reason);
        Assert.Equal(report, fatal.Report);
        Assert.Contains(report!, fatal.Message, StringComparison.Ordinal);
        Assert.Single(log.GetReports());
        Assert.Null(new CrashLog(_paths).TakeLastExit());
    }

    [AvaloniaFact]
    public async Task The_start_after_a_crash_says_so_instead_of_starting_silently()
    {
        using var services = AppServices.Create(new AppPaths(_temp.Resolve("app")));
        var main = services.GetRequiredService<MainViewModel>();

        await main.StartAsync(new UnexpectedExit("OutOfMemoryException: Insufficient memory", "/data/crash.txt"));

        Assert.Contains("closed unexpectedly", main.Start.ErrorText, StringComparison.Ordinal);
        Assert.Contains("OutOfMemoryException", main.Start.ErrorText, StringComparison.Ordinal);
        Assert.Contains("/data/crash.txt", main.Start.ErrorText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Critical_memory_pressure_saves_the_unsaved_text_before_Android_ends_the_app()
    {
        // Android's low-memory killer can end the app right after critical pressure, with no Deactivated to save the
        // session: the text typed since the last save was lost.
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var document = fixture.Workspace.ActiveDocument!;
        document.Document.Insert(0, "// typed just now\n");

        fixture.DeviceStatus.RaiseMemoryPressure(MemoryPressure.Critical);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var saved = new SessionStore(fixture.Paths).Load();
        Assert.NotNull(saved);
        Assert.StartsWith("// typed just now", saved.Documents.Single(d => d.Path == document.FilePath).UnsavedText, StringComparison.Ordinal);
    }

    [Fact]
    public void The_operations_service_stops_when_Android_15_says_its_time_is_up()
    {
        // Android 15 gives a data-sync service six hours a day across starts; one still in the foreground a few
        // seconds after onTimeout ends the app (ForegroundServiceDidNotStopInTimeException).
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TabStudio.slnx")))
        {
            root = root.Parent;
        }

        var source = File.ReadAllText(Path.Combine(root!.FullName, "src", "TabStudio.Android", "OperationsService.cs"));
        var timeout = source.IndexOf("public override void OnTimeout(int startId, ForegroundService fgsType)", StringComparison.Ordinal);
        Assert.True(timeout > 0, "OperationsService must handle Android 15's onTimeout.");
        Assert.Contains("Stop();", source[timeout..source.IndexOf('}', timeout)], StringComparison.Ordinal);
    }
}
