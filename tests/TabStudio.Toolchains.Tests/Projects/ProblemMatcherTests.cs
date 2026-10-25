using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>Finding file locations and the final error in Python and Node.js output.</summary>
public sealed class ProblemMatcherTests
{
    private static readonly string[] PythonTraceback =
    [
        "Traceback (most recent call last):",
        "  File \"/storage/emulated/0/Projects/demo/main.py\", line 7, in <module>",
        "    main()",
        "  File \"/storage/emulated/0/Projects/demo/main.py\", line 4, in main",
        "    print(greet(name))",
        "  File \"/usr/lib/python3.14/textwrap.py\", line 120, in greet",
        "NameError: name 'nme' is not defined",
    ];

    private static readonly string[] NodeStack =
    [
        "/storage/emulated/0/Projects/web/index.js:3",
        "  throw new TypeError('bad input');",
        "  ^",
        string.Empty,
        "TypeError: bad input",
        "    at check (/storage/emulated/0/Projects/web/index.js:3:9)",
        "    at Object.<anonymous> (/storage/emulated/0/Projects/web/index.js:6:1)",
        "    at Module._compile (node:internal/modules/cjs/loader:1730:14)",
        "    at /storage/emulated/0/Projects/web/lib/run.js:12:5",
        string.Empty,
        "Node.js v24.11.1",
    ];

    private static ProblemMatcher Python => new(ToolchainCatalog.Default.Find("python")!);

    private static ProblemMatcher Node => new(ToolchainCatalog.Default.Find("node")!);

    [Fact]
    public void Links_the_frames_of_a_Python_traceback()
    {
        var line = PythonTraceback[1];

        var location = Assert.Single(Python.Match(line));

        Assert.Equal("/storage/emulated/0/Projects/demo/main.py", location.File);
        Assert.Equal(7, location.Line);
        Assert.Null(location.Column);
        Assert.Equal("File \"/storage/emulated/0/Projects/demo/main.py\", line 7", line.Substring(location.Start, location.Length));
    }

    [Fact]
    public void Finds_a_Python_error_at_the_deepest_frame_in_the_users_code()
    {
        var error = Python.FindError(PythonTraceback, file => file.StartsWith("/storage/", StringComparison.Ordinal))!;

        Assert.Equal("NameError: name 'nme' is not defined", error.Message);
        Assert.Equal(("/storage/emulated/0/Projects/demo/main.py", 4), (error.Location!.File, error.Location.Line));
    }

    [Fact]
    public void Links_the_frames_of_a_Node_stack_trace_with_columns()
    {
        var located = NodeStack.Select(l => Node.Match(l)).Where(m => m.Count > 0).Select(m => Assert.Single(m)).ToList();

        Assert.Equal(
            [
                ("/storage/emulated/0/Projects/web/index.js", 3, (int?)null),
                ("/storage/emulated/0/Projects/web/index.js", 3, 9),
                ("/storage/emulated/0/Projects/web/index.js", 6, 1),
                ("/storage/emulated/0/Projects/web/lib/run.js", 12, 5),
            ],
            located.Select(l => (l.File, l.Line, l.Column)));
    }

    [Fact]
    public void Finds_a_Node_error_at_the_first_frame_after_it()
    {
        var error = Node.FindError(NodeStack, file => file.StartsWith("/storage/", StringComparison.Ordinal))!;

        Assert.Equal("TypeError: bad input", error.Message);
        Assert.Equal((3, 9), (error.Location!.Line, error.Location.Column));
    }

    [Fact]
    public void An_error_outside_the_users_code_has_no_location()
    {
        var error = Python.FindError(["Traceback (most recent call last):", "  File \"<stdin>\", line 1, in <module>", "KeyboardInterrupt"], _ => false)!;

        Assert.Equal("KeyboardInterrupt", error.Message);
        Assert.Null(error.Location);
    }

    [Fact]
    public void Compiler_errors_keep_every_location_and_leave_warnings_out()
    {
        var matcher = new ProblemMatcher(new ToolchainDefinition
        {
            Id = "compiler", Name = "Compiler", Description = "A compiler.",
            ProblemPatterns = [new() { Pattern = @"^(?<file>.+):(?<line>\d+):(?<column>\d+): (?<severity>error|warning|note): (?<message>.+)$" }],
        });

        var errors = matcher.FindErrors(["main.c:3:2: error: unknown name", "main.c:5:1: error: expected ;", "main.c:6:1: warning: unused", "lib.c:1:1: error: outside"], file => file == "main.c");

        Assert.Equal(["unknown name", "expected ;"], errors.Select(e => e.Message));
        Assert.Equal([3, 5], errors.Select(e => e.Location!.Line));
        Assert.Empty(matcher.FindErrors(["Hello, world!", "main.c:0:2: error: invalid line"], _ => true));
    }

    [Fact]
    public void Ordinary_output_is_neither_linked_nor_an_error()
    {
        string[] output = ["Hello, Ada!", "Total: 3 files, line 4 of 9", "Errors: none"];

        Assert.All(output, l => Assert.Empty(Python.Match(l)));
        Assert.Null(Python.FindError(output, _ => true));
        Assert.Null(Node.FindError(output, _ => true));
    }
}
