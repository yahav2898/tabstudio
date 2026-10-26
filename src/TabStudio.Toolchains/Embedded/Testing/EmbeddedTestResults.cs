using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Toolchains.Embedded.Testing;

/// <summary>Portable host and board test-output formats understood by the embedded workflow.</summary>
public enum EmbeddedTestFormat
{
    /// <summary>Unity's file:line:test:PASS/FAIL/IGNORE serial or RTT lines.</summary>
    Unity,
    /// <summary>Test Anything Protocol, with plans, skip/TODO and bailout detection.</summary>
    Tap,
    /// <summary>Decoded defmt-test harness progress and completion lines.</summary>
    DefmtTest,
    /// <summary>Rust libtest text or JSON events.</summary>
    Rust,
    /// <summary>GoogleTest's JUnit-style XML output.</summary>
    GoogleTest,
}

/// <summary>The observed outcome of one test on one board or host.</summary>
public enum EmbeddedTestOutcome
{
    /// <summary>The harness reported successful completion.</summary>
    Passed,
    /// <summary>The harness reported a failing assertion.</summary>
    Failed,
    /// <summary>The harness explicitly skipped the test.</summary>
    Skipped,
    /// <summary>The transport or harness ended without a valid result.</summary>
    Errored,
}

/// <summary>A test result that keeps board identity separate from its test name.</summary>
/// <param name="BoardId">The explicit device identity, or a stable host identity.</param>
/// <param name="Format">The harness.</param>
/// <param name="Name">The harness test name.</param>
/// <param name="Outcome">The observed outcome.</param>
/// <param name="Message">The failure or skip reason.</param>
/// <param name="File">The source file, if provided.</param>
/// <param name="Line">The one-based source line, if provided.</param>
/// <param name="Duration">The harness-reported duration; zero means unreported.</param>
public sealed record EmbeddedTestResult(string BoardId, EmbeddedTestFormat Format, string Name,
    EmbeddedTestOutcome Outcome, string? Message = null, string? File = null, int? Line = null, TimeSpan Duration = default)
{
    /// <summary>A deterministic Test Explorer identity that cannot collide when two boards run the same test.</summary>
    public string Id => JsonSerializer.Serialize(new[] { BoardId, Format.ToString(), File ?? "", Name });
}

/// <summary>Parses a single board's decoded test stream. Binary RTT/defmt decoding remains the debugger's job.</summary>
public sealed class EmbeddedTestStream
{
    private const int MaxLineLength = 64 * 1024;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex Ansi = new("\\x1b\\[[0-?]*[ -/]*[@-~]", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex UnityLine = new(@"^(?<file>.+):(?<line>\d+):(?<name>[^:\r\n]+):(?<result>PASS|FAIL|IGNORE)(?::(?<message>.*))?$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex TapLine = new(@"^(?<failed>not )?ok(?:\s+(?<number>\d+))?(?:\s*-?\s*(?<name>[^#]*?))?(?:\s*#\s*(?<directive>SKIP|TODO)\b(?<reason>.*))?\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeout);
    private static readonly Regex TapPlan = new(@"^1\.\.(?<count>\d+)(?:\s*#\s*SKIP\b(?<reason>.*))?\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeout);
    private static readonly Regex RustLine = new(@"^test (?<name>.+?) \.\.\. (?<result>ok|FAILED|ignored)(?:, (?<message>.*))?$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex DefmtLine = new(@"(?:^|\s)\((?<index>\d+)/(?<count>\d+)\) (?<action>running|ignoring) `(?<name>[^`]+)`\.\.\.$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex UnitySummary = new(@"^(?<count>\d+) Tests \d+ Failures \d+ Ignored$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex RustCount = new(@"^running (?<count>\d+) tests?$", RegexOptions.CultureInvariant, RegexTimeout);
    private readonly string _boardId;
    private readonly EmbeddedTestFormat _format;
    private readonly HashSet<int> _tapNumbers = [];
    private readonly List<EmbeddedTestResult> _results = [];
    private readonly HashSet<string> _rustStarted = new(StringComparer.Ordinal);
    private string? _defmtCurrent;
    private int _defmtIndex;
    private int? _expected;
    private bool _completed;
    private bool _harnessCompleted;
    private bool _badSequence;
    private int _rustBatchCount;

    /// <summary>Creates a parser for exactly one device or host stream.</summary>
    /// <param name="boardId">The device identity, stable for the test run.</param>
    /// <param name="format">The emitted format. GoogleTest uses <see cref="EmbeddedTestXml.ReadGoogleTest"/> instead.</param>
    public EmbeddedTestStream(string boardId, EmbeddedTestFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boardId);
        if (format == EmbeddedTestFormat.GoogleTest)
        {
            throw new ArgumentException("GoogleTest XML is parsed as a complete report.", nameof(format));
        }
        _boardId = boardId;
        _format = format;
    }

    /// <summary>All observed results so far. An incomplete run must be finalized with <see cref="Complete"/>.</summary>
    public IReadOnlyList<EmbeddedTestResult> Results => _results;

    /// <summary>Consumes one complete decoded line and returns only results added by that line.</summary>
    /// <param name="line">The decoded line without its newline.</param>
    public IReadOnlyList<EmbeddedTestResult> Feed(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (_completed)
        {
            throw new InvalidOperationException("A completed test stream cannot accept more data.");
        }
        if (line.Length > MaxLineLength)
        {
            throw new InvalidDataException("Test output line exceeds 64 KB.");
        }
        if (_results.Count >= 100_000)
        {
            throw new InvalidDataException("Test output exceeds 100,000 results.");
        }
        var before = _results.Count;
        line = Ansi.Replace(line, "").TrimEnd();
        switch (_format)
        {
            case EmbeddedTestFormat.Unity:
                ReadUnity(line);
                break;
            case EmbeddedTestFormat.Tap:
                ReadTap(line);
                break;
            case EmbeddedTestFormat.DefmtTest:
                ReadDefmt(line);
                break;
            case EmbeddedTestFormat.Rust:
                ReadRust(line);
                break;
            default:
                throw new InvalidOperationException("Unsupported streaming test format.");
        }
        return _results.Skip(before).ToArray();
    }

    /// <summary>Finalizes a process or disconnected board; an unfinished defmt test or mismatched TAP plan is an error.</summary>
    /// <param name="transportSucceeded">Whether the host command/transport completed successfully.</param>
    public IReadOnlyList<EmbeddedTestResult> Complete(bool transportSucceeded = true)
    {
        if (_completed)
        {
            return _results;
        }
        _completed = true;
        if (_defmtCurrent is not null)
        {
            Add(_defmtCurrent, EmbeddedTestOutcome.Errored, "The harness ended before this test completed.");
            _defmtCurrent = null;
        }
        foreach (var name in _rustStarted)
        {
            Add(name, EmbeddedTestOutcome.Errored, "The Rust test started but no completion event was received.");
        }
        if (_format == EmbeddedTestFormat.Unity && (!_harnessCompleted || _expected != _results.Count))
        {
            Add("[harness]", EmbeddedTestOutcome.Errored, "Unity's final summary was missing or did not match the received results.");
        }
        if (_format == EmbeddedTestFormat.Rust && _expected is { } rustExpected && _rustBatchCount != rustExpected)
        {
            Add("[harness]", EmbeddedTestOutcome.Errored, "Rust's test count did not match the received results.");
        }
        if (_format == EmbeddedTestFormat.Tap && _expected is { } expected && _tapNumbers.Count != expected)
        {
            Add("[harness]", EmbeddedTestOutcome.Errored,
                $"TAP planned {expected} tests but reported {_tapNumbers.Count} distinct results.");
        }
        else if (_format == EmbeddedTestFormat.Tap && _expected is null)
        {
            Add("[harness]", EmbeddedTestOutcome.Errored, "TAP's top-level plan was missing.");
        }
        else if (_format == EmbeddedTestFormat.DefmtTest && (!_harnessCompleted || _badSequence))
        {
            Add("[harness]", EmbeddedTestOutcome.Errored, "defmt-test completion was missing or progress lines were lost.");
        }
        if (!transportSucceeded && !_results.Any(r => r.Outcome is EmbeddedTestOutcome.Failed or EmbeddedTestOutcome.Errored))
        {
            Add("[transport]", EmbeddedTestOutcome.Errored, "The test command or board transport failed.");
        }
        if (_results.Count == 0)
        {
            Add("[harness]", EmbeddedTestOutcome.Errored, "No individual test results were received.");
        }
        return _results;
    }

    private void ReadUnity(string line)
    {
        var summary = UnitySummary.Match(line);
        if (summary.Success)
        {
            _harnessCompleted = true;
            _expected = int.TryParse(summary.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                ? count : throw new InvalidDataException("Invalid Unity test count.");
            return;
        }
        var match = UnityLine.Match(line);
        if (!match.Success)
        {
            return;
        }
        var outcome = match.Groups["result"].Value switch
        {
            "PASS" => EmbeddedTestOutcome.Passed,
            "FAIL" => EmbeddedTestOutcome.Failed,
            _ => EmbeddedTestOutcome.Skipped,
        };
        var number = int.TryParse(match.Groups["line"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? (int?)parsed : null;
        _results.Add(new(_boardId, _format, match.Groups["name"].Value, outcome,
            Optional(match.Groups["message"].Value), match.Groups["file"].Value, number));
    }

    private void ReadTap(string line)
    {
        if (line.StartsWith("Bail out!", StringComparison.OrdinalIgnoreCase))
        {
            Add("[harness]", EmbeddedTestOutcome.Errored, line);
            return;
        }
        var plan = TapPlan.Match(line);
        if (plan.Success)
        {
            if (!int.TryParse(plan.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count > 100_000)
            {
                throw new InvalidDataException("TAP plan exceeds the supported test count.");
            }
            if (_expected is not null)
            {
                Add("[harness]", EmbeddedTestOutcome.Errored, "TAP contains more than one top-level plan.");
            }
            _expected = count;
            if (count == 0)
            {
                Add("[suite]", EmbeddedTestOutcome.Skipped, Optional(plan.Groups["reason"].Value) ?? "The TAP suite contains no tests.");
            }
            return;
        }
        var match = TapLine.Match(line);
        if (!match.Success)
        {
            return;
        }
        var number = match.Groups["number"].Success
            && int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var explicitNumber)
            ? explicitNumber : _tapNumbers.Count + 1;
        if (number < 1 || !_tapNumbers.Add(number) || _expected is { } expected && number > expected)
        {
            Add("[harness]", EmbeddedTestOutcome.Errored, "TAP contains a duplicate or out-of-plan test number.");
            return;
        }
        var skipped = match.Groups["directive"].Success;
        Add(Optional(match.Groups["name"].Value) ?? "test " + number.ToString(CultureInfo.InvariantCulture),
            skipped ? EmbeddedTestOutcome.Skipped : match.Groups["failed"].Success ? EmbeddedTestOutcome.Failed : EmbeddedTestOutcome.Passed,
            skipped ? match.Groups["directive"].Value + ": " + match.Groups["reason"].Value.Trim() : null);
    }

    private void ReadRust(string line)
    {
        var countMatch = RustCount.Match(line);
        if (countMatch.Success)
        {
            if (_expected is { } previous && _rustBatchCount != previous)
            {
                Add("[harness]", EmbeddedTestOutcome.Errored, "A previous Rust test binary did not report every result.");
            }
            _expected = int.TryParse(countMatch.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                ? count : throw new InvalidDataException("Invalid Rust test count.");
            _rustBatchCount = 0;
            return;
        }
        if (line.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 16 });
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || String(root, "type") != "test" || String(root, "name") is not { } name)
                {
                    return;
                }
                var outcome = String(root, "event") switch
                {
                    "ok" => (EmbeddedTestOutcome?)EmbeddedTestOutcome.Passed,
                    "failed" => EmbeddedTestOutcome.Failed,
                    "ignored" => EmbeddedTestOutcome.Skipped,
                    _ => null,
                };
                if (String(root, "event") == "started")
                {
                    _rustStarted.Add(name);
                }
                if (outcome is not null)
                {
                    _rustStarted.Remove(name);
                    _rustBatchCount++;
                    var seconds = root.TryGetProperty("exec_time", out var duration) && duration.ValueKind == JsonValueKind.Number
                        && duration.TryGetDouble(out var value) ? value : 0;
                    _results.Add(new(_boardId, _format, name, outcome.Value, String(root, "stdout"), Duration: SafeDuration(seconds)));
                }
            }
            catch (JsonException)
            {
                // Cargo's ordinary JSON compiler output and a firmware log line are not test results.
            }
            return;
        }
        var match = RustLine.Match(line);
        if (match.Success)
        {
            _rustBatchCount++;
            Add(match.Groups["name"].Value, match.Groups["result"].Value switch
            {
                "ok" => EmbeddedTestOutcome.Passed,
                "FAILED" => EmbeddedTestOutcome.Failed,
                _ => EmbeddedTestOutcome.Skipped,
            }, Optional(match.Groups["message"].Value));
        }
    }

    private void ReadDefmt(string line)
    {
        var match = DefmtLine.Match(line);
        if (match.Success)
        {
            if (!int.TryParse(match.Groups["index"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                || !int.TryParse(match.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                || count > 100_000 || index < 1 || index > count)
            {
                throw new InvalidDataException("Invalid defmt-test harness count.");
            }
            if (index != _defmtIndex + 1 || _expected is not null && _expected != count)
            {
                _badSequence = true;
            }
            if (_defmtCurrent is not null)
            {
                // defmt-test starts the next test only after check_outcome and after_each returned successfully.
                Add(_defmtCurrent, _badSequence ? EmbeddedTestOutcome.Errored : EmbeddedTestOutcome.Passed,
                    _badSequence ? "Harness progress was lost; this result cannot be confirmed." : null);
            }
            _defmtCurrent = null;
            _expected = count;
            _defmtIndex = index;
            if (match.Groups["action"].Value == "ignoring")
            {
                Add(match.Groups["name"].Value, EmbeddedTestOutcome.Skipped, "Ignored by defmt-test.");
            }
            else
            {
                _defmtCurrent = match.Groups["name"].Value;
            }
        }
        else if (line.EndsWith("all tests passed!", StringComparison.Ordinal))
        {
            _harnessCompleted = true;
            if (_expected is not null && _defmtIndex != _expected)
            {
                _badSequence = true;
            }
            if (_defmtCurrent is not null)
            {
                Add(_defmtCurrent, _badSequence ? EmbeddedTestOutcome.Errored : EmbeddedTestOutcome.Passed);
                _defmtCurrent = null;
            }
        }
    }

    private void Add(string name, EmbeddedTestOutcome outcome, string? message = null) =>
        _results.Add(new(_boardId, _format, name, outcome, message));

    private static string? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    internal static TimeSpan SafeDuration(double seconds) => double.IsFinite(seconds) && seconds is >= 0 and <= 86400
        ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;
}

/// <summary>Reads complete host-test reports with no external XML entities or network access.</summary>
public static class EmbeddedTestXml
{
    /// <summary>Reads GoogleTest XML results, preserving parameterized names, failure messages and per-board identity.</summary>
    /// <param name="xml">The XML report, limited to 16 MB.</param>
    /// <param name="boardId">A stable host or board identity.</param>
    public static IReadOnlyList<EmbeddedTestResult> ReadGoogleTest(string xml, string boardId)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentException.ThrowIfNullOrWhiteSpace(boardId);
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 16 * 1024 * 1024,
            MaxCharactersFromEntities = 0,
        });
        var document = XDocument.Load(reader);
        if (document.Root?.Name.LocalName is not ("testsuites" or "testsuite"))
        {
            throw new InvalidDataException("Expected a GoogleTest testsuites or testsuite document.");
        }
        var results = new List<EmbeddedTestResult>();
        foreach (var test in document.Descendants().Where(e => e.Name.LocalName == "testcase"))
        {
            if (results.Count >= 100_000)
            {
                throw new InvalidDataException("Test report exceeds 100,000 results.");
            }
            var name = (string?)test.Attribute("name") ?? throw new InvalidDataException("GoogleTest testcase has no name.");
            var suite = (string?)test.Attribute("classname") ?? (string?)test.Parent?.Attribute("name");
            var failures = test.Elements().Where(e => e.Name.LocalName is "failure" or "error").ToArray();
            var skipped = test.Elements().Any(e => e.Name.LocalName == "skipped")
                || (string?)test.Attribute("status") == "notrun" || (string?)test.Attribute("result") == "suppressed";
            var outcome = failures.Any(e => e.Name.LocalName == "error") ? EmbeddedTestOutcome.Errored
                : failures.Length > 0 ? EmbeddedTestOutcome.Failed : skipped ? EmbeddedTestOutcome.Skipped : EmbeddedTestOutcome.Passed;
            var message = failures.Length > 0 ? string.Join("\n", failures.Select(e => (string?)e.Attribute("message") ?? e.Value))
                : skipped ? test.Elements().FirstOrDefault(e => e.Name.LocalName == "skipped") is { } skip
                    ? (string?)skip.Attribute("message") ?? skip.Value : null : null;
            var line = int.TryParse((string?)test.Attribute("line"), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedLine) && parsedLine > 0
                ? (int?)parsedLine : null;
            var duration = double.TryParse((string?)test.Attribute("time"), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                ? EmbeddedTestStream.SafeDuration(seconds) : TimeSpan.Zero;
            results.Add(new(boardId, EmbeddedTestFormat.GoogleTest, string.IsNullOrEmpty(suite) ? name : suite + "." + name,
                outcome, message, (string?)test.Attribute("file"), line, duration));
        }
        return results;
    }
}
