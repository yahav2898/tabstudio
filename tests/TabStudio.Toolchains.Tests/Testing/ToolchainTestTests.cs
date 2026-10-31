using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;
using TabStudio.Toolchains.Testing;

namespace TabStudio.Toolchains.Tests.Testing;

/// <summary>
/// Test Explorer for PHP, Kotlin, Java, Swift and Python: finding tests in source, running them with each project's own
/// runner, and reading the JUnit XML reports the runners write.
/// </summary>
public sealed class ToolchainTestTests : IDisposable
{
    private readonly TempDirectory _folder = new();

    private static ToolchainDefinition Toolchain(string id) => ToolchainCatalog.Default.Find(id)!;

    [Fact]
    public void Reads_PHPUnit_results_with_their_namespaces_data_sets_and_locations()
    {
        const string Report = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuites>
              <testsuite name="Tests" tests="4">
                <testsuite name="Tests\Unit\MathTest" file="/app/tests/Unit/MathTest.php">
                  <testcase name="test_adds" file="/app/tests/Unit/MathTest.php" line="12" class="Tests\Unit\MathTest" classname="Tests.Unit.MathTest" assertions="1" time="0.001500"/>
                  <testcase name="test_divides" file="/app/tests/Unit/MathTest.php" line="18" class="Tests\Unit\MathTest" classname="Tests.Unit.MathTest" time="0.002">
                    <failure type="PHPUnit\Framework\ExpectationFailedException">Tests\Unit\MathTest::test_divides
            Failed asserting that 2 is identical to 3.

            /app/tests/Unit/MathTest.php:20</failure>
                  </testcase>
                  <testsuite name="Tests\Unit\MathTest::test_squares">
                    <testcase name="test_squares with data set #0" file="/app/tests/Unit/MathTest.php" line="25" class="Tests\Unit\MathTest" classname="Tests.Unit.MathTest" time="0.0001"/>
                  </testsuite>
                  <testcase name="test_later" class="Tests\Unit\MathTest" classname="Tests.Unit.MathTest" time="0"><skipped/></testcase>
                </testsuite>
              </testsuite>
            </testsuites>
            """;

        var results = JUnitReport.Parse(Report);

        Assert.Equal(["Tests.Unit.MathTest.test_adds", "Tests.Unit.MathTest.test_divides", "Tests.Unit.MathTest.test_squares with data set #0", "Tests.Unit.MathTest.test_later"], results.Select(r => r.Id));
        Assert.Equal([ToolchainTestOutcome.Passed, ToolchainTestOutcome.Failed, ToolchainTestOutcome.Passed, ToolchainTestOutcome.Skipped], results.Select(r => r.Outcome));
        Assert.Equal("test_squares", results[2].MethodName);
        Assert.Equal(("/app/tests/Unit/MathTest.php", 12), (results[0].File, results[0].Line));
        Assert.Equal(TimeSpan.FromMilliseconds(1.5), results[0].Duration);
        Assert.Contains("Failed asserting that 2 is identical to 3.", results[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_JUnit_5_and_Swift_results_without_their_parameter_lists()
    {
        const string Gradle = """
            <testsuite name="demo.GreetTest" tests="2">
              <testcase name="greets by name()" classname="demo.GreetTest" time="0.012"/>
              <testcase name="fails()" classname="demo.GreetTest" time="0.003">
                <failure message="expected: &lt;1&gt; but was: &lt;2&gt;" type="org.opentest4j.AssertionFailedError">org.opentest4j.AssertionFailedError: expected: &lt;1&gt; but was: &lt;2&gt;
                at demo.GreetTest.fails(GreetTest.kt:14)</failure>
              </testcase>
            </testsuite>
            """;
        const string Swift = """
            <testsuites><testsuite name="TestResults" errors="0" tests="2" failures="1" time="0.1">
            <testcase classname="ToolTests.ToolTests" name="testAdds" time="0.01"></testcase>
            <testcase classname="ToolTests.ToolTests" name="testFails" time="0.02"><failure message="failure"></failure></testcase>
            </testsuite></testsuites>
            """;

        var gradle = JUnitReport.Parse(Gradle);
        var swift = JUnitReport.Parse(Swift);

        Assert.Equal(["demo.GreetTest.greets by name", "demo.GreetTest.fails"], gradle.Select(r => r.Id));
        Assert.Contains("expected: <1> but was: <2>", gradle[1].Message, StringComparison.Ordinal);
        Assert.Equal(["ToolTests.ToolTests.testAdds", "ToolTests.ToolTests.testFails"], swift.Select(r => r.Id));
        Assert.Equal("The test failed.", swift[1].Message);
    }

    [Fact]
    public void Refuses_reports_with_a_document_type()
    {
        Assert.Throws<System.Xml.XmlException>(() => JUnitReport.Parse("<!DOCTYPE x [<!ENTITY a \"b\">]><testsuite><testcase name=\"&a;\"/></testsuite>"));
    }

    [Fact]
    public void Finds_PHPUnit_tests_by_name_and_attribute()
    {
        var file = _folder.Write("tests/Unit/MathTest.php", """
            <?php

            namespace Tests\Unit;

            use PHPUnit\Framework\Attributes\Test;
            use PHPUnit\Framework\TestCase;

            final class MathTest extends TestCase
            {
                public function test_adds(): void {}

                #[Test]
                public function divides(): void {}

                /** @test */
                public function squares(): void {}

                public function helper(): void {}
            }
            """);

        var tests = ToolchainTestDiscoverer.Discover(_folder.Path, ToolchainTestFramework.PhpUnit);

        Assert.Equal(["Tests.Unit.MathTest.test_adds", "Tests.Unit.MathTest.divides", "Tests.Unit.MathTest.squares"], tests.Select(t => t.Id));
        Assert.Equal((file, 10, 8), (tests[0].File, tests[0].Line, tests[0].ClassLine));
    }

    [Fact]
    public void Finds_JUnit_tests_in_Kotlin_and_Java_sources_of_every_module()
    {
        _folder.Write("app/src/test/kotlin/demo/GreetTest.kt", """
            package demo

            import kotlin.test.Test

            class GreetTest {
                @Test
                fun `greets by name`() {}

                @Test fun adds() {}

                fun helper() {}
            }
            """);
        _folder.Write("lib/src/test/java/demo/MathTest.java", """
            package demo;

            import org.junit.jupiter.api.Test;
            import org.junit.jupiter.params.ParameterizedTest;

            class MathTest {
                @Test
                void adds() {}

                @ParameterizedTest
                @ValueSource(ints = {1, 2})
                public void squares(int value) {}
            }
            """);

        var tests = ToolchainTestDiscoverer.Discover(_folder.Path, ToolchainTestFramework.Gradle);

        Assert.Equal(["demo.GreetTest.greets by name", "demo.GreetTest.adds", "demo.MathTest.adds", "demo.MathTest.squares"], tests.Select(t => t.Id));
        Assert.Equal(7, tests[0].Line);
    }

    [Fact]
    public void Finds_XCTest_and_Swift_Testing_tests_as_swift_test_reports_them()
    {
        _folder.Write("Package.swift", "// swift-tools-version:6.0\n");
        _folder.Write("Tests/ToolTests/ToolTests.swift", """
            import XCTest
            @testable import Tool

            final class ToolTests: XCTestCase {
                func testAdds() {}
                func helper() {}
            }
            """);
        _folder.Write("Tests/ToolTests/Suite.swift", """
            import Testing

            @Test func topLevel() {}

            struct Arithmetic {
                @Test("Adds numbers") func adds() {}
                func helper() {}
            }
            """);

        var tests = ToolchainTestDiscoverer.Discover(_folder.Path, ToolchainTestFramework.SwiftPM);

        Assert.Equal(["ToolTests.topLevel", "ToolTests.Arithmetic.adds", "ToolTests.ToolTests.testAdds"], tests.Select(t => t.Id));
    }

    [Fact]
    public void Finds_pytest_tests_as_pytest_reports_them_and_runs_a_selection_by_node_id()
    {
        // As pytest 9 reports this file with --junitxml: "tests.unit.test_math" for its functions and
        // "tests.unit.test_math.TestDivide" for a class's methods, and "test_squares[2]" for a parametrized test.
        var file = _folder.Write("py/tests/unit/test_math.py", """
            import pytest, unittest

            def test_adds():
                def test_helper():
                    pass
                assert 1 + 1 == 2

            @pytest.mark.parametrize("x", [1, 2])
            def test_squares(x):
                assert x * x == x

            class TestDivide:
                # A comment first.
                def test_divides(self):
                    assert 4 / 2 == 2

                async def test_awaits(self):
                    pass

                def helper(self):
                    pass

            class Legacy(unittest.TestCase):
                def test_old(self):
                    self.assertTrue(True)

            class Helper:
                def test_not_collected(self):
                    pass

            def helper():
                pass
            """);
        _folder.Write("py/.venv/lib/site-packages/pkg/test_vendor.py", "def test_theirs(): pass");
        _folder.Write("py/node_modules/x/test_x.py", "def test_theirs(): pass");
        _folder.Write("py/requirements-dev.txt", "pytest==9.1.1\n");
        var project = Project("python", "py");

        Assert.Equal(ToolchainTestFramework.Pytest, ToolchainTestPlanner.Detect(project));
        var tests = ToolchainTestDiscoverer.Discover(project.Directory, ToolchainTestFramework.Pytest);
        Assert.Equal(
            ["tests.unit.test_math.test_adds", "tests.unit.test_math.test_squares", "tests.unit.test_math.TestDivide.test_divides",
             "tests.unit.test_math.TestDivide.test_awaits", "tests.unit.test_math.Legacy.test_old"],
            tests.Select(t => t.Id));
        Assert.Equal((file, 3), (tests[0].File, tests[0].Line));
        Assert.Equal((14, 12), (tests[2].Line, tests[2].ClassLine));

        var result = Assert.Single(JUnitReport.Parse("""<testsuite><testcase classname="tests.unit.test_math" name="test_squares[2]"><failure message="assert 4 == 2"/></testcase></testsuite>"""));
        Assert.Equal(("tests.unit.test_math", "test_squares"), (result.ClassName, result.MethodName));

        var plan = ToolchainTestPlanner.Plan(project, ToolchainTestFramework.Pytest,
            [new("tests.unit.test_math", "test_squares"), new("tests.unit.test_math.TestDivide", null)], _folder.Resolve("reports"));
        Assert.Equal(["tests/unit/test_math.py::test_squares", "tests/unit/test_math.py::TestDivide"], plan.Command.Arguments.TakeLast(2));
        Assert.Equal(["-c", ToolchainTestPlanner.PythonTestInstall(["-r", "requirements-dev.txt"]) + ToolchainTestPlanner.PytestScript, "python3"], plan.Command.Arguments.Take(3));
        Assert.Contains("--junitxml=" + Path.Combine(_folder.Resolve("reports"), "pytest.xml"), plan.Command.Arguments);

        // A project that does not use pytest has no tests to list.
        _folder.Write("plain/main.py", "print('hi')");
        _folder.Write("plain/requirements.txt", "requests\n");
        Assert.Null(ToolchainTestPlanner.Detect(Project("python", "plain")));
    }

    [Fact]
    public void Detects_each_projects_runner()
    {
        _folder.Write("php/composer.json", """{ "require-dev": { "phpunit/phpunit": "^12.0" } }""");
        _folder.Write("pest/composer.json", """{ "require-dev": { "pestphp/pest": "^4.0" } }""");
        _folder.Write("gradle/build.gradle.kts", "");
        _folder.Write("gradle/src/test/kotlin/A.kt", "");
        _folder.Write("maven/pom.xml", "<project/>");
        _folder.Write("maven/src/test/java/A.java", "");
        _folder.Write("noTests/build.gradle.kts", "");
        _folder.Write("swift/Package.swift", ".testTarget(name: \"T\")");

        Assert.Equal(ToolchainTestFramework.PhpUnit, ToolchainTestPlanner.Detect(Project("php", "php")));
        Assert.Equal(ToolchainTestFramework.Pest, ToolchainTestPlanner.Detect(Project("php", "pest")));
        Assert.Equal(ToolchainTestFramework.Gradle, ToolchainTestPlanner.Detect(Project("kotlin", "gradle")));
        Assert.Equal(ToolchainTestFramework.Maven, ToolchainTestPlanner.Detect(Project("java", "maven")));
        Assert.Null(ToolchainTestPlanner.Detect(Project("kotlin", "noTests")));
        Assert.Equal(ToolchainTestFramework.SwiftPM, ToolchainTestPlanner.Detect(Project("swift", "swift")));
    }

    [Fact]
    public void Runs_the_selected_tests_with_each_runners_own_filter()
    {
        var reports = _folder.Resolve("reports");
        ToolchainTestSelector[] selection = [new("Tests.Unit.MathTest", "test_adds"), new("demo.GreetTest", null)];

        var php = ToolchainTestPlanner.Plan(Project("php", "."), ToolchainTestFramework.PhpUnit, selection, reports).Command;
        Assert.Equal(["php", "vendor/bin/phpunit"], [php.Program, php.Arguments[0]]);
        Assert.Equal(@"/^(?:Tests\\Unit\\MathTest::test_adds(?: with data set .*)?|demo\\GreetTest::.*)$/", php.Arguments[^1]);
        Assert.Contains(Path.Combine(reports, "phpunit.xml"), php.Arguments);

        var gradle = ToolchainTestPlanner.Plan(Project("kotlin", "."), ToolchainTestFramework.Gradle, selection, reports).Command;
        Assert.Equal(["--tests", "Tests.Unit.MathTest.test_adds", "--tests", "demo.GreetTest"], gradle.Arguments.TakeLast(4));
        Assert.Contains("ignoreFailures = true", File.ReadAllText(Path.Combine(reports, "tabstudio-tests.init.gradle")), StringComparison.Ordinal);

        var maven = ToolchainTestPlanner.Plan(Project("java", "."), ToolchainTestFramework.Maven, selection, reports).Command;
        Assert.Equal("-Dtest=Tests.Unit.MathTest#test_adds,demo.GreetTest", maven.Arguments[^1]);
        Assert.Contains("-Dmaven.test.failure.ignore=true", maven.Arguments);

        var swift = ToolchainTestPlanner.Plan(Project("swift", "."), ToolchainTestFramework.SwiftPM, [new("ToolTests.ToolTests", "testAdds")], reports).Command;
        Assert.Equal(["test", "--parallel", "--xunit-output"], swift.Arguments.Take(3));
        Assert.Equal(@"ToolTests\.ToolTests[./]testAdds(?:\(|$)", swift.Arguments[^1]);

        var all = ToolchainTestPlanner.Plan(Project("swift", "."), ToolchainTestFramework.SwiftPM, null, reports).Command;
        Assert.DoesNotContain("--filter", all.Arguments);
    }

    [Fact]
    public void Reads_the_reports_of_every_Gradle_module_written_by_the_run()
    {
        var started = DateTime.UtcNow;
        _folder.Write("app/build/test-results/test/TEST-demo.A.xml", "<testsuite><testcase classname=\"demo.A\" name=\"a()\"/></testsuite>");
        _folder.Write("lib/build/test-results/test/TEST-demo.B.xml", "<testsuite><testcase classname=\"demo.B\" name=\"b()\"><failure message=\"no\"/></testcase></testsuite>");
        var old = _folder.Write("old/build/test-results/test/TEST-demo.C.xml", "<testsuite><testcase classname=\"demo.C\" name=\"c()\"/></testsuite>");
        File.SetLastWriteTimeUtc(old, started.AddHours(-1));
        _folder.Write("app/build/test-results/test/binary/output.bin", "");
        var project = Project("kotlin", ".");
        var plan = ToolchainTestPlanner.Plan(project, ToolchainTestFramework.Gradle, null, _folder.Resolve(".tabstudio/test-results/run"));

        var results = ToolchainTestPlanner.ReadResults(project, plan, started);

        Assert.Equal(["demo.A.a", "demo.B.b"], results.Select(r => r.Id));
        Assert.Equal(ToolchainTestOutcome.Failed, results[1].Outcome);
    }

    private ToolchainProject Project(string toolchain, string folder) =>
        new(Toolchain(toolchain), Path.GetFullPath(_folder.Resolve(folder)), null);

    public void Dispose() => _folder.Dispose();
}
