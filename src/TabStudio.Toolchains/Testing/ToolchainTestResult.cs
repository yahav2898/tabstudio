namespace TabStudio.Toolchains.Testing;

/// <summary>The test runners of toolchain projects whose results Test Explorer shows.</summary>
public enum ToolchainTestFramework
{
    /// <summary>PHPUnit, run as <c>vendor/bin/phpunit</c>, as Laravel's and Symfony's tests are.</summary>
    PhpUnit,

    /// <summary>Pest, PHPUnit's test runner with its own syntax, run as <c>vendor/bin/pest</c>.</summary>
    Pest,

    /// <summary>JUnit tests of a Gradle build, run by its <c>test</c> tasks.</summary>
    Gradle,

    /// <summary>JUnit tests of a Maven build, run by Surefire.</summary>
    Maven,

    /// <summary>XCTest and Swift Testing tests of a Swift package, run by <c>swift test</c>.</summary>
    SwiftPM,

    /// <summary>pytest's tests of a Python project, and the <c>unittest</c> tests pytest runs too.</summary>
    Pytest,

    /// <summary>The tests of a Go module, run by <c>go test -v ./...</c>.</summary>
    GoTest,

    /// <summary>The tests of a Rust package or workspace, run by <c>cargo test</c>.</summary>
    Cargo,

    /// <summary>The tests of a Dart package, run by <c>dart test</c>.</summary>
    DartTest,

    /// <summary>The tests of a Flutter app or package, run by <c>flutter test</c>.</summary>
    FlutterTest,

    /// <summary>Vitest's tests of a Node.js package, run by <c>vitest run</c>.</summary>
    Vitest,

    /// <summary>Jest's tests of a Node.js package, run by <c>jest</c>.</summary>
    Jest,

    /// <summary>Mocha's tests of a Node.js package, run by <c>mocha</c>.</summary>
    Mocha,

    /// <summary>The tests a Node.js package runs with Node's own test runner, <c>node --test</c>.</summary>
    NodeTest,
}

/// <summary>How a toolchain project's test ended.</summary>
public enum ToolchainTestOutcome
{
    /// <summary>It passed.</summary>
    Passed,

    /// <summary>An assertion failed.</summary>
    Failed,

    /// <summary>It was skipped.</summary>
    Skipped,

    /// <summary>It threw, or did not run to the end.</summary>
    Errored,
}

/// <summary>A test's result, as its runner's JUnit XML report states it.</summary>
/// <param name="ClassName">The test class, with <c>.</c> between its namespace or package and its name.</param>
/// <param name="MethodName">The test method, without its data set or arguments.</param>
/// <param name="Name">The test's name in the report: its method, with its data set for a parameterized test.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Duration">How long it ran.</param>
/// <param name="Message">The failure's message and stack trace, or the reason it was skipped.</param>
/// <param name="File">The file the report says the test is in, when it says.</param>
/// <param name="Line">The test's line in <paramref name="File"/>, 1-based, when the report says.</param>
public sealed record ToolchainTestResult(
    string ClassName, string MethodName, string Name, ToolchainTestOutcome Outcome, TimeSpan Duration, string? Message, string? File, int? Line)
{
    /// <summary>The test's identity, the same for its source and its result: its class and its name.</summary>
    public string Id => ClassName + "." + Name;
}

/// <summary>A test found in a toolchain project's source, before it runs.</summary>
/// <param name="ClassName">The test class, with <c>.</c> between its namespace or package and its name.</param>
/// <param name="MethodName">The test method, as its runner reports it.</param>
/// <param name="File">The file.</param>
/// <param name="Line">The method's line, 1-based.</param>
/// <param name="ClassLine">The class's line, 1-based.</param>
public sealed record ToolchainTestCase(string ClassName, string MethodName, string File, int Line, int ClassLine)
{
    /// <summary>The test's identity, the same as its result's <see cref="ToolchainTestResult.Id"/>.</summary>
    public string Id => ClassName + "." + MethodName;
}

/// <summary>Tests to run: a class, or one of its methods.</summary>
/// <param name="ClassName">The class, with <c>.</c> between its namespace or package and its name.</param>
/// <param name="MethodName">The method, or <see langword="null"/> for every test of the class.</param>
public sealed record ToolchainTestSelector(string ClassName, string? MethodName);
