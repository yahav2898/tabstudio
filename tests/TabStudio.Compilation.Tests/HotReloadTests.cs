using TabStudio.Compilation.HotReload;
using TabStudio.Compilation.Running;
using Microsoft.CodeAnalysis;

namespace TabStudio.Compilation.Tests;

public sealed class HotReloadTests
{
    [Fact]
    public void Runtime_capability_probe_proves_an_existing_method_uses_the_new_IL()
    {
        Assert.True(HotReloadRuntime.VerifyApplyUpdate());
    }

    [Theory]
    [InlineData("public static int Value() => 1;", "public static int Value() => 42;")]
    [InlineData("public int Value { get { return 1; } }", "public int Value { get { return 42; } }")]
    [InlineData("public static int Value() { return 1; }", "public static int Value() { var x = 40; return x + 2; }")]
    [InlineData("public static Func<int> Value() => () => 1;", "public static Func<int> Value() => () => 42;")]
    [InlineData("public static async Task<int> Value() { await Task.Yield(); return 1; }", "public static async Task<int> Value() { await Task.Yield(); return 42; }")]
    public void Emits_metadata_IL_and_portable_PDB_deltas_for_supported_method_edits(string before, string after)
    {
        var original = Compile(before);
        using var baseline = new HotReloadBaseline(original);

        var result = baseline.EmitDifference(Compile(after).HotReloadCompilation!, out var delta);

        Assert.Equal(HotReloadStatus.Applied, result.Status);
        Assert.NotNull(delta);
        Assert.NotEmpty(delta.Metadata);
        Assert.NotEmpty(delta.IL);
        Assert.NotEmpty(delta.Pdb);
    }

    [Theory]
    [InlineData("public static int Value() => 1;", "public static long Value() => 42;")]
    [InlineData("public static int Value() => 1;", "public static int Value(int x) => x;")]
    [InlineData("public static int Value() => 1;", "public static int Changed() => 1;")]
    [InlineData("public static int Value() => 1;", "public static int Value() => 1; public int Field;")]
    [InlineData("public int Field = 1; public int Value() => Field;", "public int Field = 2; public int Value() => Field;")]
    [InlineData("public static Func<int> Value() => () => 1;", "public static Func<int> Value() => () => 1 + 2;")]
    [InlineData("public static Func<long> Value() { var x = 1; return () => x; }", "public static Func<long> Value() { var x = 1L; return () => x; }")]
    [InlineData("public static long Value() { int Local() => 1; return Local(); }", "public static long Value() { long Local() => 1; return Local(); }")]
    [InlineData("public static void Value() { Task.Run(() => 1); }", "public static void Value() { Task.Run(() => 1L); }")]
    public void Rude_edits_produce_no_delta(string before, string after)
    {
        using var baseline = new HotReloadBaseline(Compile(before));
        var result = baseline.EmitDifference(Compile(after).HotReloadCompilation!, out var delta);

        Assert.Equal(HotReloadStatus.RestartRequired, result.Status);
        Assert.Contains("restart", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(delta);
    }

    [Fact]
    public void Failed_and_abandoned_edits_do_not_advance_the_baseline()
    {
        using var baseline = new HotReloadBaseline(Compile("public static int Value() => 1;"));
        var compiler = new CSharpCompiler();
        var invalid = compiler.CreateCompilation(Request("public static int Value() => ;"));
        Assert.Equal(HotReloadStatus.CompilationErrors, baseline.EmitDifference(invalid, out _).Status);
        Assert.Equal(HotReloadStatus.RestartRequired, baseline.EmitDifference(Compile("public static long Value() => 2;").HotReloadCompilation!, out _).Status);

        Assert.True(baseline.EmitDifference(Compile("public static int Value() => 2;").HotReloadCompilation!, out var first).Success);
        Assert.True(baseline.EmitDifference(Compile("public static int Value() => 3;").HotReloadCompilation!, out var replacement).Success);
        baseline.Accept(replacement!);
        Assert.Throws<InvalidOperationException>(() => baseline.Accept(first!));
        Assert.Equal(HotReloadStatus.NoChanges, baseline.EmitDifference(Compile("public static int Value() => 3;").HotReloadCompilation!, out _).Status);
        Assert.True(baseline.EmitDifference(Compile("public static int Value() => 4;").HotReloadCompilation!, out var next).Success);
        baseline.Accept(next!);
    }

    [Fact]
    public void ApplyUpdate_changes_existing_method_and_delegate_calls_across_generations_preserving_static_state()
    {
        Assert.True(HotReloadSession.IsSupported, "Launch tests with DOTNET_MODIFIABLE_ASSEMBLIES=debug.");
        const string body = "public static int Count; public static int Value() => ++Count + OFFSET; public static Func<int> Handler() => () => OFFSET;";
        var original = Compile(body.Replace("OFFSET", "1", StringComparison.Ordinal));
        using var program = LoadedProgram.Load(original, [], [], unloadable: false);
        using var session = new HotReloadSession(original, [], program.EntryAssembly);
        var type = program.EntryAssembly.GetType("Probe")!;
        var value = type.GetMethod("Value")!;
        var handler = (Func<int>)type.GetMethod("Handler")!.Invoke(null, null)!;
        Assert.Equal(2, value.Invoke(null, null));
        Assert.Equal(1, handler());

        foreach (var offset in new[] { 10, 100 })
        {
            var updated = Compile(body.Replace("OFFSET", offset.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
            var result = session.Apply([updated]);
            Assert.Equal(HotReloadStatus.Applied, result.Status);
            Assert.Equal(offset, handler()); // This delegate was created BEFORE the update.
        }

        Assert.Equal(102, value.Invoke(null, null));
        Assert.Equal(2, type.GetField("Count")!.GetValue(null));
    }

    [Fact]
    public void Invalid_update_keeps_the_old_app_and_allows_a_corrected_edit()
    {
        var original = Compile("public static int Value() => 1;");
        using var program = LoadedProgram.Load(original, [], [], unloadable: false);
        using var session = new HotReloadSession(original, [], program.EntryAssembly);
        var method = program.EntryAssembly.GetType("Probe")!.GetMethod("Value")!;

        Assert.Equal(HotReloadStatus.RestartRequired, session.Apply([Compile("public static long Value() => 2;")]).Status);
        Assert.Equal(1, method.Invoke(null, null));
        Assert.Equal(HotReloadStatus.Applied, session.Apply([Compile("public static int Value() => 3;")]).Status);
        Assert.Equal(3, method.Invoke(null, null));
    }

    [Fact]
    public async Task A_top_level_lambda_updates_while_Main_is_waiting_without_restarting_Main()
    {
        const string source = "Func<int> read = () => 1; Console.ReadLine(); Console.WriteLine(read());";
        CompiledAssembly Build(string text) => new CSharpCompiler().Compile(new CompilationRequest
        {
            AssemblyName = "ReloadTopLevel",
            Documents = [new SourceDocument("Program.cs", text)],
        }).Assembly!;
        var original = Build(source);
        var runner = new ProgramRunner();
        HotReloadSession? session = null;
        runner.Loaded += assembly => session = new HotReloadSession(original, [], assembly);
        using var input = new ConsoleInput();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        input.WaitingForInput += (_, _) => waiting.TrySetResult();
        var console = new CapturingConsole();
        var run = runner.RunAsync(original, [], [], console, input, stop.Token);
        try
        {
            await waiting.Task.WaitAsync(stop.Token);
            Assert.NotNull(session);
            Assert.Equal(HotReloadStatus.Applied, session.Apply([Build(source.Replace("=> 1", "=> 2", StringComparison.Ordinal))]).Status);
            input.SubmitLine("continue");
            Assert.Equal(RunOutcome.Completed, (await run.WaitAsync(stop.Token)).Outcome);
            Assert.Equal("2\n", console.Output);
        }
        finally
        {
            input.Complete();
            await stop.CancelAsync();
            await run;
            session?.Dispose();
        }
    }

    private static CompilationRequest Request(string members) => new()
    {
        AssemblyName = "HotReloadProbe",
        Kind = AssemblyKind.Library,
        Documents = [new SourceDocument("Probe.cs", "public class Probe { " + members + " }")],
    };

    [Fact]
    public void Every_project_is_validated_before_any_assembly_is_updated()
    {
        var compiler = new CSharpCompiler();
        CompiledAssembly Build(string name, string source, CompiledAssembly? dependency = null)
        {
            var result = compiler.Compile(new CompilationRequest
            {
                AssemblyName = name,
                Kind = AssemblyKind.Library,
                Documents = [new SourceDocument(name + ".cs", source)],
                AdditionalReferences = dependency is null ? [] : [MetadataReference.CreateFromImage(dependency.Image)],
            });
            Assert.True(result.Success, string.Join('\n', result.Diagnostics));
            return result.Assembly!;
        }

        var dependency = Build("ReloadDependency", "public static class Dependency { public static int Value() => 1; }");
        var entry = Build("ReloadEntry", "public static class Probe { public static int Value() => Dependency.Value(); }", dependency);
        using var program = LoadedProgram.Load(entry, [dependency], [], unloadable: false);
        using var session = new HotReloadSession(entry, [dependency], program.EntryAssembly);
        var method = program.EntryAssembly.GetType("Probe")!.GetMethod("Value")!;
        Assert.Equal(1, method.Invoke(null, null));

        var rude = Build("ReloadDependency", "public static class Dependency { public static int Field; public static int Value() => 2; }");
        var edited = Build("ReloadEntry", "public static class Probe { public static int Value() => Dependency.Value() + 10; }", rude);
        Assert.Equal(HotReloadStatus.RestartRequired, session.Apply([edited, rude]).Status);
        Assert.Equal(1, method.Invoke(null, null)); // Even the valid first project's delta was not applied.

        var corrected = Build("ReloadDependency", "public static class Dependency { public static int Value() => 2; }");
        Assert.Equal(HotReloadStatus.Applied, session.Apply([edited, corrected]).Status);
        Assert.Equal(12, method.Invoke(null, null));
    }

    [Fact]
    public void Metadata_update_handlers_clear_caches_before_refreshing_the_application()
    {
        const string source = """
            [assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Handler))]
            public static class Handler
            {
                public static string Calls = "";
                public static void ClearCache(Type[]? types) { Calls += "clear;"; }
                public static void UpdateApplication(Type[]? types) { Calls += "update:" + Probe.Value(); }
            }
            public static class Probe { public static int Value() => 1; }
            """;
        var compiler = new CSharpCompiler();
        CompiledAssembly Build(string text) => compiler.Compile(new CompilationRequest
        {
            AssemblyName = "ReloadHandlers",
            Kind = AssemblyKind.Library,
            Documents = [new SourceDocument("Handlers.cs", text)],
        }).Assembly!;
        var original = Build(source);
        using var program = LoadedProgram.Load(original, [], [], unloadable: false);
        using var session = new HotReloadSession(original, [], program.EntryAssembly);
        var result = session.Apply([Build(source.Replace("=> 1", "=> 2", StringComparison.Ordinal))]);
        Assert.Equal(HotReloadStatus.Applied, result.Status);
        Assert.Equal("clear;update:2", program.EntryAssembly.GetType("Handler")!.GetField("Calls")!.GetValue(null));
    }

    [Fact]
    public void Stopping_the_run_during_a_framework_update_does_not_deadlock()
    {
        const string callbackKey = "TabStudio.HotReload.Tests.StopCallback";
        const string source = """
            [assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Handler))]
            public static class Handler
            {
                public static void UpdateApplication(Type[]? types)
                {
                    ((Action)AppContext.GetData("TabStudio.HotReload.Tests.StopCallback")!)();
                }
            }
            public static class Probe { public static int Value() => 1; }
            """;
        CompiledAssembly Build(string text) => new CSharpCompiler().Compile(new CompilationRequest
        {
            AssemblyName = "ReloadStopCallback",
            Kind = AssemblyKind.Library,
            Documents = [new SourceDocument("Stop.cs", text)],
        }).Assembly!;
        var original = Build(source);
        using var program = LoadedProgram.Load(original, [], [], unloadable: false);
        using var session = new HotReloadSession(original, [], program.EntryAssembly);
        var stopped = false;
        AppContext.SetData(callbackKey, (Action)(() => stopped = Task.Run(session.Dispose).Wait(TimeSpan.FromSeconds(3))));
        try
        {
            Assert.Equal(HotReloadStatus.Applied, session.Apply([Build(source.Replace("=> 1", "=> 2", StringComparison.Ordinal))]).Status);
            Assert.True(stopped, "A framework callback must not hold a lock that Stop needs on another thread.");
        }
        finally
        {
            AppContext.SetData(callbackKey, null);
        }
    }

    private static CompiledAssembly Compile(string members)
    {
        var result = new CSharpCompiler().Compile(Request(members));
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return result.Assembly!;
    }
}
