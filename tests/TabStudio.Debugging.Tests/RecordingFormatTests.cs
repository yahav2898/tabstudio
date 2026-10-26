using System.Text;
using TabStudio.Debugging.Engine;
using TabStudio.Debugging.Recording;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging.Tests;

public sealed class RecordingFormatTests
{
    [Fact]
    public void Version_one_binary_record_has_a_stable_header_and_round_trips_scalar_values_and_frames()
    {
        var info = new DebugInfoBuilder();
        var location = new DebugSourceSpan("Program.cs", 2, 1, 2, 10, 8, 9);
        var method = info.AddMethod("Calculate(int)", location, []);
        DebugLocalSlot[] slots = [new("count", "int"), new("text", "string"), new("flag", "bool"), new("empty", "object")];
        var statement = info.AddStatement(method, location, slots);
        var frame = new DebugFrame(method, null) { StatementId = statement };
        var recording = new DebugRecording(info.Build(), new RecordingOptions { CapacityBytes = 1024 });
        recording.Record(frame, slots, [42, "hello\nworld", true, null], breakpoint: true);

        var bytes = recording.CopyEncodedStep(0);
        using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
        Assert.Equal(DebugRecording.FormatVersion, reader.ReadByte());
        Assert.Equal(1, reader.ReadInt64());
        Assert.Equal(Environment.CurrentManagedThreadId, reader.Read7BitEncodedInt());
        Assert.True(reader.ReadBoolean());
        Assert.Equal(string.Empty, reader.ReadString());
        Assert.Equal(1, reader.Read7BitEncodedInt());
        Assert.Equal(frame.Id, reader.ReadInt64());
        Assert.Equal(method, reader.Read7BitEncodedInt());
        Assert.Equal(statement, reader.Read7BitEncodedInt());
        Assert.Equal(4, reader.Read7BitEncodedInt());
        var step = recording.GetStep(0)!;
        Assert.True(step.IsBreakpoint);
        Assert.Equal(location, step.Location);
        Assert.Equal(new[] { "42", "\"hello\\nworld\"", "true", "null" }, step.Frames[0].Locals.Select(l => l.Value));
        Assert.All(step.Frames[0].Locals, l => Assert.Null(l.Object));
        Assert.Equal(bytes.Length, recording.UsedBytes);
    }

    [Fact]
    public void Unknown_tiny_capacities_are_rejected_before_any_hooks_run()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DebugRecording(DebugInfoMap.Empty, new RecordingOptions { CapacityBytes = 100 }));
    }
}
