using System.Text;
using System.Text.Json.Nodes;
using TabStudio.Link.Language;
using TabStudio.Testing;
using TabStudio.WindowsBridge.Builds;
using TabStudio.WindowsBridge.Debugging;

namespace TabStudio.WindowsBridge.Tests;

/// <summary>The debugger's tunnel: messages are cut from any pieces of the stream, and their paths are rewritten both ways.</summary>
public sealed class DebugTunnelTests : IDisposable
{
    private const string TabletRoot = "/storage/emulated/0/TabStudio/Projects/Hello";

    private readonly TempDirectory _mirror = new();

    [Fact]
    public void Messages_are_cut_from_a_stream_that_arrives_in_pieces()
    {
        var first = DapMessageFramer.Frame("{\"seq\":1}"u8);
        var second = DapMessageFramer.Frame("{\"seq\":2,\"type\":\"event\"}"u8);
        var stream = first.Concat(second).ToArray();
        var framer = new DapMessageFramer();

        var messages = new List<string>();
        for (var i = 0; i < stream.Length; i += 7)
        {
            messages.AddRange(framer.Append(stream.AsSpan(i, Math.Min(7, stream.Length - i))).Select(m => Encoding.UTF8.GetString(m)));
        }

        Assert.Equal(["{\"seq\":1}", "{\"seq\":2,\"type\":\"event\"}"], messages);
    }

    [Fact]
    public void A_stream_without_lengths_is_refused()
    {
        var framer = new DapMessageFramer();

        Assert.Throws<InvalidDataException>(() => framer.Append("Content-Type: json\r\n\r\n{}"u8));
    }

    [Fact]
    public void Breakpoints_and_the_program_get_the_mirrors_paths()
    {
        var rewriter = Rewriter();
        var request = $$$"""
            {"seq":3,"type":"request","command":"launch","arguments":{"program":"{{{TabletRoot}}}/App/bin/Debug/App.dll","cwd":"{{{TabletRoot}}}/App","args":["{{{TabletRoot}}}/data.txt"]}}
            """;
        var breakpoints = $$$"""
            {"seq":4,"type":"request","command":"setBreakpoints","arguments":{"source":{"path":"{{{TabletRoot}}}/App/MainWindow.xaml.cs"},"breakpoints":[{"line":12}]}}
            """;

        var (launch, command) = rewriter.ToAdapter(Encoding.UTF8.GetBytes(request));
        var (set, _) = rewriter.ToAdapter(Encoding.UTF8.GetBytes(breakpoints));
        var launchJson = JsonNode.Parse(launch)!;
        var setJson = JsonNode.Parse(set)!;

        Assert.Equal("launch", command);
        Assert.Equal(_mirror.Resolve("App/bin/Debug/App.dll"), (string?)launchJson["arguments"]!["program"]);
        Assert.Equal(_mirror.Resolve("App"), (string?)launchJson["arguments"]!["cwd"]);
        Assert.Equal($"{TabletRoot}/data.txt", (string?)launchJson["arguments"]!["args"]![0]);
        Assert.Equal(_mirror.Resolve("App/MainWindow.xaml.cs"), (string?)setJson["arguments"]!["source"]!["path"]);
        Assert.Equal(12, (int?)setJson["arguments"]!["breakpoints"]![0]!["line"]);
    }

    [Fact]
    public void Stack_frames_and_output_get_the_tablets_paths()
    {
        var rewriter = Rewriter();
        var file = _mirror.Resolve("App/MainWindow.xaml.cs");
        var frames = new JsonObject
        {
            ["seq"] = 9,
            ["type"] = "response",
            ["command"] = "stackTrace",
            ["body"] = new JsonObject
            {
                ["stackFrames"] = new JsonArray(new JsonObject { ["name"] = "App.MainWindow.OnClick", ["line"] = 12, ["source"] = new JsonObject { ["path"] = file } }),
            },
        };
        var output = new JsonObject
        {
            ["type"] = "event",
            ["event"] = "output",
            ["body"] = new JsonObject { ["category"] = "stderr", ["output"] = $"   at App.MainWindow.OnClick() in {file}:line 12\n" },
        };

        var (stack, _) = rewriter.ToTablet(Encoding.UTF8.GetBytes(frames.ToJsonString()));
        var (text, name) = rewriter.ToTablet(Encoding.UTF8.GetBytes(output.ToJsonString()));

        Assert.Equal($"{TabletRoot}/App/MainWindow.xaml.cs", (string?)JsonNode.Parse(stack)!["body"]!["stackFrames"]![0]!["source"]!["path"]);
        Assert.Equal("output", name);
        Assert.Equal($"   at App.MainWindow.OnClick() in {TabletRoot}/App/MainWindow.xaml.cs:line 12\n", (string?)JsonNode.Parse(text)!["body"]!["output"]);
    }

    [Fact]
    public void Messages_that_are_not_json_pass_through()
    {
        var bytes = Encoding.UTF8.GetBytes("not json");

        Assert.Same(bytes, Rewriter().ToTablet(bytes).Json);
    }

    public void Dispose() => _mirror.Dispose();

    private DapPathRewriter Rewriter() =>
        new(new BridgePathMapper(new LinkPathMap(TabletRoot, '/', _mirror.Path), hostIgnoresCase: OperatingSystem.IsWindows()));
}
