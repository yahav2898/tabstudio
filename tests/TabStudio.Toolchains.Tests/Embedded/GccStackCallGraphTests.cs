using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Analysis;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class GccStackCallGraphTests
{
    [Theory]
    [InlineData("gcc", "c", "leaf", "worker", "task")]
    [InlineData("g++", "cpp", "int app::leaf(int)", "int app::worker(int)", "int app::task(int)")]
    public async Task Fresh_real_GCC_graph_binds_direct_indirect_and_missing_library_frames(string compiler, string extension, string leaf, string worker, string task)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var temp = new TempDirectory();
        var source = "extern int external(int); static __attribute__((noinline)) int leaf(int value) { volatile int pad[4] = { value }; return pad[0]; } int (*callback)(int) = leaf; __attribute__((noinline)) int worker(int value) { return leaf(value) + external(value); } int task(int value) { return worker(value) + callback(value); }";
        if (extension == "cpp") source = "namespace app { " + source + " }";
        temp.Write("calls." + extension, source + "\n");
        var runtime = new HostRuntime();
        Assert.True(runtime.HasCommand(compiler), "Linux CI requires the native GCC/G++ compiler for the actual compiler-output proof.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var compile = await CommandRunner.RunAsync(runtime, new ProcessStartSpec
        {
            Program = compiler, Arguments = ["-O2", "-fstack-protector-all", "-fstack-usage", "-fcallgraph-info=su", "-c", "calls." + extension, "-o", "calls.o"], WorkingDirectory = temp.Path,
        }, line: null, cancellationToken: timeout.Token);
        Assert.True(compile.Succeeded, compile.Tail());
        var usage = StackAnalyzer.ParseGccStackUsage(await File.ReadAllTextAsync(temp.Resolve("calls.su"), timeout.Token));
        var graph = GccStackCallGraphReader.Parse(await File.ReadAllTextAsync(temp.Resolve("calls.ci"), timeout.Token));
        var binding = GccStackCallGraphReader.Bind(usage, [graph]);
        Assert.Contains(binding.Calls, edge => edge.Caller == worker && edge.Callee == leaf);
        Assert.Contains(binding.Calls, edge => edge.Caller == task && edge.Callee == worker);
        Assert.Contains(binding.Calls, edge => edge.Caller == task && edge.Callee is null);
        Assert.Contains(binding.Calls, edge => edge.Caller == worker && edge.Callee!.Contains("external", StringComparison.Ordinal));
        Assert.Contains(binding.Calls, edge => edge.Caller == leaf && edge.Callee!.Contains("__stack_chk_fail", StringComparison.Ordinal));
        var analysis = StackAnalyzer.Analyze(usage, binding.Calls);
        var bound = Assert.Single(analysis, entry => entry.Function == task);
        Assert.Null(bound.WorstCaseBytes);
        Assert.True(bound.KnownBytes > usage.Single(entry => entry.Function == task).Bytes);
        Assert.Contains(bound.Reasons, reason => reason.Contains("indirect", StringComparison.Ordinal));
        Assert.Contains(bound.Reasons, reason => reason.Contains("Missing stack-usage", StringComparison.Ordinal));
    }

    [Fact]
    public void Stale_frame_sizes_do_not_resolve_to_matching_name_alone()
    {
        var graph = GccStackCallGraphReader.Parse("graph: { title: \"main.c\" node: { title: \"task\" label: \"task\\nmain.c:1:1\\n16 bytes (static)\" } node: { title: \"leaf\" label: \"leaf\\nmain.c:2:1\\n32 bytes (static)\" } edge: { sourcename: \"task\" targetname: \"leaf\" } }");
        StackUsageEntry[] usage = [new("main.c", 1, 1, "task", 16, true), new("main.c", 2, 1, "leaf", 8, true)];
        var binding = GccStackCallGraphReader.Bind(usage, [graph]);
        Assert.Contains(binding.Warnings, warning => warning.Contains("leaf", StringComparison.Ordinal));
        var bound = Assert.Single(StackAnalyzer.Analyze(usage, binding.Calls), entry => entry.Function == "task");
        Assert.Null(bound.WorstCaseBytes);
        Assert.Equal(16UL, bound.KnownBytes);
    }

    [Theory]
    [InlineData("graph: { node: { title: \"a\" label: \"a\" }")]
    [InlineData("graph: { node: { title: \"a\" title: \"b\" label: \"a\" } }")]
    [InlineData("graph: { node: { title: \"a\" label: \"bad\\q\" } }")]
    [InlineData("graph: { graph: { } }")]
    [InlineData("graph: { node: { title: \"a\" label: \"a\" } } trailing")]
    public void Malformed_or_unsupported_VCG_fails_explicitly(string text) => Assert.Throws<InvalidDataException>(() => GccStackCallGraphReader.Parse(text));

    [Fact]
    public void Escaped_windows_paths_cpp_names_and_dynamic_frames_are_preserved()
    {
        var graph = GccStackCallGraphReader.Parse("graph: { node: { title: \"_ZN3App4workEv\" label: \"void App::work()\\nC:\\\\src\\\\main.cpp:12:4\\n32 bytes (dynamic,bounded)\" } }");
        var frame = Assert.Single(graph.Functions).Frame;
        Assert.NotNull(frame);
        Assert.Equal("C:\\src\\main.cpp", frame.File);
        Assert.Equal("void App::work()", frame.Function);
        Assert.True(frame.Bounded);
        Assert.Throws<InvalidDataException>(() => GccStackCallGraphReader.Parse(new string('x', 16 * 1024 * 1024 + 1)));
    }

    [Fact]
    public void A_variables_only_translation_unit_has_an_empty_valid_graph()
    {
        var graph = GccStackCallGraphReader.Parse("graph: { title: \"board_vectors.c\" }");
        Assert.Empty(graph.Functions);
        Assert.Empty(graph.Calls);
    }
}
