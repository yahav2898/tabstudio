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
    public void An_ES_modules_error_is_found_at_its_file_URL_with_spaces_decoded()
    {
        // The Node.js template is an ES module ("type": "module"): its frames are file:// URLs, which no pattern took.
        string[] output =
        [
            "file:///storage/emulated/0/Projects/web/lib%20dir/helper.js:2",
            "  return undefinedThing + 1;",
            "  ^",
            string.Empty,
            "ReferenceError: undefinedThing is not defined",
            "    at boom (file:///storage/emulated/0/Projects/web/lib%20dir/helper.js:2:3)",
            "    at file:///storage/emulated/0/Projects/web/index.js:2:13",
            "    at ModuleJob.run (node:internal/modules/esm/module_job:343:25)",
        ];

        var error = Node.FindError(output, file => file.StartsWith("/storage/", StringComparison.Ordinal))!;

        Assert.Equal("ReferenceError: undefinedThing is not defined", error.Message);
        Assert.Equal(("/storage/emulated/0/Projects/web/lib dir/helper.js", 2, 3), (error.Location!.File, error.Location.Line, error.Location.Column));
        Assert.Equal(("/storage/emulated/0/Projects/web/index.js", 2, 13), (Assert.Single(Node.Match(output[6])).File, 2, 13));
        Assert.Equal("/storage/emulated/0/Projects/web/lib dir/helper.js", Assert.Single(Node.Match(output[0])).File);
    }

    [Fact]
    public void A_Rust_panic_is_listed_at_its_line_with_its_message()
    {
        // The panic's location is on its own line and its message on the next; nothing reached the Error List.
        string[] output =
        [
            "thread 'main' (29844) panicked at src/main.rs:3:21:",
            "index out of bounds: the len is 0 but the index is 3",
            "note: run with `RUST_BACKTRACE=1` environment variable to display a backtrace",
        ];

        var error = Assert.Single(new ProblemMatcher(ToolchainCatalog.Default.Find("rust")!).FindErrors(output, _ => true));

        Assert.Equal("thread 'main' (29844) panicked at src/main.rs:3:21: index out of bounds: the len is 0 but the index is 3", error.Message);
        Assert.Equal(("src/main.rs", 3, (int?)21), (error.Location!.File, error.Location.Line, error.Location.Column));
    }

    [Fact]
    public void TypeScript_compiler_errors_are_listed_with_their_code()
    {
        // tsc writes file(line,column): the TypeScript template's and Capacitor's build errors were never listed.
        string[] output =
        [
            "> tsc && node dist/index.js",
            string.Empty,
            "src/index.ts(1,7): error TS2322: Type 'string' is not assignable to type 'number'.",
            "src/util/math.ts(4,10): error TS2304: Cannot find name 'nmae'.",
        ];

        var errors = Node.FindErrors(output, _ => true);

        Assert.Equal(
            [
                ("src/index.ts", 1, (int?)7, "TS2322: Type 'string' is not assignable to type 'number'."),
                ("src/util/math.ts", 4, 10, "TS2304: Cannot find name 'nmae'."),
            ],
            errors.Select(e => (e.Location!.File, e.Location.Line, e.Location.Column, e.Message)));
    }

    [Fact]
    public void C_link_and_CMake_errors_are_listed_where_gcc_lld_and_CMake_print_them()
    {
        // A failed link printed no compiler error, so the Error List stayed empty after "collect2: error".
        var cpp = new ProblemMatcher(ToolchainCatalog.Default.Find("cpp")!);
        var debug = Assert.Single(cpp.FindErrors(
            ["/usr/bin/ld: /tmp/cci8dxnk.o: in function `main':", "/src/app/main.c:3:(.text+0x9): undefined reference to `foo'", "collect2: error: ld returned 1 exit status"],
            _ => true));
        Assert.Equal(("undefined reference to `foo'", "/src/app/main.c", 3), (debug.Message, debug.Location?.File, debug.Location?.Line));

        var release = Assert.Single(cpp.FindErrors(["/usr/bin/ld: /tmp/cc2u4NbD.o: in function `main':", "main.c:(.text+0x9): undefined reference to `foo'", "collect2: error: ld returned 1 exit status"], _ => true));
        Assert.Equal("main.c:(.text+0x9): undefined reference to `foo'", release.Message);

        var lld = Assert.Single(cpp.FindErrors(["ld.lld: error: undefined symbol: foo", ">>> referenced by main.c:3 (/src/main.c:3)", "clang: error: linker command failed with exit code 1 (use -v to see invocation)"], _ => true));
        Assert.Equal(("ld.lld: error: undefined symbol: foo", "main.c", 3), (lld.Message, lld.Location?.File, lld.Location?.Line));

        Assert.Equal("/usr/bin/ld: cannot find -lnotthere: No such file or directory",
            Assert.Single(cpp.FindErrors(["/usr/bin/ld: cannot find -lnotthere: No such file or directory", "collect2: error: ld returned 1 exit status"], _ => true)).Message);

        var cmake = Assert.Single(cpp.FindErrors(["-- Configuring done (0.1s)", "CMake Error at CMakeLists.txt:3 (add_executable):", "  No SOURCES given to target: app", "", "", "CMake Generate step failed.  Build files cannot be regenerated correctly."], _ => true));
        Assert.Equal(("CMake Error at CMakeLists.txt:3 (add_executable): No SOURCES given to target: app", "CMakeLists.txt", 3), (cmake.Message, cmake.Location?.File, cmake.Location?.Line));
    }

    [Fact]
    public void An_uncaught_Java_or_Kotlin_exception_is_listed_at_its_first_frame_in_the_users_code()
    {
        // A Java program's crash listed nothing: Java had no error pattern, and neither language linked its frames.
        string[] output =
        [
            "Exception in thread \"main\" java.lang.ArithmeticException: / by zero",
            "\tat java.base/java.lang.Math.floorDiv(Math.java:1)",
            "\tat com.example.shop.Cart.total(Cart.java:42)",
            "\tat com.example.shop.Main.main(Main.java:7)",
        ];
        var java = new ProblemMatcher(ToolchainCatalog.Default.Find("java")!);
        var error = Assert.Single(java.FindErrors(output, file => file.StartsWith("com/", StringComparison.Ordinal)));
        Assert.Equal(("java.lang.ArithmeticException: / by zero", "com/example/shop/Cart.java", 42), (error.Message, error.Location?.File, error.Location?.Line));

        var kotlin = new ProblemMatcher(ToolchainCatalog.Default.Find("kotlin")!);
        var frame = Assert.Single(kotlin.Match("    at MainKt.main(Main.kt:3)"));
        Assert.Equal(("Main.kt", 3), (frame.File, frame.Line));
        Assert.Equal("demo/app/Main.kt", Assert.Single(kotlin.Match("\tat demo.app.MainKt$run$1.invoke(Main.kt:12)")).File);
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

    /// <summary>
    /// Android's sunflower, architecture-samples and JetNews built with 3, 7 and 1 errors: the Kotlin compiler's
    /// deprecation warnings (<c>w:</c>) were listed as errors, since the Android pattern dropped their severity.
    /// </summary>
    [Fact]
    public void Kotlin_warnings_of_an_Android_build_are_not_errors()
    {
        var android = new ProblemMatcher(ToolchainCatalog.Default.Find("android")!);
        string[] output =
        [
            "w: file:///p/app/build.gradle.kts:78:3 'packagingOptions(Packaging.() -> Unit): Unit' is deprecated. Renamed to packaging",
            "w: file:///p/app/src/main/java/com/example/Theme.kt:118:34 'rememberSystemUiController' is deprecated.",
            "e: file:///p/app/src/main/java/com/example/Main.kt:10:5 Unresolved reference 'foo'.",
        ];

        var error = Assert.Single(android.FindErrors(output, _ => true));

        Assert.Equal("Unresolved reference 'foo'.", error.Message);
        Assert.Equal(10, error.Location?.Line);
    }
}
