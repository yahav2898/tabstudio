using System.Xml;
using TabStudio.Toolchains.Embedded.Testing;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class TestingResultsTests
{
    [Fact]
    public void Unity_results_preserve_windows_paths_lines_and_board_identity()
    {
        var parser = new EmbeddedTestOutputParser("board-A", EmbeddedTestFormat.Unity);
        parser.Feed("\u001b[31mC:\\firmware\\test_sensor.c:42:test_sensor:FAIL:Expected 2 Was 3\u001b[0m");
        parser.Feed("test/test_sensor.c:55:test_pending:IGNORE:Requires fixture");
        parser.Feed("test/test_sensor.c:65:test_ok:PASS");
        parser.Feed("3 Tests 1 Failures 1 Ignored");
        var results = parser.Complete();
        Assert.Equal(3, results.Count);
        Assert.Equal("C:\\firmware\\test_sensor.c", results[0].File);
        Assert.Equal(42, results[0].Line);
        Assert.Equal(EmbeddedTestOutcome.Failed, results[0].Outcome);
        Assert.Equal("Expected 2 Was 3", results[0].Message);
        Assert.Equal(EmbeddedTestOutcome.Skipped, results[1].Outcome);
        Assert.Equal("board-A", results[2].BoardId);
        Assert.NotEqual(results[0].Id, (results[0] with { BoardId = "board-B" }).Id);
    }

    [Fact]
    public void A_disconnected_Unity_suite_cannot_turn_one_PASS_line_into_a_green_run()
    {
        var parser = new EmbeddedTestOutputParser("board", EmbeddedTestFormat.Unity);
        parser.Feed("test.c:4:test_one:PASS");
        Assert.Contains(parser.Complete(), r => r.Outcome == EmbeddedTestOutcome.Errored);
    }

    [Fact]
    public void Multiple_Unity_binaries_can_report_separate_summaries_in_one_board_session()
    {
        var parser = new EmbeddedTestOutputParser("board", EmbeddedTestFormat.Unity);
        parser.Feed("first.c:4:test_one:PASS");
        parser.Feed("1 Tests 0 Failures 0 Ignored");
        parser.Feed("second.c:7:test_two:PASS");
        parser.Feed("1 Tests 0 Failures 0 Ignored");
        Assert.All(parser.Complete(), r => Assert.Equal(EmbeddedTestOutcome.Passed, r.Outcome));
    }

    [Fact]
    public void A_Unity_summary_reporting_missing_failures_cannot_make_the_run_green()
    {
        var parser = new EmbeddedTestOutputParser("board", EmbeddedTestFormat.Unity);
        parser.Feed("test.c:10:test_one:PASS");
        parser.Feed("1 Tests 1 Failures 0 Ignored");
        Assert.Contains(parser.Complete(), result => result.Outcome == EmbeddedTestOutcome.Errored);
    }

    [Fact]
    public void Tap_plans_failures_and_skip_todo_directives_are_parsed()
    {
        var parser = new EmbeddedTestOutputParser("host", EmbeddedTestFormat.Tap);
        parser.Feed("TAP version 13");
        parser.Feed("1..4");
        parser.Feed("ok 1 - normal");
        parser.Feed("not ok 2 - bad sensor");
        parser.Feed("ok 3 - unavailable # SKIP no fixture");
        parser.Feed("not ok 4 - pending # TODO implement");
        var results = parser.Complete();
        Assert.Equal([EmbeddedTestOutcome.Passed, EmbeddedTestOutcome.Failed, EmbeddedTestOutcome.Skipped, EmbeddedTestOutcome.Skipped], results.Select(r => r.Outcome));
        Assert.Equal("normal", results[0].Name);
    }

    [Theory]
    [InlineData("1..2\nok 1 - only one")]
    [InlineData("1..1\nok 1 - one\nok 1 - duplicate")]
    [InlineData("ok 1 - no plan")]
    [InlineData("1..2\nBail out! lost target")]
    public void Invalid_or_truncated_TAP_runs_include_a_harness_error(string text)
    {
        var parser = new EmbeddedTestOutputParser("board", EmbeddedTestFormat.Tap);
        foreach (var line in text.Split('\n'))
        {
            parser.Feed(line);
        }
        Assert.Contains(parser.Complete(), r => r.Outcome == EmbeddedTestOutcome.Errored);
    }

    [Fact]
    public void An_explicitly_skipped_empty_TAP_suite_is_skipped()
    {
        var parser = new EmbeddedTestOutputParser("host", EmbeddedTestFormat.Tap);
        parser.Feed("1..0 # SKIP unsupported board");
        Assert.Equal(EmbeddedTestOutcome.Skipped, Assert.Single(parser.Complete()).Outcome);
    }

    [Fact]
    public void Defmt_harness_progress_is_confirmed_by_the_next_test_or_final_banner()
    {
        var parser = new EmbeddedTestOutputParser("nrf52840-1", EmbeddedTestFormat.DefmtTest);
        Assert.Empty(parser.Feed("(1/3) running `first`..."));
        Assert.Equal(EmbeddedTestOutcome.Passed, Assert.Single(parser.Feed("(2/3) running `second`...")).Outcome);
        parser.Feed("(3/3) ignoring `optional`...");
        parser.Feed("all tests passed!");
        var results = parser.Complete();
        Assert.Equal(3, results.Count);
        Assert.Equal(EmbeddedTestOutcome.Skipped, results[2].Outcome);
        Assert.DoesNotContain(results, r => r.Outcome == EmbeddedTestOutcome.Errored);
    }

    [Fact]
    public void Defmt_disconnect_does_not_mark_the_current_test_passed()
    {
        var parser = new EmbeddedTestOutputParser("board", EmbeddedTestFormat.DefmtTest);
        parser.Feed("(1/2) running `first`...");
        parser.Feed("(2/2) running `second`...");
        var results = parser.Complete(false);
        Assert.Equal(EmbeddedTestOutcome.Passed, results[0].Outcome);
        Assert.Equal("second", results[1].Name);
        Assert.Equal(EmbeddedTestOutcome.Errored, results[1].Outcome);
    }

    [Fact]
    public void Defmt_lost_progress_and_explicit_failure_are_visible()
    {
        var parser = new EmbeddedTestOutputParser("board", EmbeddedTestFormat.DefmtTest);
        parser.Feed("(1/3) running `first`...");
        parser.Feed("(3/3) running `third`...");
        parser.Feed("test failed with outcome: Err(Error)");
        var results = parser.Complete(false);
        Assert.Contains(results, r => r.Name == "third" && r.Outcome == EmbeddedTestOutcome.Failed);
        Assert.Contains(results, r => r.Outcome == EmbeddedTestOutcome.Errored);
    }

    [Fact]
    public void Rust_text_and_json_results_ignore_compiler_events_and_keep_failures()
    {
        var parser = new EmbeddedTestOutputParser("host", EmbeddedTestFormat.Rust);
        parser.Feed("running 2 tests");
        parser.Feed("test math::adds ... ok");
        parser.Feed("test math::other ... FAILED");
        parser.Feed("{\"reason\":\"compiler-artifact\",\"target\":{\"name\":\"lib\"}}");
        var results = parser.Complete();
        Assert.Equal(2, results.Count);
        Assert.Equal(EmbeddedTestOutcome.Failed, results[1].Outcome);

        var json = new EmbeddedTestOutputParser("host", EmbeddedTestFormat.Rust);
        json.Feed("{\"type\":\"test\",\"event\":\"started\",\"name\":\"math::adds\"}");
        json.Feed("{\"type\":\"test\",\"event\":\"ok\",\"name\":\"math::adds\",\"exec_time\":0.125}");
        var result = Assert.Single(json.Complete());
        Assert.Equal(TimeSpan.FromMilliseconds(125), result.Duration);
    }

    [Fact]
    public void Rust_started_without_completion_is_an_error_and_bad_duration_types_do_not_crash()
    {
        var parser = new EmbeddedTestOutputParser("host", EmbeddedTestFormat.Rust);
        parser.Feed("{\"type\":\"test\",\"event\":\"started\",\"name\":\"unfinished\"}");
        parser.Feed("{\"type\":\"test\",\"event\":\"ok\",\"name\":\"complete\",\"exec_time\":\"not a number\"}");
        var results = parser.Complete();
        Assert.Contains(results, r => r.Name == "unfinished" && r.Outcome == EmbeddedTestOutcome.Errored);
        Assert.Equal(TimeSpan.Zero, results[0].Duration);
    }

    [Fact]
    public void GoogleTest_XML_keeps_parameterized_names_failure_text_and_source_locations()
    {
        const string xml = """
            <testsuites><testsuite name="Sensors">
              <testcase name="Reads/0" classname="SensorTests" time="0.125" file="test_sensor.cpp" line="42" />
              <testcase name="Fails" classname="SensorTests" time="0.3"><failure message="expected 1 &amp; 2" /></testcase>
              <testcase name="Missing" status="notrun" result="suppressed" />
            </testsuite></testsuites>
            """;
        var results = EmbeddedTestXml.ReadGoogleTest(xml, "host-linux");
        Assert.Equal("SensorTests.Reads/0", results[0].Name);
        Assert.Equal(TimeSpan.FromMilliseconds(125), results[0].Duration);
        Assert.Equal(42, results[0].Line);
        Assert.Equal("expected 1 & 2", results[1].Message);
        Assert.Equal(EmbeddedTestOutcome.Skipped, results[2].Outcome);
    }

    [Fact]
    public void XML_entities_are_prohibited_and_malformed_reports_fail_explicitly()
    {
        Assert.Throws<XmlException>(() => EmbeddedTestXml.ReadGoogleTest("<!DOCTYPE testsuites [<!ENTITY secret SYSTEM 'file:///etc/passwd'>]><testsuites>&secret;</testsuites>", "host"));
        Assert.Throws<InvalidDataException>(() => EmbeddedTestXml.ReadGoogleTest("<unrelated />", "host"));
        Assert.Throws<InvalidDataException>(() => EmbeddedTestXml.ReadGoogleTest("<testsuites><testcase /></testsuites>", "host"));
    }

    [Fact]
    public void Parser_limits_and_lifecycle_are_explicit()
    {
        var parser = new EmbeddedTestOutputParser("board", EmbeddedTestFormat.Unity);
        Assert.Throws<InvalidDataException>(() => parser.Feed(new string('x', 65537)));
        parser.Complete();
        Assert.Throws<InvalidOperationException>(() => parser.Feed("late data"));
        Assert.Same(parser.Results, parser.Complete());
    }
}
