using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;
using TabStudio.Toolchains.Testing;

namespace TabStudio.Toolchains.Tests.Testing;

/// <summary>
/// Test Explorer for Go, Rust and Dart. Before 0.20.5 it found no tests in spf13/cobra, urfave/cli, labstack/echo,
/// sharkdp/bat, dtolnay/anyhow, serde-rs/json or dart-lang/shelf: only JUnit XML reports were read. Their tests are now
/// found in source and run with <c>go test -v</c>, <c>cargo test</c> and <c>dart test</c>, whose output and JSON report
/// become the results, under the same names as the tests found in source.
/// </summary>
public sealed class GoRustDartTestTests : IDisposable
{
    private readonly TempDirectory _folder = new();

    [Fact]
    public void Reads_go_test_output_per_package_with_failures_skips_and_a_package_that_did_not_build()
    {
        const string Output = """
            === RUN   TestAdd
            --- PASS: TestAdd (0.00s)
            === RUN   TestDivide
                math_test.go:14: got 2, want 3
            --- FAIL: TestDivide (0.01s)
            === RUN   TestTable
            === RUN   TestTable/zero
                --- PASS: TestTable/zero (0.00s)
            --- PASS: TestTable (0.00s)
            FAIL
            FAIL	github.com/spf13/cobra	0.015s
            === RUN   TestLater
            --- SKIP: TestLater (0.00s)
                doc_test.go:9: not on this platform
            PASS
            ok  	github.com/spf13/cobra/doc	(cached)
            ?   	github.com/spf13/cobra/internal	[no test files]
            # github.com/spf13/cobra/broken [github.com/spf13/cobra/broken.test]
            broken/broken_test.go:5:2: undefined: Missing
            FAIL	github.com/spf13/cobra/broken [build failed]
            FAIL
            """;

        var results = ToolchainTestOutput.ParseGo(Output, "github.com/spf13/cobra");

        Assert.Equal(["cobra.TestAdd", "cobra.TestDivide", "cobra.TestTable", "cobra.doc.TestLater", "cobra.broken.(package)"], results.Select(r => r.Id));
        Assert.Equal([ToolchainTestOutcome.Passed, ToolchainTestOutcome.Failed, ToolchainTestOutcome.Passed, ToolchainTestOutcome.Skipped, ToolchainTestOutcome.Errored], results.Select(r => r.Outcome));
        Assert.Equal("math_test.go:14: got 2, want 3", results[1].Message);
        Assert.Equal(TimeSpan.FromMilliseconds(10), results[1].Duration);
        Assert.Contains("undefined: Missing", results[4].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Finds_go_tests_in_source_under_the_names_go_test_reports()
    {
        _folder.Write("go.mod", "module github.com/spf13/cobra\n\ngo 1.22\n");
        _folder.Write("command_test.go", "package cobra\n\nimport \"testing\"\n\nfunc TestAdd(t *testing.T) {}\n\nfunc Testable(t *testing.T) {}\n\nfunc FuzzParse(f *testing.F) {}\n\nfunc helper(t *testing.T) {}\n");
        _folder.Write("doc/man_test.go", "package doc\n\nimport \"testing\"\n\nfunc TestMan(t *testing.T) {\n}\n");
        _folder.Write("vendor/x/x_test.go", "package x\n\nimport \"testing\"\n\nfunc TestVendored(t *testing.T) {}\n");
        var project = Project("go");

        var tests = ToolchainTestDiscoverer.Discover(project.Directory, ToolchainTestFramework.GoTest);

        Assert.Equal(ToolchainTestFramework.GoTest, ToolchainTestPlanner.Detect(project));
        Assert.Equal(["cobra.TestAdd", "cobra.FuzzParse", "cobra.doc.TestMan"], tests.Select(t => t.Id));
        Assert.Equal(5, tests[0].Line);
        var all = ToolchainTestPlanner.Plan(project, ToolchainTestFramework.GoTest, null, _folder.Resolve("reports")).Command;
        Assert.Equal(["sh", "-c"], [all.Program, all.Arguments[0]]);
        Assert.Equal(["go", "test", "-v", "./..."], all.Arguments.TakeLast(4));
        var selected = ToolchainTestPlanner.Plan(project, ToolchainTestFramework.GoTest, [new("cobra.doc", "TestMan"), new("cobra", "TestAdd")], _folder.Resolve("reports")).Command;
        Assert.Equal(["./doc", ".", "-run", "^(?:TestMan|TestAdd)$"], selected.Arguments.TakeLast(4));
    }

    [Fact]
    public void Reads_cargo_test_output_per_target_with_failures_ignored_tests_and_doc_tests()
    {
        const string Output = """
               Compiling anyhow v1.0.98 (/src/anyhow)
                Finished `test` profile [unoptimized + debuginfo] target(s) in 3.10s
                 Running unittests src/lib.rs (target/debug/deps/anyhow-0f1e2d3c4b5a6978)

            running 3 tests
            test error::tests::displays ... ok
            test chain ... FAILED
            test slow ... ignored, takes a minute

            failures:

            ---- chain stdout ----

            thread 'chain' panicked at src/lib.rs:40:5:
            assertion `left == right` failed

            failures:
                chain

            test result: FAILED. 1 passed; 1 failed; 1 ignored; 0 measured; 0 filtered out; finished in 0.00s

                 Running tests/test_backtrace.rs (target/debug/deps/test_backtrace-a1b2c3d4e5f60718)

            running 1 test
            test test_backtrace ... ok

               Doc-tests anyhow

            running 1 test
            test src/lib.rs - Error::chain (line 120) ... ok
            """;

        var results = ToolchainTestOutput.ParseCargo(Output);

        Assert.Equal(["anyhow.error.tests.displays", "anyhow.chain", "anyhow.slow", "test_backtrace.test_backtrace", "anyhow.doctests.src/lib.rs - Error::chain (line 120)"], results.Select(r => r.Id));
        Assert.Equal([ToolchainTestOutcome.Passed, ToolchainTestOutcome.Failed, ToolchainTestOutcome.Skipped, ToolchainTestOutcome.Passed, ToolchainTestOutcome.Passed], results.Select(r => r.Outcome));
        Assert.Contains("assertion `left == right` failed", results[1].Message, StringComparison.Ordinal);
        Assert.Null(results[0].Message);
    }

    [Fact]
    public void Finds_rust_tests_in_source_under_the_names_cargo_test_reports()
    {
        _folder.Write("Cargo.toml", "[package]\nname = \"mini-redis\"\nversion = \"0.4.1\"\n");
        _folder.Write("src/lib.rs", "pub mod frame;\n\n#[cfg(test)]\nmod tests {\n    #[test]\n    fn parses() {\n    }\n\n    mod nested {\n        #[tokio::test]\n        async fn waits() {}\n    }\n\n    #[test]\n    fn after_nested() {}\n}\n");
        _folder.Write("src/frame.rs", "#[test]\n#[should_panic]\nfn rejects() {}\n\nfn helper() {}\n");
        _folder.Write("tests/server.rs", "#[tokio::test(flavor = \"multi_thread\")]\nasync fn key_value_get_set() {}\n");
        _folder.Write("target/debug/build/x/out.rs", "#[test]\nfn generated() {}\n");
        var project = Project("rust");

        var tests = ToolchainTestDiscoverer.Discover(project.Directory, ToolchainTestFramework.Cargo);

        Assert.Equal(ToolchainTestFramework.Cargo, ToolchainTestPlanner.Detect(project));
        Assert.Equal(["mini_redis.frame.rejects", "mini_redis.tests.parses", "mini_redis.tests.nested.waits", "mini_redis.tests.after_nested", "server.key_value_get_set"], tests.Select(t => t.Id));
        var results = ToolchainTestOutput.ParseCargo("     Running unittests src/lib.rs (target/debug/deps/mini_redis-0123456789abcdef)\ntest tests::nested::waits ... ok\n");
        Assert.Equal("mini_redis.tests.nested.waits", results.Single().Id);
        var selected = ToolchainTestPlanner.Plan(project, ToolchainTestFramework.Cargo, [new("mini_redis.tests.nested", "waits"), new("server", null)], _folder.Resolve("reports")).Command;
        Assert.Equal(["cargo", "test", "--no-fail-fast", "--", "tests::nested::waits", ""], selected.Arguments.TakeLast(6));
    }

    [Fact]
    public void Reads_dart_test_json_reports_and_runs_dart_or_flutter_test()
    {
        _folder.Write("pubspec.yaml", "name: shelf\nenvironment:\n  sdk: ^3.3.0\n");
        _folder.Write("test/shelf_test.dart", "void main() {}\n");
        var report = string.Join('\n',
            """{"protocolVersion":"0.1.1","runnerVersion":"1.25.8","type":"start","time":0}""",
            """{"suite":{"id":0,"platform":"vm","path":"test/shelf_test.dart"},"type":"suite","time":1}""",
            """{"test":{"id":1,"name":"loading test/shelf_test.dart","suiteID":0,"groupIDs":[],"line":null,"column":null,"url":null},"type":"testStart","time":2}""",
            """{"testID":1,"result":"success","skipped":false,"hidden":true,"type":"testDone","time":300}""",
            """{"test":{"id":3,"name":"Request handles a body","suiteID":0,"groupIDs":[2],"line":12,"column":5,"url":"file:///p/test/shelf_test.dart"},"type":"testStart","time":301}""",
            """{"testID":3,"result":"success","skipped":false,"hidden":false,"type":"testDone","time":320}""",
            """{"test":{"id":4,"name":"Request rejects a bad url","suiteID":0,"groupIDs":[2],"line":20,"column":5,"url":"file:///p/test/shelf_test.dart"},"type":"testStart","time":321}""",
            """{"testID":4,"error":"Expected: <400>\n  Actual: <200>","stackTrace":"test/shelf_test.dart 22:7  main.<fn>","isFailure":true,"type":"error","time":330}""",
            """{"testID":4,"result":"failure","skipped":false,"hidden":false,"type":"testDone","time":331}""",
            """{"test":{"id":5,"name":"Request later","suiteID":0,"groupIDs":[2],"line":30,"column":5,"url":null},"type":"testStart","time":332}""",
            """{"testID":5,"result":"success","skipped":true,"hidden":false,"type":"testDone","time":333}""",
            """{"success":false,"type":"done","time":400}""");

        var results = ToolchainTestOutput.ParseDart(report, _folder.Path);

        Assert.Equal(["test.shelf_test.Request handles a body", "test.shelf_test.Request rejects a bad url", "test.shelf_test.Request later"], results.Select(r => r.Id));
        Assert.Equal([ToolchainTestOutcome.Passed, ToolchainTestOutcome.Failed, ToolchainTestOutcome.Skipped], results.Select(r => r.Outcome));
        Assert.Contains("Actual: <200>", results[1].Message, StringComparison.Ordinal);
        Assert.Equal(20, results[1].Line);
        var project = Project("flutter");
        Assert.Equal(ToolchainTestFramework.DartTest, ToolchainTestPlanner.Detect(project));
        var plan = ToolchainTestPlanner.Plan(project, ToolchainTestFramework.DartTest, null, _folder.Resolve("reports")).Command;
        Assert.Equal(["dart", "test", "--file-reporter"], new[] { plan.Program }.Concat(plan.Arguments.Take(2)));
        Assert.StartsWith("json:", plan.Arguments[2], StringComparison.Ordinal);
        _folder.Write("pubspec.yaml", "name: app\nenvironment:\n  sdk: ^3.3.0\ndependencies:\n  flutter:\n    sdk: flutter\n");
        Assert.Equal(ToolchainTestFramework.FlutterTest, ToolchainTestPlanner.Detect(project));
    }

    [Fact]
    public void A_rust_package_without_tests_and_a_go_module_without_test_files_have_no_runner()
    {
        _folder.Write("rs/Cargo.toml", "[package]\nname = \"plain\"\n");
        _folder.Write("rs/src/main.rs", "fn main() {}\n");
        _folder.Write("go/go.mod", "module example.com/plain\n");
        _folder.Write("go/main.go", "package main\n\nfunc main() {}\n");

        Assert.Null(ToolchainTestPlanner.Detect(new ToolchainProject(ToolchainCatalog.Default.Find("rust")!, _folder.Resolve("rs"), null)));
        Assert.Null(ToolchainTestPlanner.Detect(new ToolchainProject(ToolchainCatalog.Default.Find("go")!, _folder.Resolve("go"), null)));
    }

    private ToolchainProject Project(string toolchain) =>
        new(ToolchainCatalog.Default.Find(toolchain)!, Path.GetFullPath(_folder.Path), null);

    public void Dispose() => _folder.Dispose();
}
