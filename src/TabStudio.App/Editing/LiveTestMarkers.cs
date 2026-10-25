using System.Globalization;
using AvaloniaEdit.Document;
using Microsoft.CodeAnalysis.Text;
using TabStudio.LiveTesting.Results;

namespace TabStudio.App.Editing;

/// <summary>The Live Unit Testing marker of one line: how the tests that run it fare.</summary>
/// <param name="State">Whether only passing tests, a failing test or no test runs the line.</param>
/// <param name="IsStale">Whether the line or its tests changed since the results, which are then drawn hollow.</param>
/// <param name="Tests">The tests that run the line, failing first.</param>
public sealed record LiveTestMarker(LineCoverageState State, bool IsStale, IReadOnlyList<CoveringTest> Tests)
{
    /// <summary>What the marker means, in Visual Studio's words, such as "Covered by 3 tests, 1 failing".</summary>
    public string Summary
    {
        get
        {
            var failing = Tests.Count(t => t.IsFailing);
            var text = State switch
            {
                LineCoverageState.NotCovered => "Not covered by any test",
                LineCoverageState.Failing => string.Create(CultureInfo.InvariantCulture, $"Covered by {Tests.Count} {Plural(Tests.Count, "test")}, {failing} failing"),
                _ => string.Create(CultureInfo.InvariantCulture, $"Covered by {Tests.Count} passing {Plural(Tests.Count, "test")}"),
            };
            return IsStale ? text + " (out of date)" : text;
        }
    }

    private static string Plural(int count, string word) => count == 1 ? word : word + "s";
}

/// <summary>
/// The Live Unit Testing markers of an open document, one per line with code, kept on their lines while the user
/// types: a marker moves with the text around it, and a line that is edited keeps its marker, drawn stale, until
/// the tests run again, as do the other lines those tests ran.
/// </summary>
/// <remarks>Belongs to the UI thread, like the document.</remarks>
public sealed class LiveTestMarkers
{
    private TrackedRange<LiveTestMarker>[] _ranges = [];
    private SourceText? _mappedText;
    private int _mappedEdits = -1;
    private MappedLine[] _map = [];
    private int _edits;

    /// <summary>The number of lines with a marker.</summary>
    public int Count => _ranges.Length;

    /// <summary>Puts the markers of a file's coverage on the document's lines.</summary>
    /// <param name="document">The open document.</param>
    /// <param name="coverage">The coverage, for the text of the build it came from.</param>
    /// <remarks>
    /// The document may have changed since that build: lines are mapped onto its current text, and the lines that
    /// changed are marked stale.
    /// </remarks>
    public void Update(TextDocument document, FileCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(coverage);

        // Results arrive many times during a run, for the same build text: the line map is kept until either side changes.
        if (!ReferenceEquals(coverage.Text, _mappedText) || _mappedEdits != _edits)
        {
            var before = coverage.Text.Lines.Select(l => l.ToString()).ToList();
            var after = document.Lines.Select(l => document.GetText(l.Offset, l.Length)).ToList();
            _map = LineMapper.Map(before, after);
            _mappedText = coverage.Text;
            _mappedEdits = _edits;
        }

        var map = _map;
        var ranges = new List<TrackedRange<LiveTestMarker>>(coverage.Lines.Count);
        var edited = new List<TrackedRange<LiveTestMarker>>();
        foreach (var line in coverage.Lines)
        {
            if (line.Line < 1 || line.Line > map.Length || map[line.Line - 1] is not { Line: > 0 } mapped || mapped.Line > document.LineCount)
            {
                continue;
            }

            var target = document.GetLineByNumber(mapped.Line);
            var marker = new LiveTestMarker(line.State, line.IsStale || mapped.IsChanged, line.Tests);
            var range = new TrackedRange<LiveTestMarker>(target.Offset, target.Length, marker);
            ranges.Add(range);
            if (mapped.IsChanged)
            {
                edited.Add(range);
            }
        }

        _ranges = [.. StaleWithTheirTests(ranges, edited).OrderBy(r => r.Start)];
    }

    /// <summary>The marker of a line, if it has one.</summary>
    /// <param name="line">A line of the document.</param>
    public LiveTestMarker? At(DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var index = FirstStartingAtOrAfter(line.Offset);
        return index < _ranges.Length && _ranges[index].Start <= line.EndOffset ? _ranges[index].Value : null;
    }

    /// <summary>The markers with their current lines, in order, for tests and screenshots.</summary>
    /// <param name="document">The document.</param>
    public IReadOnlyList<(int Line, LiveTestMarker Marker)> Lines(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return [.. _ranges
            .Where(r => r.Start <= document.TextLength)
            .Select(r => (document.GetLineByOffset(r.Start).LineNumber, r.Value))];
    }

    /// <summary>
    /// Moves the markers through an edit: markers after it shift, the marker of an edited line stays and turns
    /// stale, and markers of lines the edit removed are dropped.
    /// </summary>
    /// <param name="change">The change just applied to the document.</param>
    public void Apply(DocumentChangeEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        _edits++;
        if (_ranges.Length == 0)
        {
            return;
        }

        var offset = change.Offset;
        var removedEnd = offset + change.RemovalLength;
        var delta = change.InsertionLength - change.RemovalLength;
        var result = new List<TrackedRange<LiveTestMarker>>(_ranges.Length);
        foreach (var range in _ranges)
        {
            if (range.End < offset)
            {
                result.Add(range);
            }
            else if (range.Start > offset && range.Start >= removedEnd)
            {
                result.Add(range with { Start = range.Start + delta });
            }
            else if (range.Start == offset && change.RemovalLength == 0)
            {
                // Text inserted at the start of the line pushes it along: whole lines move it down unchanged, while
                // anything else is typed into the line.
                var inserted = change.InsertedText;
                var wholeLines = inserted.TextLength > 0 && inserted.GetCharAt(inserted.TextLength - 1) == '\n';
                result.Add(range with { Start = range.Start + delta, Value = wholeLines ? range.Value : Stale(range.Value) });
            }
            else if (range.Start <= offset && range.End >= removedEnd)
            {
                result.Add(range with { Length = range.Length + delta, Value = Stale(range.Value) });
            }
        }

        var wasStale = _ranges.Where(r => r.Value.IsStale).Select(r => r.Value).ToHashSet(ReferenceEqualityComparer.Instance);
        _ranges = [.. StaleWithTheirTests(result, result.Where(r => r.Value.IsStale && !wasStale.Contains(r.Value)))];
    }

    private static LiveTestMarker Stale(LiveTestMarker marker) => marker.IsStale ? marker : marker with { IsStale = true };

    /// <summary>
    /// The tests that ran an edited line run again, so the results of every line they ran are out of date too, as
    /// Visual Studio shows them: those markers turn stale with it.
    /// </summary>
    /// <param name="ranges">The markers, changed in place.</param>
    /// <param name="edited">The markers of the lines just edited.</param>
    private static List<TrackedRange<LiveTestMarker>> StaleWithTheirTests(List<TrackedRange<LiveTestMarker>> ranges, IEnumerable<TrackedRange<LiveTestMarker>> edited)
    {
        var tests = edited.SelectMany(r => r.Value.Tests).Select(t => t.Key).ToHashSet();
        if (tests.Count == 0)
        {
            return ranges;
        }

        for (var i = 0; i < ranges.Count; i++)
        {
            if (!ranges[i].Value.IsStale && ranges[i].Value.Tests.Any(t => tests.Contains(t.Key)))
            {
                ranges[i] = ranges[i] with { Value = Stale(ranges[i].Value) };
            }
        }

        return ranges;
    }

    private int FirstStartingAtOrAfter(int offset)
    {
        int low = 0, high = _ranges.Length;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (_ranges[mid].Start < offset)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }
}
