using System.Text.RegularExpressions;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Compilation.VisualBasic;
using TabStudio.Debugging.Building;

namespace TabStudio.Debugging.Tests;

/// <summary>The Visual Basic instrumenter keeps programs compiling, behaving and reporting the same line numbers.</summary>
public sealed partial class VisualBasicInstrumentationTests
{
    private const string Throwing = """
        Module Program
            Sub Main()
                Run()
            End Sub

            Sub Run()
                Dim values = New Integer() {1, 2}
                Helper(values)
            End Sub

            Sub Helper(values As Integer())
                Console.WriteLine(values.Length)
                Throw New InvalidOperationException("boom")
            End Sub
        End Module
        """;

    /// <summary>A sweep of Visual Basic constructs the instrumenter has to keep compiling and behaving the same.</summary>
    private const string Constructs = """
        Module Program
            Sub Main()
                Dim numbers As New List(Of Integer) From {3, 1, 2}
                For Each n In numbers
                    If n > 1 Then Console.Write(n) Else Console.Write("-")
                Next
                Console.WriteLine()

                Dim i = 0
                While i < 3
                    i += 1
                End While
                Do
                    i -= 1
                Loop While i > 0
                Do Until i >= 2
                    i += 1
                Loop
                Console.WriteLine(i)

                Select Case i
                    Case 1 : Console.WriteLine("one")
                    Case 2
                        Console.WriteLine("two")
                    Case Else
                        Console.WriteLine("other")
                End Select

                If i = 0 Then
                    Console.WriteLine("zero")
                ElseIf i = 2 Then
                    Console.WriteLine("two again")
                Else
                    Console.WriteLine("else")
                End If

                Try
                    Throw New InvalidOperationException("caught")
                Catch e As InvalidOperationException When e.Message = "caught"
                    Console.WriteLine(e.Message)
                Finally
                    Console.WriteLine("finally")
                End Try

                Using reader As New System.IO.StringReader("line")
                    Console.WriteLine(reader.ReadLine())
                End Using

                SyncLock numbers
                    Console.WriteLine("locked")
                End SyncLock

                Dim holder As New Crate()
                With holder
                    .Value = 5
                    Console.WriteLine(.Value)
                End With

                Dim twice As Func(Of Integer, Integer) = Function(x As Integer)
                                                             Dim y = x * 2
                                                             Return y
                                                         End Function
                Dim square As Func(Of Integer, Integer) = Function(x) x * x
                Dim show As Action(Of String) = Sub(s) Console.WriteLine(s)
                show($"{twice(4)} {square(3)}")

                Console.WriteLine(String.Join(",", Up(3)))
                Console.WriteLine(holder.Doubled)
                holder.Doubled = 4
                Console.WriteLine(holder.Value)
                Console.WriteLine(New Money(5) + New Money(6))

                For k = 1 To 3 Step 2
                    Console.Write(k)
                Next
                Console.WriteLine()

                Dim j = 0
        again:
                j += 1
                If j < 2 Then GoTo again
                Console.WriteLine(j)
            End Sub

            Iterator Function Up(count As Integer) As IEnumerable(Of Integer)
                For i = 1 To count
                    Yield i
                Next
            End Function
        End Module

        Class Crate
            Public Property Value As Integer

            Public Property Doubled As Integer
                Get
                    Return Value * 2
                End Get
                Set(value As Integer)
                    Me.Value = value \ 2
                End Set
            End Property
        End Class

        Structure Money
            Public ReadOnly Cents As Integer

            Public Sub New(cents As Integer)
                Me.Cents = cents
            End Sub

            Public Shared Operator +(a As Money, b As Money) As Money
                Return New Money(a.Cents + b.Cents)
            End Operator

            Public Overrides Function ToString() As String
                Return "Money " & Cents
            End Function
        End Structure

        Class Base
            Protected ReadOnly Name As String

            Public Sub New(name As String)
                Me.Name = name
            End Sub
        End Class

        Class Derived
            Inherits Base

            Public Sub New()
                MyBase.New("derived")
                Console.WriteLine(Name)
            End Sub
        End Class
        """;

    private const string ConstructsOutput = """
        3-2
        2
        two
        two again
        caught
        finally
        line
        locked
        5
        8 9
        1,2,3
        10
        2
        Money 11
        13
        2

        """;

    [Fact]
    public async Task A_program_without_a_debugger_attached_runs_normally()
    {
        var build = DebuggedVisualBasicProgram.Compile(Constructs);
        var console = new CapturingConsole();

        var result = await new ProgramRunner().RunAsync(build.Result.EntryAssembly!, [], [], console);

        Assert.True(result.Outcome == RunOutcome.Completed, console.Error + result.Exception);
        Assert.Equal(ConstructsOutput, console.Output);
        Assert.Equal(ConstructsOutput, await DebuggedVisualBasicProgram.RunPlainAsync(Constructs));
    }

    [Fact]
    public async Task Stepping_through_every_statement_keeps_the_program_behavior()
    {
        using var program = DebuggedVisualBasicProgram.Create(Constructs, 3);
        program.Start();

        var steps = 0;
        var lines = new HashSet<int>();
        while (await program.TryNextPauseAsync() is { } pause)
        {
            // Reading every value runs each hook's locals through the inspector.
            foreach (var local in pause.Locals)
            {
                Assert.NotNull(local.Value);
                Assert.NotNull(local.Type);
            }

            lines.Add(pause.Location?.Line ?? 0);
            Assert.True(++steps < 5000, "Stepping did not reach the end of the program.");
            program.Session.StepInto();
        }

        var result = await program.FinishAsync();
        Assert.True(result.Outcome == RunOutcome.Completed, program.Console.Error + result.Exception);
        Assert.Equal(ConstructsOutput, program.Console.Output);
        Assert.True(lines.Count > 40, $"Only {lines.Count} distinct lines were stepped through.");
    }

    [Fact]
    public async Task A_constructor_that_calls_its_base_constructor_is_debugged_after_that_call()
    {
        using var program = DebuggedVisualBasicProgram.Create("""
            Module Program
                Sub Main()
                    Dim item As New Derived()
                End Sub
            End Module

            Class Base
                Protected ReadOnly Name As String

                Public Sub New(name As String)
                    Me.Name = name
                End Sub
            End Class

            Class Derived
                Inherits Base

                Public Sub New()
                    MyBase.New("derived")
                    Console.WriteLine(Name)
                End Sub
            End Class
            """, 20);
        program.Start();

        var pause = await program.NextPauseAsync();

        Assert.Equal(20, pause.Location?.Line);
        Assert.Equal(new[] { "Derived.New()", "Program.Main()" }, pause.CallStack.Select(f => f.MethodName));
        Assert.Equal("Derived", DebuggedProgram.Local(pause, "Me").Type);
        program.Session.Continue();

        await program.FinishAsync();
        Assert.Equal("derived\n", program.Console.Output);
    }

    [Fact]
    public async Task Instrumentation_keeps_the_line_numbers_of_exception_stack_traces()
    {
        var original = DebuggedVisualBasicProgram.CompilePlain(Throwing);
        var instrumented = DebuggedVisualBasicProgram.Compile(Throwing);

        var plainRun = await new ProgramRunner().RunAsync(original, [], [], new CapturingConsole());
        var debugRun = await new ProgramRunner().RunAsync(instrumented.Result.EntryAssembly!, [], [], new CapturingConsole());

        Assert.Equal(new[] { 13, 8, 3 }, StackLines(plainRun.Exception));
        Assert.Equal(StackLines(plainRun.Exception), StackLines(debugRun.Exception));
    }

    [Fact]
    public void Statements_are_mapped_to_their_original_positions()
    {
        var build = DebuggedVisualBasicProgram.Compile("""
            Module Program
                Sub Main()
                    Dim x = 1
                    If x > 0 Then
                        Console.WriteLine(x)
                    End If
                    If x > 1 Then Console.WriteLine("big")
                End Sub
            End Module
            """);

        var statements = build.DebugInfo.GetStatements(DebuggedVisualBasicProgram.FilePath);

        Assert.Equal(new[] { 3, 4, 5, 7, 7 }, statements.Select(s => s.Location.Line));
        Assert.Equal("If x > 0 Then".Length, statements[1].Location.Length);
        Assert.Equal(13, statements[2].Location.Column);
        Assert.Contains(statements[2].Locals, l => l.Name == "x" && l.Type == "Integer");
        Assert.Equal("If x > 1 Then".Length, statements[3].Location.Length);
        Assert.Equal("Program.Main()", Assert.Single(build.DebugInfo.Methods).Name);
    }

    [Fact]
    public void Generated_files_and_the_debugger_runtime_are_not_instrumented()
    {
        var compilation = VisualBasicCompiler.CreateCompilation(new CompilationRequest
        {
            AssemblyName = "Generated",
            Documents =
            [
                new SourceDocument(DebuggedVisualBasicProgram.FilePath, """
                    Module Program
                        Sub Main()
                            Console.WriteLine(Helper.Value() + Marked.Value())
                        End Sub
                    End Module
                    """),
                new SourceDocument("obj/Helper.g.vb", "Module Helper\n    Function Value() As Integer\n        Return 1\n    End Function\nEnd Module\n"),
                new SourceDocument("Marked.vb", "' <auto-generated/>\nModule Marked\n    Function Value() As Integer\n        Return 2\n    End Function\nEnd Module\n"),
            ],
        });

        var build = DebugBuilder.Build(compilation);

        Assert.True(build.Success, string.Join('\n', build.Result.Diagnostics));
        Assert.All(build.DebugInfo.Statements, s => Assert.Equal(DebuggedVisualBasicProgram.FilePath, s.Location.FilePath));
        Assert.Single(build.DebugInfo.Statements);
        Assert.DoesNotContain(build.DebugInfo.Methods, m => m.Name.Contains("Value", StringComparison.Ordinal));
    }

    [Fact]
    public void The_in_process_safety_check_still_rejects_end()
    {
        var compilation = VisualBasicCompiler.CreateCompilation(new CompilationRequest
        {
            AssemblyName = "Ends",
            Documents = [new SourceDocument(DebuggedVisualBasicProgram.FilePath, "Module Program\n    Sub Main()\n        End\n    End Sub\nEnd Module\n")],
        });

        var build = DebugBuilder.Build(compilation);

        Assert.False(build.Success);
        Assert.Contains(build.Result.Diagnostics, d => d.Id == InProcessSafetyAnalyzer.ExitId);
    }

    [Fact]
    public async Task Option_strict_on_and_unstructured_error_handling_still_compile()
    {
        using var program = DebuggedVisualBasicProgram.Create("""
            Option Strict On

            Module Program
                Sub Main()
                    Dim text As String = Nothing
                    Console.WriteLine(Recover())
                    Console.WriteLine(If(text, "none"))
                End Sub

                Function Recover() As Integer
                    On Error GoTo Failed
                    Dim zero = 0
                    Return 1 \ zero
            Failed:
                    Return -1
                End Function
            End Module
            """, 7);
        program.Start();

        var pause = await program.NextPauseAsync();
        Assert.Equal("Nothing", DebuggedProgram.Local(pause, "text").Value);
        Assert.DoesNotContain(program.Build.DebugInfo.Methods, m => m.Name.StartsWith("Program.Recover", StringComparison.Ordinal));
        program.Session.Continue();

        var result = await program.FinishAsync();
        Assert.True(result.Outcome == RunOutcome.Completed, program.Console.Error + result.Exception);
        Assert.Equal("-1\nnone\n", program.Console.Output);
    }

    private static int[] StackLines(Exception? exception)
    {
        Assert.NotNull(exception);
        return LinePattern().Matches(exception.StackTrace ?? string.Empty)
            .Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
    }

    [GeneratedRegex(@"Program\.vb:line (\d+)")]
    private static partial Regex LinePattern();
}
