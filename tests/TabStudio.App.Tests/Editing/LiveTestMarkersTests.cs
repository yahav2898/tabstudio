using Avalonia.Headless.XUnit;
using AvaloniaEdit.Document;
using Microsoft.CodeAnalysis.Text;
using TabStudio.App.Editing;
using TabStudio.LiveTesting.Results;

namespace TabStudio.App.Tests.Editing;

public sealed class LiveTestMarkersTests
{
    private const string Code = "class C\n{\n    int A() { return 1; }\n    int B() { return 2; }\n}\n";

    [Fact]
    public void Lines_keep_their_numbers_before_an_edit_and_move_after_it()
    {
        string[] before = ["a", "b", "c", "d", "e"];
        string[] after = ["a", "b", "x", "y", "c2", "d", "e"];

        var map = LineMapper.Map(before, after);

        Assert.Equal(new MappedLine(1, false), map[0]);
        Assert.Equal(new MappedLine(2, false), map[1]);
        Assert.Equal(new MappedLine(0, true), map[2]);
        Assert.Equal(new MappedLine(6, false), map[3]);
        Assert.Equal(new MappedLine(7, false), map[4]);
    }

    [Fact]
    public void An_edited_line_keeps_its_place_when_no_line_was_added()
    {
        var map = LineMapper.Map(["a", "b", "c"], ["a", "B", "c"]);

        Assert.Equal(new[] { new MappedLine(1, false), new MappedLine(2, true), new MappedLine(3, false) }, map);
    }

    [AvaloniaFact]
    public void Markers_go_on_the_lines_of_the_current_text()
    {
        var document = new TextDocument("// header\n" + Code);
        var markers = new LiveTestMarkers();

        markers.Update(document, Coverage((3, LineCoverageState.Covered), (4, LineCoverageState.Failing)));

        Assert.Equal(new[] { (4, LineCoverageState.Covered), (5, LineCoverageState.Failing) }, markers.Lines(document).Select(l => (l.Line, l.Marker.State)));
        Assert.All(markers.Lines(document), l => Assert.False(l.Marker.IsStale));
        Assert.Null(markers.At(document.GetLineByNumber(3)));
        Assert.Equal(LineCoverageState.Covered, markers.At(document.GetLineByNumber(4))?.State);
    }

    [AvaloniaFact]
    public void Typing_on_a_line_makes_its_marker_stale_and_new_lines_push_markers_down()
    {
        var document = new TextDocument(Code);
        var markers = new LiveTestMarkers();
        markers.Update(document, Coverage((3, LineCoverageState.Covered), (4, LineCoverageState.Failing)));
        document.Changed += (_, e) => markers.Apply(e);

        document.Insert(document.Text.IndexOf("1;", StringComparison.Ordinal), "4");
        document.Insert(document.GetLineByNumber(4).Offset, "\n");

        Assert.Equal(new[] { (3, true), (5, false) }, markers.Lines(document).Select(l => (l.Line, l.Marker.IsStale)));
    }

    [AvaloniaFact]
    public void Deleting_a_line_removes_its_marker()
    {
        var document = new TextDocument(Code);
        var markers = new LiveTestMarkers();
        markers.Update(document, Coverage((3, LineCoverageState.Covered), (4, LineCoverageState.Failing)));
        document.Changed += (_, e) => markers.Apply(e);

        var line = document.GetLineByNumber(3);
        document.Remove(line.Offset - 1, line.TotalLength);

        Assert.Equal(new[] { 3 }, markers.Lines(document).Select(l => l.Line));
        Assert.Equal(LineCoverageState.Failing, markers.At(document.GetLineByNumber(3))?.State);
    }

    [Fact]
    public void The_summary_says_what_a_marker_means()
    {
        Assert.Equal("Not covered by any test", new LiveTestMarker(LineCoverageState.NotCovered, false, []).Summary);
        Assert.Equal("Not covered by any test (out of date)", new LiveTestMarker(LineCoverageState.NotCovered, true, []).Summary);
    }

    private static FileCoverage Coverage(params (int Line, LineCoverageState State)[] lines) =>
        new("/projects/C.cs", SourceText.From(Code), [.. lines.Select(l => new LineCoverage(l.Line, l.State, false, []))]);
}
