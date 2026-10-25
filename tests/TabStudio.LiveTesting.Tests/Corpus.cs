namespace TabStudio.LiveTesting.Tests;

/// <summary>
/// Programs that sweep the C# constructs an instrumenter has to keep compiling and behaving the same: the same
/// corpus the debugger's instrumentation is tested with.
/// </summary>
internal static class Corpus
{
    public const string Throwing = """
        Run();

        static void Run()
        {
            var values = new[] { 1, 2 };
            Helper(values);
        }

        static void Helper(int[] values)
        {
            Console.WriteLine(values.Length);
            throw new InvalidOperationException("boom");
        }
        """;

    public const string Constructs = """
        var shapes = new List<Shape> { new Circle(1), new Square(2) };
        foreach (var shape in shapes)
            Console.WriteLine($"{shape.Name}: {(int)(shape.Area * 100)}");

        Console.WriteLine(string.Join(",", Numbers.Up(3)));
        await foreach (var n in Numbers.UpAsync(2)) Console.Write(n);
        Console.WriteLine();

        var counter = new Counter();
        counter.Increment();
        counter++;
        Console.WriteLine(counter.Value + " " + counter[1]);

        int total = 0;
        Add(ref total, 5);
        Console.WriteLine(total);
        Console.WriteLine(Classify(3) + Classify(-1));

        Func<int, int> twice = x =>
        {
            var y = x * 2;
            return y; // lambda
        };
        Func<int, int> square = static x => { return x * x; };
        Action<string> print = delegate (string s) { Console.WriteLine(s); };
        print($"{twice(4)} {square(3)}");

        var i = 0;
        loop:
        if (i < 2) { i++; goto loop; }
        Console.WriteLine(i);

        lock (shapes) Console.WriteLine("locked");
        using (var reader = new StringReader("line")) Console.WriteLine(reader.ReadLine());
        try { throw new InvalidOperationException("caught"); }
        catch (InvalidOperationException e) when (e.Message == "caught") { Console.WriteLine(e.Message); }
        finally { Console.WriteLine("finally"); }

        var point = new Point(1, 2);
        Console.WriteLine(point with { Y = 5 });
        Console.WriteLine(Describe(point));
        Console.WriteLine(LocalSum(2, 3));
        do total--; while (total > 3);
        Console.WriteLine(total);
        switch (total) { case 3: Console.WriteLine("three"); break; default: Console.WriteLine("other"); break; }
        Console.WriteLine(new Vector { X = -4 }.Length());

        static void Add(ref int target, in int amount) { target += amount; }
        static string Classify(int n) => n switch { > 0 => "positive", < 0 => "negative", _ => "zero" };
        static string Describe(object o) { if (o is Point { X: var x }) return "x=" + x; return "?"; }
        int LocalSum(int a, int b) => a + b;

        abstract record Shape(string Name)
        {
            public abstract double Area { get; }
        }

        sealed record Circle(double Radius) : Shape("circle")
        {
            public override double Area => Math.PI * Radius * Radius; // property
        }

        sealed record Square(double Side) : Shape("square")
        {
            public override double Area { get { return Side * Side; } }
        }

        readonly record struct Point(int X, int Y);

        sealed class Counter
        {
            private int _value;
            public int Value => _value;
            public int this[int offset] => _value + offset;
            public void Increment() => _value++;
            public static Counter operator ++(Counter c) { c._value++; return c; }
        }

        static class Numbers
        {
            public static IEnumerable<int> Up(int count)
            {
                for (var i = 1; i <= count; i++)
                {
                    yield return i; // iterator
                }
            }

            public static async IAsyncEnumerable<int> UpAsync(int count)
            {
                for (var i = 1; i <= count; i++)
                {
                    await Task.Yield();
                    yield return i;
                }
            }
        }

        struct Vector
        {
            public int X;
            public int Length() { var copy = this; return Math.Abs(copy.X); }
        }
        """;

    public const string ConstructsOutput = """
        circle: 314
        square: 400
        1,2,3
        12
        2 3
        5
        positivenegative
        8 9
        2
        locked
        line
        caught
        finally
        Point { X = 1, Y = 5 }
        x=1
        5
        3
        three
        4

        """;

    public const string MoreConstructs = """
        using System.Collections;

        var dictionary = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        foreach (var (key, value) in dictionary)
        {
            Console.WriteLine($"{key}={value}");
        }

        using var stream = new MemoryStream();
        stream.WriteByte(1);
        Console.WriteLine(stream.Length);

        object boxed = 42;
        switch (boxed)
        {
            case int n when n > 40:
                Console.WriteLine("big " + n);
                break;
            case int n:
                Console.WriteLine("small " + n);
                break;
            default:
                break;
        }

        var (left, right) = (3, 4);
        switch (left, right)
        {
            case (3, 4): Console.WriteLine("tuple"); break;
        }

        var queue = new Queue<int>(new[] { 1, 2, 3 });
        while (queue.TryDequeue(out var item))
            if (item % 2 == 1) Console.Write(item);
            else if (item == 2) Console.Write("-");
        Console.WriteLine();

        int captured;
        void Show() => Console.WriteLine(captured);
        captured = 5;
        Show();

        Console.WriteLine(Echo<string>("echo"));
        dynamic dyn = 7;
        Console.WriteLine(dyn + 1);

        Action<int, int> discard = (_, _) => { Console.WriteLine("discards"); };
        discard(1, 2);
        var total = await Task.Run(async () => { await Task.Delay(1); return 10; });
        Console.WriteLine(total);
        checked { Console.WriteLine(int.MaxValue - 1); }
        { Console.WriteLine("nested block"); }
        var seeded = new Seeded(9);
        Console.WriteLine(seeded.Next());
        IGreeter greeter = new Greeter();
        Console.WriteLine(greeter.Greet());
        var events = new Events();
        events.Changed += () => Console.WriteLine("changed");
        events.Raise();
        var wrapper = new Wrapper { Value = 3 };
        Console.WriteLine(wrapper.Value);
        Console.WriteLine(new Money(5) + new Money(6));
        Console.WriteLine(Sum(1, 2, 3));
        Console.WriteLine(new Bag().Count());

        static T Echo<T>(T value) => value;
        static int Sum(params int[] values) { var sum = 0; foreach (var v in values) sum += v; return sum; }

        sealed class Seeded(int seed)
        {
            public int Next() { return seed + 1; }
        }

        interface IGreeter
        {
            string Greet() { return "default greeting"; }
        }

        sealed class Greeter : IGreeter;

        sealed class Events
        {
            private Action? _changed;
            public event Action Changed { add => _changed += value; remove => _changed -= value; }
            public void Raise() => _changed?.Invoke();
        }

        sealed class Wrapper
        {
            private int _value;
            public int Value { get => _value; init => _value = value; }
        }

        readonly record struct Money(int Cents)
        {
            public static Money operator +(Money a, Money b) => new(a.Cents + b.Cents);
        }

        struct Bag : IEnumerable<int>
        {
            public int Size => 2;
            public IEnumerator<int> GetEnumerator() { for (var i = 0; i < Size; i++) yield return i; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public int Count() { var n = 0; foreach (var _ in this) n++; return n; }
        }
        """;
}
