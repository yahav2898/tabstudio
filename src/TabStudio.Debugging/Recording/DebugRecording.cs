using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using TabStudio.Debugging.Engine;
using TabStudio.Debugging.Inspection;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging.Recording;

/// <summary>
/// A byte-bounded binary ring of independent shallow snapshots. It retains neither debuggee objects nor assemblies.
/// All operations serialize under one lock, establishing a total order across recorded threads.
/// </summary>
public sealed class DebugRecording
{
    internal const byte FormatVersion = 1;
    private readonly object _gate = new();
    private readonly byte[] _ring;
    private readonly DebugInfoMap _map;
    private readonly List<Entry> _entries = [];
    private readonly ConditionalWeakTable<object, Identity> _identities = new();
    private static readonly ValueInspector ScalarInspector = new(new Evaluator(TimeSpan.FromSeconds(1)));
    private long _nextIdentity;
    private long _sequence;
    private int _first;
    private int _tail;
    private int _used;
    private long _evicted;
    private long _dropped;

    internal DebugRecording(DebugInfoMap map, RecordingOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.CapacityBytes, 1024);
        _ring = new byte[options.CapacityBytes];
        _map = map;
    }

    /// <summary>Encoded capacity, independent of the number of events.</summary>
    public int CapacityBytes => _ring.Length;

    /// <summary>Bytes occupied by complete retained records.</summary>
    public int UsedBytes { get { lock (_gate) return _used; } }

    /// <summary>Number of retained steps.</summary>
    public int Count { get { lock (_gate) return _entries.Count - _first; } }

    /// <summary>Complete steps evicted when the ring wrapped.</summary>
    public long EvictedSteps { get { lock (_gate) return _evicted; } }

    /// <summary>Steps too large to fit in the configured ring.</summary>
    public long DroppedSteps { get { lock (_gate) return _dropped; } }

    /// <summary>The last write implicated by a supported exception, if still retained.</summary>
    public long? ExceptionCause { get; private set; }

    /// <summary>Reads a retained step by zero-based timeline index. Invalid or evicted indices return null.</summary>
    public RecordedStep? GetStep(int index)
    {
        lock (_gate)
        {
            return (uint)index < (uint)(_entries.Count - _first) ? Read(_entries[_first + index]) : null;
        }
    }

    /// <summary>Finds the retained index of a sequence number, or -1 after eviction.</summary>
    public int IndexOf(long sequence)
    {
        lock (_gate)
        {
            var low = _first;
            var high = _entries.Count - 1;
            while (low <= high)
            {
                var mid = low + (high - low) / 2;
                var value = _entries[mid].Sequence;
                if (value == sequence) return mid - _first;
                if (value < sequence) low = mid + 1; else high = mid - 1;
            }
            return -1;
        }
    }

    internal void Record(DebugFrame frame, IReadOnlyList<DebugLocalSlot>? slots, object?[]? values, bool breakpoint)
    {
        lock (_gate)
        {
            frame.RecordedValues.Clear();
            if (slots is not null && values is not null)
            {
                for (var i = 0; i < Math.Min(slots.Count, values.Length); i++)
                    frame.RecordedValues[slots[i].Name] = Capture(slots[i].Name, slots[i].Type, values[i]);
            }
            Append(frame, null, breakpoint);
        }
    }

    internal void Write(DebugFrame frame, int site, string path, object? value)
    {
        lock (_gate)
        {
            var dot = path.IndexOf('.');
            if (dot < 0)
            {
                frame.RecordedValues[path] = Capture(path, null, value);
            }
            else if (frame.RecordedValues.TryGetValue(path[..dot], out var owner))
            {
                var member = path[(dot + 1)..];
                var captured = Capture(member, null, value);
                // Two names for one object must show the same fields at a write event, including caller aliases.
                for (var current = frame; current is not null; current = current.Parent)
                {
                    foreach (var alias in current.RecordedValues.Values.Where(v => v.Value == owner.Value).ToArray())
                    {
                        var children = alias.Expand().Where(v => v.Name != member).Append(captured).ToArray();
                        current.RecordedValues[alias.Name] = new DebugValue(alias.Name, alias.Type, alias.Value, children);
                    }
                }
            }
            else
            {
                frame.RecordedValues[path] = Capture(path, null, value);
            }
            Append(frame, path, false, site);
        }
    }

    internal void FindExceptionCause(DebugFrame frame, Exception exception)
    {
        // Only a syntactically identified operand is considered. No guess from the most recent unrelated write.
        var statement = _map.GetStatement(frame.StatementId);
        var path = exception is DivideByZeroException ? statement?.DivisionOperand
            : exception is NullReferenceException ? statement?.NullReceiver : null;
        lock (_gate)
        {
            ExceptionCause = null;
            if (path is null) return;
            var current = ValuePath.Find(frame.RecordedValues.Values.ToArray(), path);
            if (current?.Value != (exception is NullReferenceException ? "null" : "0")) return;
            var found = FindLastWriteCore(path, frame.Id, _entries.Count);
            if (found >= 0)
            {
                var write = Read(_entries[found]);
                if (ValuePath.Find(write.Frames[0].Locals, path)?.Value == current.Value) ExceptionCause = write.Sequence;
            }
        }
    }

    internal int FindLastWrite(string path, long frameId, int beforeIndex)
    {
        lock (_gate)
        {
            var anchor = Read(_entries[_first + Math.Min(beforeIndex, _entries.Count - _first - 1)]);
            var locals = anchor.Frames.FirstOrDefault(f => f.Id == frameId)?.Locals ?? [];
            if (!locals.Any(v => v.Name == path) && ValuePath.Find(locals, "this." + path) is not null) path = "this." + path;
            var dot = path.IndexOf('.');
            var owner = dot < 0 ? null : ValuePath.Find(locals, path[..dot]);
            var identity = owner is not null && owner.Value.StartsWith('{') ? owner.Value : null;
            var found = FindLastWriteCore(path, frameId, _first + beforeIndex, identity);
            return found < 0 ? -1 : found - _first;
        }
    }

    private int FindLastWriteCore(string path, long frameId, int before, string? identity = null)
    {
        for (var i = Math.Min(before, _entries.Count) - 1; i >= _first; i--)
        {
            var step = Read(_entries[i]);
            if (step.Frames[0].Id == frameId && (step.WrittenPath == path || step.WrittenPath == "this." + path)) return i;
            if (identity is null || step.WrittenPath is not { } written) continue;
            var dot = written.IndexOf('.');
            var targetDot = path.IndexOf('.');
            if (dot > 0 && targetDot > 0 && written[dot..] == path[targetDot..] &&
                ValuePath.Find(step.Frames[0].Locals, written[..dot])?.Value == identity)
            {
                return i;
            }
        }
        return -1;
    }

    private DebugValue Capture(string name, string? declaredType, object? value)
    {
        var type = value?.GetType();
        var typeName = ValueInspector.FormatType(value, declaredType);
        if (value is null) return new DebugValue(name, typeName, "null", []);
        if (IsScalar(type!))
        {
            // ValueInspector never runs user code for these base-library scalar types.
            var display = value is string text ? "\"" + System.Text.Json.JsonEncodedText.Encode(text).ToString() + "\"" : ScalarInspector.FormatValue(value);
            return new DebugValue(name, typeName, display, []);
        }

        var identity = _identities.GetValue(value, _ => new Identity(++_nextIdentity));
        var fields = new List<DebugValue>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!IsScalar(field.FieldType)) continue;
                var fieldName = field.Name;
                if (fieldName.StartsWith('<') && fieldName.EndsWith(">k__BackingField", StringComparison.Ordinal))
                {
                    fieldName = fieldName[1..fieldName.IndexOf('>')];
                }
                try { fields.Add(Capture(fieldName, null, field.GetValue(value))); }
                catch (Exception ex) when (ex is MemberAccessException or NotSupportedException) { }
            }
        }
        return new DebugValue(name, typeName, $"{{{type!.Name}}} @{identity.Id.ToString(CultureInfo.InvariantCulture)}", fields);
    }

    private static bool IsScalar(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
        || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid);

    private void Append(DebugFrame frame, string? path, bool breakpoint, int writeSite = 0)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(FormatVersion);
        var sequence = ++_sequence;
        writer.Write(sequence);
        writer.Write7BitEncodedInt(Environment.CurrentManagedThreadId);
        writer.Write(breakpoint);
        writer.Write(path ?? string.Empty);
        var frames = new List<DebugFrame>();
        for (var f = frame; f is not null && frames.Count < 10_000; f = f.Parent) frames.Add(f);
        writer.Write7BitEncodedInt(frames.Count);
        foreach (var f in frames)
        {
            writer.Write(f.Id);
            writer.Write7BitEncodedInt(f.MethodId);
            // Negative ids are write-expression sites in this method, never breakpoint targets.
            writer.Write7BitEncodedInt(ReferenceEquals(f, frame) && writeSite != 0 ? -writeSite : f.StatementId);
            writer.Write7BitEncodedInt(f.RecordedValues.Count);
            foreach (var value in f.RecordedValues.Values) WriteValue(writer, value);
        }
        writer.Flush();
        var length = checked((int)stream.Length);
        if (length > _ring.Length) { _dropped++; return; }
        while (_used + length > _ring.Length)
        {
            _used -= _entries[_first++].Length;
            _evicted++;
        }
        if (_first > 4096 && _first > _entries.Count / 2) { _entries.RemoveRange(0, _first); _first = 0; }
        var bytes = stream.GetBuffer();
        var firstPart = Math.Min(length, _ring.Length - _tail);
        bytes.AsSpan(0, firstPart).CopyTo(_ring.AsSpan(_tail));
        bytes.AsSpan(firstPart, length - firstPart).CopyTo(_ring);
        _entries.Add(new Entry(sequence, _tail, length));
        _tail = (_tail + length) % _ring.Length;
        _used += length;
    }

    private RecordedStep Read(Entry entry)
    {
        var bytes = CopyEncoded(entry);
        using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
        if (reader.ReadByte() != FormatVersion) throw new InvalidDataException("Unsupported recording format.");
        var sequence = reader.ReadInt64();
        var thread = reader.Read7BitEncodedInt();
        var breakpoint = reader.ReadBoolean();
        var path = reader.ReadString();
        var frames = new RecordedFrame[reader.Read7BitEncodedInt()];
        for (var i = 0; i < frames.Length; i++)
        {
            var id = reader.ReadInt64();
            var method = reader.Read7BitEncodedInt();
            var statement = reader.Read7BitEncodedInt();
            var locals = new DebugValue[reader.Read7BitEncodedInt()];
            for (var j = 0; j < locals.Length; j++) locals[j] = ReadValue(reader);
            var symbol = _map.GetMethod(method);
            var location = statement < 0 ? symbol?.WriteSites.GetValueOrDefault(-statement)
                : _map.GetStatement(statement)?.Location;
            frames[i] = new(id, method, statement, new(symbol?.Name ?? "?", location ?? symbol?.Location), locals);
        }
        return new(sequence, thread, frames, path.Length == 0 ? null : path, breakpoint);
    }

    internal byte[] CopyEncodedStep(int index)
    {
        lock (_gate) return CopyEncoded(_entries[_first + index]);
    }

    private byte[] CopyEncoded(Entry entry)
    {
        var bytes = new byte[entry.Length];
        var firstPart = Math.Min(entry.Length, _ring.Length - entry.Offset);
        _ring.AsSpan(entry.Offset, firstPart).CopyTo(bytes);
        _ring.AsSpan(0, entry.Length - firstPart).CopyTo(bytes.AsSpan(firstPart));
        return bytes;
    }

    private static void WriteValue(BinaryWriter writer, DebugValue value)
    {
        writer.Write(value.Name); writer.Write(value.Type); writer.Write(value.Value);
        var children = value.Expand();
        writer.Write7BitEncodedInt(children.Count);
        foreach (var child in children) WriteValue(writer, child);
    }

    private static DebugValue ReadValue(BinaryReader reader)
    {
        var name = reader.ReadString(); var type = reader.ReadString(); var value = reader.ReadString();
        var children = new DebugValue[reader.Read7BitEncodedInt()];
        for (var i = 0; i < children.Length; i++) children[i] = ReadValue(reader);
        return new DebugValue(name, type, value, children);
    }

    private readonly record struct Entry(long Sequence, int Offset, int Length);
    private sealed record Identity(long Id);
}
