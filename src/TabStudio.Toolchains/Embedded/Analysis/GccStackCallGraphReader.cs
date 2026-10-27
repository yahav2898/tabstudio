using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Embedded.Analysis;

/// <summary>A function/synthetic target in GCC's per-object -fcallgraph-info=su VCG output.</summary>
/// <param name="Symbol">Actual GCC symbol identity, source-qualified for local functions.</param>
/// <param name="Name">Compiler-rendered function identity.</param>
/// <param name="Frame">Actual compiler source location and frame size; absent on declarations/placeholders.</param>
/// <param name="IsIndirect">GCC's explicit indirect-call placeholder, never a guessed direct target.</param>
public sealed record GccStackGraphFunction(string Symbol, string Name, StackUsageEntry? Frame, bool IsIndirect);

/// <summary>A GCC-emitted direct edge, or an edge to its explicit indirect-call placeholder.</summary>
public sealed record GccStackGraphEdge(string CallerSymbol, string CalleeSymbol);

/// <summary>One actual compiler callgraph. It never proves whole-program completeness.</summary>
public sealed record GccStackCallGraph(IReadOnlyList<GccStackGraphFunction> Functions, IReadOnlyList<GccStackGraphEdge> Calls);

/// <summary>Call edges bound only to matching .su source/name/frame evidence, plus explicit limitations.</summary>
public sealed record GccStackGraphBinding(IReadOnlyList<StackCall> Calls, IReadOnlyList<string> Warnings);

/// <summary>Reads GCC's documented final per-object VCG callgraph rather than pre-codegen IPA dump tables.</summary>
/// <remarks>Build with -fstack-usage -fcallgraph-info=su. LTO may emit multiple .ci files at link time.
/// Missing libraries, indirect calls, assembly, stale frame sizes and ambiguous local names remain unknown.</remarks>
public static class GccStackCallGraphReader
{
    private static readonly Regex SourceLocation = new(@"^(?<file>.+):(?<line>\d+):(?<column>\d+)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex FrameSize = new(@"^(?<bytes>\d+) bytes \((?<flags>static|dynamic(?:,bounded)?)\)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Reads a bounded GCC .ci VCG graph; unsupported/nested/malformed forms fail explicitly.</summary>
    public static GccStackCallGraph Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > 16 * 1024 * 1024) throw new InvalidDataException("GCC callgraph exceeds 16 MiB.");
        var input = new Reader(text);
        input.Require("graph"); input.Require(":"); input.Require("{");
        var functions = new List<GccStackGraphFunction>();
        var edges = new List<GccStackGraphEdge>();
        while (input.Peek() != "}")
        {
            var kind = input.Next();
            input.Require(":");
            if (kind is not ("node" or "edge"))
            {
                input.Value();
                continue;
            }
            input.Require("{");
            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
            while (input.Peek() != "}")
            {
                var key = input.Next();
                input.Require(":");
                if (!attributes.TryAdd(key, input.Value())) throw new InvalidDataException("Duplicate GCC callgraph attribute: " + key);
                if (attributes.Count > 64) throw new InvalidDataException("GCC callgraph node exceeds 64 attributes.");
            }
            input.Require("}");
            if (kind == "edge")
            {
                edges.Add(new(Required(attributes, "sourcename"), Required(attributes, "targetname")));
                if (edges.Count > 200_000) throw new InvalidDataException("GCC callgraph exceeds 200,000 edges.");
                continue;
            }
            var symbol = Required(attributes, "title");
            var label = Required(attributes, "label").Split('\n');
            var name = label[0];
            StackUsageEntry? frame = null;
            if (label.Length >= 3)
            {
                var location = SourceLocation.Match(label[1]);
                var size = FrameSize.Match(label[2]);
                if (!location.Success || !size.Success
                    || !int.TryParse(location.Groups["line"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var line) || line < 1
                    || !int.TryParse(location.Groups["column"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var column) || column < 1
                    || !ulong.TryParse(size.Groups["bytes"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes))
                {
                    throw new InvalidDataException("Unsupported GCC frame/source label for " + symbol);
                }
                frame = new(location.Groups["file"].Value, line, column, name, bytes, size.Groups["flags"].Value is "static" or "dynamic,bounded");
            }
            functions.Add(new(symbol, name, frame, symbol == "__indirect_call" && name == "Indirect Call Placeholder"));
            if (functions.Count > 100_000) throw new InvalidDataException("GCC callgraph exceeds 100,000 nodes.");
        }
        input.Require("}");
        if (!input.End) throw new InvalidDataException("Unexpected data after GCC callgraph.");
        if (functions.GroupBy(function => function.Symbol, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new InvalidDataException("GCC callgraph contains duplicate symbol identities.");
        return new(functions, edges);
    }

    /// <summary>Binds exact compiler location/name/size identities. Missing/ambiguous evidence never becomes a zero-frame callee.</summary>
    public static GccStackGraphBinding Bind(IReadOnlyList<StackUsageEntry> usage, IReadOnlyList<GccStackCallGraph> graphs)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(graphs);
        if (usage.Count > 100_000 || graphs.Sum(graph => (long)graph.Functions.Count) > 100_000
            || graphs.Sum(graph => (long)graph.Calls.Count) > 200_000)
        {
            throw new InvalidDataException("Combined GCC stack evidence exceeds 100,000 functions/200,000 calls.");
        }
        var evidence = usage.GroupBy(entry => entry).ToDictionary(group => group.Key, group => group.Count());
        var warnings = new HashSet<string>(StringComparer.Ordinal)
        {
            "Imported GCC per-object callgraphs are not a proof of complete whole-program stack usage; interrupt/RTOS/assembly/library overhead still needs review.",
        };
        var matched = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var function in graphs.SelectMany(graph => graph.Functions).Where(function => function.Frame is not null))
        {
            var frame = function.Frame!;
            if (!evidence.TryGetValue(frame, out var found) || found != 1)
            {
                warnings.Add($"{function.Symbol}: .ci frame does not match exactly one .su location/name/size; regenerate both from the same build.");
                continue;
            }
            if (!matched.TryGetValue(function.Symbol, out var names)) matched.Add(function.Symbol, names = new(StringComparer.Ordinal));
            names.Add(frame.Function);
        }
        string Resolve(GccStackGraphFunction function)
        {
            if (matched.TryGetValue(function.Symbol, out var names) && names.Count == 1) return names.Single();
            return "[unmatched GCC symbol] " + function.Symbol;
        }
        var calls = new HashSet<StackCall>();
        foreach (var graph in graphs)
        {
            var nodes = graph.Functions.ToDictionary(function => function.Symbol, StringComparer.Ordinal);
            foreach (var edge in graph.Calls)
            {
                if (!nodes.TryGetValue(edge.CallerSymbol, out var caller)) throw new InvalidDataException("GCC edge has no caller node: " + edge.CallerSymbol);
                var name = Resolve(caller);
                if (!nodes.TryGetValue(edge.CalleeSymbol, out var callee))
                {
                    warnings.Add("GCC edge has no target node: " + edge.CalleeSymbol);
                    calls.Add(new(name, null));
                }
                else
                {
                    calls.Add(new(name, callee.IsIndirect ? null : Resolve(callee)));
                }
            }
        }
        return new(calls.ToArray(), warnings.Order(StringComparer.Ordinal).ToArray());
    }

    private static string Required(Dictionary<string, string> attributes, string key) =>
        attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new InvalidDataException("Missing GCC callgraph attribute: " + key);

    private sealed class Reader(string text)
    {
        private int _position;
        private string? _next;
        public bool End { get { Space(); return _next is null && _position == text.Length; } }
        public string Peek() => _next ??= Token();
        public string Next()
        {
            var result = Peek();
            _next = null;
            return result;
        }
        public void Require(string expected)
        {
            if (Next() != expected) throw new InvalidDataException("Expected " + expected + " in GCC callgraph.");
        }
        public string Value()
        {
            var token = Next();
            if (token.Length == 0 || token[0] is '{' or '}' or ':') throw new InvalidDataException("Expected a scalar GCC callgraph attribute.");
            return token[0] == '"' ? token[1..] : token;
        }
        private void Space()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position])) _position++;
        }
        private string Token()
        {
            Space();
            if (_position >= text.Length) throw new InvalidDataException("Truncated GCC callgraph.");
            var current = text[_position++];
            if (current is '{' or '}' or ':') return current.ToString();
            if (current == '"')
            {
                var value = new StringBuilder("\"");
                while (_position < text.Length)
                {
                    current = text[_position++];
                    if (current == '"') return value.ToString();
                    if (current == '\\')
                    {
                        if (_position >= text.Length) break;
                        current = text[_position++] switch
                        {
                            '\\' => '\\', '"' => '"', 'n' => '\n', 'r' => '\r', 't' => '\t',
                            _ => throw new InvalidDataException("Unsupported GCC callgraph string escape."),
                        };
                    }
                    else if (char.IsControl(current))
                    {
                        throw new InvalidDataException("Unescaped control in GCC callgraph string.");
                    }
                    value.Append(current);
                    if (value.Length > 65536) throw new InvalidDataException("GCC callgraph value exceeds 64 KiB.");
                }
                throw new InvalidDataException("Unterminated GCC callgraph string.");
            }
            var start = _position - 1;
            while (_position < text.Length && !char.IsWhiteSpace(text[_position]) && text[_position] is not ('{' or '}' or ':')) _position++;
            if (_position - start > 256) throw new InvalidDataException("GCC callgraph token exceeds 256 characters.");
            return text[start.._position];
        }
    }
}
