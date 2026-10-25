using Stopwatch = System.Diagnostics.Stopwatch;
using Android.App;
using Android.OS;
using Android.Util;

namespace GcBridgeRace;

/// <summary>
/// Reproduces a race between Mono's concurrent major mark and the Tarjan GC bridge.
/// </summary>
/// <remarks>
/// <para>
/// Three things together:
/// </para>
/// <list type="number">
/// <item>A large, live object graph: about a million small objects, dictionaries whose entry arrays are large
/// objects (over 8,000 bytes) and arrays over 1 MB, which Mono maps one by one.</item>
/// <item>A Java peer that is dead on the managed side, waiting on the Java side, whose managed fields reach the
/// graph. <see cref="Handler.Post(Action)"/> makes one per call: a <c>Java.Lang.Thread.RunnableImplementor</c>
/// that only a ConditionalWeakTable keyed on its own Action holds, which Avalonia's Android dispatcher does for
/// every signal. In a minor collection every old object counts as dead to the bridge, so its pass walks the whole
/// graph and tags each object's header for hundreds of milliseconds.</item>
/// <item>Threads that allocate, so that minor collections come often and concurrent major marks run beside them,
/// and that store young objects and new large entry arrays into old objects.</item>
/// </list>
/// <para>
/// Within minutes the app aborts with "How can an object and a reference inside it not be in the same block?", a
/// failed assertion on <c>free_chunk_map</c> in sgen-los.c, or SIGSEGV on the SGen worker thread; with
/// <c>MONO_GC_DEBUG=mod-union-consistency-check</c> it aborts sooner with "major-&gt;major reference ... not found in
/// remsets". It logs GCBRIDGERACE_ALIVE every 10 s while it runs.
/// </para>
/// </remarks>
[Activity(Label = "GcBridgeRace", MainLauncher = true, Name = "com.example.gcbridgerace.MainActivity")]
public sealed class MainActivity : Activity
{
    private const string Tag = "GcBridgeRace";

    /// <summary>Keeps the graph alive.</summary>
    private static Graph? s_graph;

    /// <inheritdoc/>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var graph = s_graph = new Graph();
        var main = new Handler(Looper.MainLooper!);
        Start(() =>
        {
            while (true)
            {
                // The closure captures graph, so the waiting RunnableImplementor reaches all of it.
                main.Post(() => graph.Touch());
                Thread.Sleep(2);
            }
        });
        for (var i = 0; i < 3; i++)
        {
            var seed = i;
            Start(() => graph.Churn(seed));
        }

        var clock = Stopwatch.StartNew();
        Start(() =>
        {
            while (true)
            {
                Thread.Sleep(10_000);
                Log.Info(Tag, $"GCBRIDGERACE_ALIVE {clock.Elapsed.TotalSeconds:0} s, {GC.CollectionCount(0)} collections, {graph.Touches} posts run");
            }
        });
        Log.Info(Tag, "GCBRIDGERACE_STARTED");
    }

    private static void Start(Action body) => new Thread(() => body()) { IsBackground = true }.Start();
}

/// <summary>A small object of the graph.</summary>
internal sealed class Node
{
    public Node? Next;
    public object? Young;
    public readonly int[] Data = new int[4];
}

/// <summary>The live graph the bridge walks.</summary>
internal sealed class Graph
{
    private readonly Node[] _nodes = new Node[1 << 20];
    private readonly Dictionary<int, Node>[] _maps = new Dictionary<int, Node>[512];
    private readonly Holder[] _holders = new Holder[64];
    private long _touches;

    public Graph()
    {
        Node? previous = null;
        for (var i = 0; i < _nodes.Length; i++)
        {
            previous = _nodes[i] = new Node { Next = previous };
        }

        for (var i = 0; i < _maps.Length; i++)
        {
            _maps[i] = Fill(new Dictionary<int, Node>(), 2000, i);
        }

        for (var i = 0; i < _holders.Length; i++)
        {
            _holders[i] = new Holder(new byte[(1 << 20) + (i * 4096)]);
        }
    }

    public long Touches => Interlocked.Read(ref _touches);

    public void Touch() => Interlocked.Increment(ref _touches);

    /// <summary>Allocates, and stores new objects into old ones, forever.</summary>
    public void Churn(int seed)
    {
        var random = new Random(seed);
        while (true)
        {
            // A young object stored into an old one.
            _nodes[random.Next(_nodes.Length)].Young = new object[random.Next(1, 8)];

            // A dictionary rebuilt from empty: each Resize stores a new entries array, large from 431 entries on,
            // into a dictionary that may already be old and marked.
            if (random.Next(64) == 0)
            {
                var index = random.Next(_maps.Length);
                var map = Fill(new Dictionary<int, Node>(), random.Next(500, 4000), index);
                Volatile.Write(ref _maps[index], map);
            }

            // A small object pointing at a new array over 1 MB.
            if (random.Next(20_000) == 0)
            {
                _holders[random.Next(_holders.Length)].Array = new byte[(1 << 20) + (random.Next(256) * 4096)];
            }

            // Garbage, so that the nursery fills.
            _ = new byte[random.Next(16, 1024)];
        }
    }

    private Dictionary<int, Node> Fill(Dictionary<int, Node> map, int count, int salt)
    {
        for (var i = 0; i < count; i++)
        {
            map[(salt * 7919) + i] = _nodes[((salt * 104729) + i) & (_nodes.Length - 1)];
        }

        return map;
    }

    private sealed class Holder(byte[] array)
    {
        public byte[] Array = array;
    }
}
