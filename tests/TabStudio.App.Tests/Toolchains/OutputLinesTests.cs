using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// The lines of a run that the Error List reads keep its start as well as its end: with only the last 400 kept, a C
/// build whose first error was followed by 400 lines of warnings and notes listed no error at all.
/// </summary>
public sealed class OutputLinesTests
{
    [Fact]
    public void A_long_runs_first_errors_and_last_lines_are_both_kept()
    {
        var lines = new OutputProcess.LineCollector();
        lines.Add("main.c:3:5: error: unknown type name 'strin'\n", OutputKind.Error);
        for (var i = 0; i < 5000; i++)
        {
            lines.Add($"util.c:{i}:1: warning: unused variable 'x{i}'\n", OutputKind.Error);
        }

        lines.Add("make: *** [Makefile:4: app] Error 1", OutputKind.Error);
        lines.Complete(OutputKind.Error);

        var kept = lines.Snapshot();
        Assert.Equal(2 * OutputProcess.KeptLines, kept.Count);
        Assert.Equal("main.c:3:5: error: unknown type name 'strin'", kept[0]);
        Assert.Equal("make: *** [Makefile:4: app] Error 1", kept[^1]);
    }

    [Fact]
    public void A_short_run_keeps_every_line_in_order()
    {
        var lines = new OutputProcess.LineCollector();
        lines.Add("one\ntwo\n", OutputKind.Program);
        lines.Add("three", OutputKind.Program);

        Assert.Equal(["one", "two", "three"], lines.Snapshot());
    }
}
