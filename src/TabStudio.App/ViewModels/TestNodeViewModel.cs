using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>What a row of the Test Explorer stands for.</summary>
public enum TestNodeKind
{
    /// <summary>A test project.</summary>
    Project,

    /// <summary>A namespace.</summary>
    Namespace,

    /// <summary>A test class.</summary>
    Class,

    /// <summary>A parameterized test method, whose data rows are its children.</summary>
    Method,

    /// <summary>One test: a test method without data, or one data row.</summary>
    Test,

    /// <summary>A group made by "Group By": an outcome, a duration or a trait.</summary>
    Group,
}

/// <summary>What a Test Explorer row's context menu does. Implemented by the Test Explorer.</summary>
public interface ITestNodeActions
{
    /// <summary>Runs the tests under a row.</summary>
    Task RunAsync(TestNodeViewModel node);

    /// <summary>Opens a row's test or class in the editor.</summary>
    Task GoToAsync(TestNodeViewModel node);
}

/// <summary>
/// One row of the Test Explorer tree: a group of tests, or one test, with the outcome of its tests and their
/// total duration. Like the Solution Explorer, the tree is shown as a flat list of visible rows.
/// </summary>
public sealed partial class TestNodeViewModel : ObservableObject
{
    private const double IndentPerLevel = 14;

    /// <summary>The states from the one a group shows first to the one it shows last.</summary>
    private static readonly TestState[] WorstFirst =
        [TestState.Failed, TestState.Running, TestState.Queued, TestState.NotRun, TestState.Skipped, TestState.Passed];

    private readonly ITestNodeActions _actions;
    private readonly List<TestNodeViewModel> _children = [];

    /// <summary>Creates a row.</summary>
    /// <param name="kind">What the row stands for.</param>
    /// <param name="key">Identifies the row across rebuilds of the tree, to keep it expanded and selected.</param>
    /// <param name="name">The text shown.</param>
    /// <param name="depth">How deep the row is; top-level rows are 0.</param>
    /// <param name="entries">The tests under the row, at least one.</param>
    /// <param name="actions">What the row's commands do.</param>
    public TestNodeViewModel(TestNodeKind kind, string key, string name, int depth, IReadOnlyList<TestEntry> entries, ITestNodeActions actions)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentOutOfRangeException.ThrowIfZero(entries.Count, nameof(entries));

        Kind = kind;
        Key = key;
        Name = name;
        Depth = depth;
        Entries = entries;
        _actions = actions;
        Refresh();
    }

    /// <summary>What the row stands for.</summary>
    public TestNodeKind Kind { get; }

    /// <summary>Identifies the row across rebuilds of the tree.</summary>
    public string Key { get; }

    /// <summary>The text shown.</summary>
    public string Name { get; }

    /// <summary>More about the row, for its tooltip, such as a class's full name.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether "Run" selects the row's tests by name (its project, classes or method), which also picks up tests
    /// added since the tree was built. Rows under an outcome, duration or trait group hold only some of a class's
    /// tests, so they select theirs one by one.
    /// </summary>
    public bool SelectsByName { get; init; } = true;

    /// <summary>How deep the row is.</summary>
    public int Depth { get; }

    /// <summary>The left indentation, in pixels.</summary>
    public double IndentWidth => 4 + (Depth * IndentPerLevel);

    /// <summary>The tests under the row.</summary>
    public IReadOnlyList<TestEntry> Entries { get; }

    /// <summary>The test, for a <see cref="TestNodeKind.Test"/> row.</summary>
    public TestEntry? Entry => Kind == TestNodeKind.Test ? Entries[0] : null;

    /// <summary>The rows under this one.</summary>
    public IReadOnlyList<TestNodeViewModel> Children => _children;

    /// <summary>Whether the row has rows under it.</summary>
    public bool IsExpandable => _children.Count > 0;

    /// <summary>"(5)": how many tests a group holds.</summary>
    public string? CountText => Kind == TestNodeKind.Test ? null : "(" + Entries.Count.ToString(CultureInfo.InvariantCulture) + ")";

    /// <summary>The project of the row's tests; for a group spanning projects, its first test's.</summary>
    public string ProjectFilePath => Entries[0].ProjectFilePath;

    /// <summary>Where "Go To Test" goes: the test method, or the class.</summary>
    public TestSourceLocation? Location => Kind switch
    {
        TestNodeKind.Test or TestNodeKind.Method => Entries[0].Location,
        TestNodeKind.Class => Entries.Select(e => e.ClassLocation).FirstOrDefault(l => l is not null) ?? Entries[0].Location,
        _ => null,
    };

    /// <summary>Whether "Go To Test" has somewhere to go.</summary>
    public bool HasLocation => Location is not null;

    /// <summary>Whether the row's children are shown.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>
    /// The outcome shown: the worst of the tests under the row, failed first, then running, waiting, not run,
    /// skipped and passed.
    /// </summary>
    [ObservableProperty]
    public partial TestState State { get; private set; }

    /// <summary>How long the row's tests took, or <see langword="null"/> when none has run.</summary>
    [ObservableProperty]
    public partial string? DurationText { get; private set; }

    /// <summary>Whether a result under the row is out of date because code changed after it ran.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconOpacity))]
    public partial bool IsStale { get; private set; }

    /// <summary>Stale results are drawn faded, as in Visual Studio.</summary>
    public double IconOpacity => IsStale ? 0.45 : 1;

    /// <summary>The tests the row selects, project by project.</summary>
    public IReadOnlyList<TestProjectSelection> Selection()
    {
        if (!SelectsByName || Kind == TestNodeKind.Group)
        {
            return Entries
                .GroupBy(e => e.ProjectFilePath, StringComparer.Ordinal)
                .Select(g => new TestProjectSelection(g.Key, g.Select(e => e.Id).ToList()))
                .ToList();
        }

        IReadOnlyList<string>? names = Kind switch
        {
            TestNodeKind.Project => null,
            TestNodeKind.Namespace => Entries.Select(e => e.TypeFullName).Distinct(StringComparer.Ordinal).ToList(),
            TestNodeKind.Class => [Entries[0].TypeFullName],
            TestNodeKind.Method => [Entries[0].MethodFullName],
            _ => [Entries[0].Id],
        };
        return [new TestProjectSelection(ProjectFilePath, names)];
    }

    /// <summary>Recomputes the outcome, duration and staleness from the row's tests.</summary>
    public void Refresh()
    {
        var counts = new int[Enum.GetValues<TestState>().Length];
        var duration = TimeSpan.Zero;
        var ran = false;
        var stale = false;
        foreach (var entry in Entries)
        {
            counts[(int)entry.State]++;
            if (entry.Result is { } result && entry.State is TestState.Passed or TestState.Failed or TestState.Skipped)
            {
                ran = true;
                duration += result.Duration;
                stale |= entry.IsStale;
            }
        }

        State = WorstState(counts);
        DurationText = ran ? TestDisplay.Duration(duration) : null;
        IsStale = stale;
    }

    /// <summary>Adds a row under this one, while the tree is built.</summary>
    /// <param name="child">The row.</param>
    internal void AddChild(TestNodeViewModel child) => _children.Add(child);

    [RelayCommand]
    private Task RunAsync() => _actions.RunAsync(this);

    [RelayCommand(CanExecute = nameof(HasLocation))]
    private Task GoToTestAsync() => _actions.GoToAsync(this);

    private static TestState WorstState(int[] counts)
    {
        foreach (var state in WorstFirst)
        {
            if (counts[(int)state] > 0)
            {
                return state;
            }
        }

        return TestState.NotRun;
    }
}
