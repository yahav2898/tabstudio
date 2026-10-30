using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>Output is colored by what each line says, per toolchain, never by the stream it came on.</summary>
public sealed class OutputClassifierTests
{
    [Fact]
    public void Cargo_progress_on_standard_error_is_ordinary_and_its_diagnostics_are_warnings_and_errors()
    {
        var kinds = Classify("rust", """
               Compiling memchr v2.7.4
               Compiling fd-find v10.3.0 (/projects/fd)
            warning: unused variable: `x`
             --> src/main.rs:2:9
              |
            2 |     let x = 1;
              |         ^ help: if this is intentional, prefix it with an underscore: `_x`
              |
              = note: `#[warn(unused_variables)]` on by default

            error[E0308]: mismatched types
             --> src/walk.rs:10:5
               |
            10 |     "text"
               |     ^^^^^^ expected `u32`, found `&str`

            error: could not compile `fd-find` (bin "fd") due to 1 previous error
                Finished `release` profile [optimized] target(s) in 1m 52s
            """);

        Assert.Equal(OutputLineKind.Normal, kinds["   Compiling memchr v2.7.4"]);
        Assert.Equal(OutputLineKind.Normal, kinds["   Compiling fd-find v10.3.0 (/projects/fd)"]);
        Assert.Equal(OutputLineKind.Warning, kinds["warning: unused variable: `x`"]);
        Assert.Equal(OutputLineKind.Warning, kinds[" --> src/main.rs:2:9"]);
        Assert.Equal(OutputLineKind.Warning, kinds["  = note: `#[warn(unused_variables)]` on by default"]);
        Assert.Equal(OutputLineKind.Error, kinds["error[E0308]: mismatched types"]);
        Assert.Equal(OutputLineKind.Error, kinds["10 |     \"text\""]);
        Assert.Equal(OutputLineKind.Error, kinds["error: could not compile `fd-find` (bin \"fd\") due to 1 previous error"]);
        Assert.Equal(OutputLineKind.Normal, kinds["    Finished `release` profile [optimized] target(s) in 1m 52s"]);
    }

    [Fact]
    public void A_panic_with_the_thread_id_newer_Rust_prints_is_an_error_too()
    {
        var kinds = Classify("rust", """
            thread 'main' (29844) panicked at src/main.rs:3:21:
            index out of bounds: the len is 0 but the index is 3
            done
            """);

        Assert.Equal(OutputLineKind.Error, kinds["thread 'main' (29844) panicked at src/main.rs:3:21:"]);
        Assert.Equal(OutputLineKind.Error, kinds["index out of bounds: the len is 0 but the index is 3"]);
    }

    [Fact]
    public void A_Rust_panic_is_an_error_up_to_its_backtrace_note()
    {
        var kinds = Classify("rust", """
            Searching…
            thread 'main' panicked at src/main.rs:4:5:
            index out of bounds: the len is 3 but the index is 7
            note: run with `RUST_BACKTRACE=1` environment variable to display a backtrace
            done
            """);

        Assert.Equal(OutputLineKind.Normal, kinds["Searching…"]);
        Assert.Equal(OutputLineKind.Error, kinds["thread 'main' panicked at src/main.rs:4:5:"]);
        Assert.Equal(OutputLineKind.Error, kinds["index out of bounds: the len is 3 but the index is 7"]);
        Assert.Equal(OutputLineKind.Error, kinds["note: run with `RUST_BACKTRACE=1` environment variable to display a backtrace"]);
        Assert.Equal(OutputLineKind.Normal, kinds["done"]);
    }

    [Fact]
    public void A_Python_traceback_is_an_error_through_its_exception_line_and_warnings_are_warnings()
    {
        var kinds = Classify("python", """
             * Serving Flask app 'microblog.py'
            /app/models.py:12: DeprecationWarning: datetime.utcnow() is deprecated
            Traceback (most recent call last):
              File "/app/main.py", line 3, in <module>
                print(1 / 0)
                      ~~^~~
            ZeroDivisionError: division by zero
            Still here
            """);

        Assert.Equal(OutputLineKind.Normal, kinds[" * Serving Flask app 'microblog.py'"]);
        Assert.Equal(OutputLineKind.Warning, kinds["/app/models.py:12: DeprecationWarning: datetime.utcnow() is deprecated"]);
        Assert.Equal(OutputLineKind.Error, kinds["Traceback (most recent call last):"]);
        Assert.Equal(OutputLineKind.Error, kinds["  File \"/app/main.py\", line 3, in <module>"]);
        Assert.Equal(OutputLineKind.Error, kinds["ZeroDivisionError: division by zero"]);
        Assert.Equal(OutputLineKind.Normal, kinds["Still here"]);
    }

    [Fact]
    public void A_Python_syntax_error_without_a_traceback_header_is_an_error()
    {
        var kinds = Classify("python", """
              File "/app/main.py", line 1
                print("x"
                     ^
            SyntaxError: '(' was never closed
            """);

        Assert.All(kinds.Values, kind => Assert.Equal(OutputLineKind.Error, kind));
    }

    [Fact]
    public void Java_exceptions_Spring_log_levels_and_Maven_levels_are_classified()
    {
        var kinds = Classify("java", """
            [INFO] Building petclinic 4.0.0-SNAPSHOT
            [WARNING] The requested profile "x" could not be activated
            [ERROR] Failed to execute goal on project petclinic
            2026-10-27T01:28:00.123+02:00  INFO 4242 --- [petclinic] [           main] o.s.s.petclinic.PetClinicApplication     : Started PetClinicApplication in 9.1 seconds
            2026-10-27T01:28:01.000+02:00  WARN 4242 --- [petclinic] [           main] o.h.e.j.e.i.JdbcEnvironmentInitiator     : HHH000342
            2026-10-27T01:28:02.000+02:00 ERROR 4242 --- [petclinic] [           main] o.s.boot.SpringApplication               : Application run failed
            Exception in thread "main" java.lang.IllegalStateException: no owner
            	at org.springframework.samples.petclinic.Owner.find(Owner.java:42)
            Caused by: java.io.IOException: closed
            	... 3 more
            after
            """);

        Assert.Equal(OutputLineKind.Normal, kinds["[INFO] Building petclinic 4.0.0-SNAPSHOT"]);
        Assert.Equal(OutputLineKind.Warning, kinds["[WARNING] The requested profile \"x\" could not be activated"]);
        Assert.Equal(OutputLineKind.Error, kinds["[ERROR] Failed to execute goal on project petclinic"]);
        Assert.Equal(OutputLineKind.Normal, kinds.Single(k => k.Key.Contains("Started PetClinicApplication", StringComparison.Ordinal)).Value);
        Assert.Equal(OutputLineKind.Warning, kinds.Single(k => k.Key.Contains("HHH000342", StringComparison.Ordinal)).Value);
        Assert.Equal(OutputLineKind.Error, kinds.Single(k => k.Key.Contains("Application run failed", StringComparison.Ordinal)).Value);
        Assert.Equal(OutputLineKind.Error, kinds["Exception in thread \"main\" java.lang.IllegalStateException: no owner"]);
        Assert.Equal(OutputLineKind.Error, kinds["\tat org.springframework.samples.petclinic.Owner.find(Owner.java:42)"]);
        Assert.Equal(OutputLineKind.Error, kinds["Caused by: java.io.IOException: closed"]);
        Assert.Equal(OutputLineKind.Error, kinds["\t... 3 more"]);
        Assert.Equal(OutputLineKind.Normal, kinds["after"]);
    }

    [Fact]
    public void Go_compiler_errors_and_panics_are_errors_only_for_Go()
    {
        var go = Classify("go", """
            # example.com/app
            ./main.go:5:2: undefined: fmt.Printn
            panic: runtime error: index out of range [3] with length 1

            goroutine 1 [running]:
            main.main()
            	/app/main.go:9 +0x1d
            exit status 2
            """);

        Assert.Equal(OutputLineKind.Normal, go["# example.com/app"]);
        Assert.Equal(OutputLineKind.Error, go["./main.go:5:2: undefined: fmt.Printn"]);
        Assert.Equal(OutputLineKind.Error, go["panic: runtime error: index out of range [3] with length 1"]);
        Assert.Equal(OutputLineKind.Error, go["goroutine 1 [running]:"]);
        Assert.Equal(OutputLineKind.Error, go["main.main()"]);
        Assert.Equal(OutputLineKind.Normal, go["exit status 2"]);

        // A Python program's own "x.go:1:2: text" is ordinary output.
        Assert.Equal(OutputLineKind.Normal, Classify("python", "notes.go:1:2: a line from a file\n")["notes.go:1:2: a line from a file"]);
    }

    [Fact]
    public void Node_errors_npm_and_pip_levels_and_C_compiler_diagnostics_are_classified()
    {
        var node = Classify("node", """
            Listening on 3000
            TypeError: Cannot read properties of undefined (reading 'x')
                at Object.<anonymous> (/app/index.js:3:9)
            npm warn deprecated inflight@1.0.6
            npm error code ELIFECYCLE
            """);
        Assert.Equal(OutputLineKind.Normal, node["Listening on 3000"]);
        Assert.Equal(OutputLineKind.Error, node["TypeError: Cannot read properties of undefined (reading 'x')"]);
        Assert.Equal(OutputLineKind.Error, node["    at Object.<anonymous> (/app/index.js:3:9)"]);
        Assert.Equal(OutputLineKind.Warning, node["npm warn deprecated inflight@1.0.6"]);
        Assert.Equal(OutputLineKind.Error, node["npm error code ELIFECYCLE"]);

        var pip = Classify("python", """
            Collecting psycopg2==2.9.9
              error: subprocess-exited-with-error
              × Getting requirements to build wheel did not run successfully.
            WARNING: Retrying (Retry(total=4)) after connection broken
            ERROR: Failed to build 'psycopg2' when getting requirements to build wheel
            """);
        Assert.Equal(OutputLineKind.Normal, pip["Collecting psycopg2==2.9.9"]);
        Assert.Equal(OutputLineKind.Error, pip["  error: subprocess-exited-with-error"]);
        Assert.Equal(OutputLineKind.Error, pip["  × Getting requirements to build wheel did not run successfully."]);
        Assert.Equal(OutputLineKind.Warning, pip["WARNING: Retrying (Retry(total=4)) after connection broken"]);
        Assert.Equal(OutputLineKind.Error, pip["ERROR: Failed to build 'psycopg2' when getting requirements to build wheel"]);

        var cpp = Classify("cpp", """
            [ 50%] Building C object CMakeFiles/app.dir/main.c.o
            /app/main.c:3:5: warning: implicit declaration of function 'puts'
                3 |     puts("hi");
                  |     ^~~~
            /app/main.c:4:1: error: expected ';' before '}' token
            make[2]: *** [CMakeFiles/app.dir/build.make:76: main.c.o] Error 1
            """);
        Assert.Equal(OutputLineKind.Normal, cpp["[ 50%] Building C object CMakeFiles/app.dir/main.c.o"]);
        Assert.Equal(OutputLineKind.Warning, cpp["/app/main.c:3:5: warning: implicit declaration of function 'puts'"]);
        Assert.Equal(OutputLineKind.Warning, cpp["    3 |     puts(\"hi\");"]);
        Assert.Equal(OutputLineKind.Error, cpp["/app/main.c:4:1: error: expected ';' before '}' token"]);
        Assert.Equal(OutputLineKind.Error, cpp["make[2]: *** [CMakeFiles/app.dir/build.make:76: main.c.o] Error 1"]);
    }

    [Fact]
    public void Ordinary_text_on_standard_error_stays_ordinary()
    {
        var kinds = Classify(null, "Loading the index\nno errors found\nWarnings are disabled\n");

        Assert.All(kinds.Values, kind => Assert.Equal(OutputLineKind.Normal, kind));
    }

    [Fact]
    public void A_stream_split_in_any_pieces_is_classified_by_its_whole_lines()
    {
        var stream = new StreamClassifier("rust");
        var pieces = new List<ClassifiedText>();
        foreach (var chunk in new[] { "   Compi", "ling fd v1\nerr", "or[E0308]: mismatched\n --> src/a.rs:1:1\n", "\nok\nEnter a value: " })
        {
            pieces.AddRange(stream.Feed(chunk));
        }

        Assert.Equal("   Compiling fd v1\nerror[E0308]: mismatched\n --> src/a.rs:1:1\n\nok\nEnter a value: ", string.Concat(pieces.Select(p => p.Text)));
        Assert.Equal(OutputLineKind.Normal, pieces[0].Kind);
        Assert.Equal(OutputLineKind.Error, pieces.Single(p => p.Text.Contains("mismatched", StringComparison.Ordinal)).Kind);
        Assert.Equal(OutputLineKind.Normal, pieces[^1].Kind);
        Assert.EndsWith("Enter a value: ", pieces[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Peeking_at_an_unfinished_line_does_not_change_the_block_it_is_in()
    {
        var classifier = new OutputClassifier("python");
        Assert.Equal(OutputLineKind.Error, classifier.Classify("Traceback (most recent call last):"));
        Assert.Equal(OutputLineKind.Error, classifier.Peek("ValueEr"));
        Assert.Equal(OutputLineKind.Error, classifier.Classify("  File \"a.py\", line 1, in <module>"));
        Assert.Equal(OutputLineKind.Error, classifier.Classify("ValueError: bad"));
        Assert.Equal(OutputLineKind.Normal, classifier.Classify("next"));
    }

    private static Dictionary<string, OutputLineKind> Classify(string? toolchain, string text)
    {
        var classifier = new OutputClassifier(toolchain);
        var result = new Dictionary<string, OutputLineKind>(StringComparer.Ordinal);
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n'))
        {
            var kind = classifier.Classify(line);
            if (line.Length > 0)
            {
                result.TryAdd(line, kind);
            }
        }

        return result;
    }
}
