using TabStudio.App.Services;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

public sealed class CrashLogTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void A_report_holds_the_error_and_where_it_was_caught()
    {
        var log = new CrashLog(new AppPaths(_temp.Path));

        var report = log.Write(new InvalidOperationException("the log stream ended"), "UI thread");

        Assert.NotNull(report);
        var text = File.ReadAllText(report);
        Assert.Contains("the log stream ended", text, StringComparison.Ordinal);
        Assert.Contains("UI thread", text, StringComparison.Ordinal);
        Assert.Equal(report, Assert.Single(log.GetReports()));
    }

    [Fact]
    public void Only_the_newest_reports_are_kept()
    {
        var log = new CrashLog(new AppPaths(_temp.Path));
        for (var i = 0; i < CrashLog.Capacity + 5; i++)
        {
            Directory.CreateDirectory(log.Folder);
            File.WriteAllText(Path.Combine(log.Folder, $"crash-20000101-0000{i:00}-000.txt"), "old");
        }

        log.Write(new InvalidOperationException("new"), "UI thread");

        Assert.Equal(CrashLog.Capacity, log.GetReports().Count);
        Assert.Contains("new", File.ReadAllText(log.GetReports()[0]), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(IOException), true)]
    [InlineData(typeof(InvalidOperationException), true)]
    [InlineData(typeof(NullReferenceException), true)]
    [InlineData(typeof(OutOfMemoryException), false)]
    [InlineData(typeof(InsufficientExecutionStackException), false)]
    public void The_app_goes_on_after_errors_but_not_after_the_runtimes_own(Type type, bool recoverable) =>
        Assert.Equal(recoverable, CrashLog.IsRecoverable((Exception)Activator.CreateInstance(type)!));

    public void Dispose() => _temp.Dispose();
}
