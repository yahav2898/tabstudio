using TabStudio.TestRunner;

namespace TabStudio.LiveTesting;

/// <summary>An entry of the Live Unit Testing include and exclude list.</summary>
/// <param name="ProjectFilePath">The project the entry is about.</param>
/// <param name="Name">
/// What in the project: an empty string for the whole project, else a namespace, a test class's full name, a test
/// method's full name or a test's id, as Test Explorer names them.
/// </param>
/// <param name="Include">Whether it is included (<see langword="true"/>) or excluded.</param>
public sealed record LiveTestRule(string ProjectFilePath, string Name, bool Include);

/// <summary>
/// Which projects and tests Live Unit Testing includes, as chosen with Include and Exclude in the Solution
/// Explorer and the Test Explorer. Everything is included until excluded.
/// </summary>
/// <remarks>
/// <para>
/// The most specific entry wins, as in Visual Studio: a test is included when the nearest entry above it (its
/// own, its method's, its class's, a namespace's or its project's) includes it, or when there is none. Excluding a
/// class and then including one of its tests runs that test alone.
/// </para>
/// <para>
/// Excluded tests never run live. An excluded project that is tested is not instrumented, so its files get no
/// markers. The list is safe to use from any thread.
/// </para>
/// </remarks>
public sealed class LiveTestInclusion
{
    private readonly Lock _gate = new();
    private readonly List<LiveTestRule> _rules;

    /// <summary>Creates the list.</summary>
    /// <param name="rules">Entries saved before, or <see langword="null"/> to include everything.</param>
    public LiveTestInclusion(IEnumerable<LiveTestRule>? rules = null)
    {
        _rules = rules is null ? [] : [.. rules];
    }

    /// <summary>Raised after an entry was added or removed.</summary>
    public event EventHandler? Changed;

    /// <summary>The entries, to save them.</summary>
    public IReadOnlyList<LiveTestRule> Rules
    {
        get
        {
            lock (_gate)
            {
                return [.. _rules];
            }
        }
    }

    /// <summary>Whether a whole project is included.</summary>
    /// <param name="projectFilePath">The project.</param>
    public bool IsProjectIncluded(string projectFilePath)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        lock (_gate)
        {
            return Find(projectFilePath, [string.Empty]) ?? true;
        }
    }

    /// <summary>The projects that are excluded as a whole.</summary>
    public IReadOnlySet<string> ExcludedProjects
    {
        get
        {
            lock (_gate)
            {
                return _rules.Where(r => r.Name.Length == 0 && !r.Include).Select(r => r.ProjectFilePath).ToHashSet(StringComparer.Ordinal);
            }
        }
    }

    /// <summary>Whether a test is included.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="test">The test.</param>
    public bool IsIncluded(string projectFilePath, TestCase test)
    {
        ArgumentNullException.ThrowIfNull(test);
        return IsIncluded(projectFilePath, test.Id);
    }

    /// <summary>Whether a test, class, namespace or project is included.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="name">A test id, a method's or class's full name, a namespace, or an empty string for the project.</param>
    public bool IsIncluded(string projectFilePath, string name)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            return Find(projectFilePath, [name, .. Ancestors(name)]) ?? true;
        }
    }

    /// <summary>Includes or excludes a project, namespace, class, method or test, and everything in it.</summary>
    /// <param name="projectFilePath">The project.</param>
    /// <param name="name">What in the project, as for <see cref="IsIncluded(string, string)"/>; an empty string for the whole project.</param>
    /// <param name="include">Whether to include it.</param>
    public void Set(string projectFilePath, string name, bool include)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            _rules.RemoveAll(r => r.ProjectFilePath == projectFilePath && IsWithin(r.Name, name));
            var inherited = Find(projectFilePath, Ancestors(name)) ?? true;
            if (inherited != include)
            {
                _rules.Add(new LiveTestRule(projectFilePath, name, include));
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The names above a name, nearest first: a data row's method, then its class, outer classes and namespaces,
    /// and finally the project (an empty string).
    /// </summary>
    /// <param name="name">A test id, a method's or class's full name, or a namespace.</param>
    internal static IEnumerable<string> Ancestors(string name)
    {
        if (name.Length == 0)
        {
            yield break;
        }

        var arguments = name.IndexOf('(', StringComparison.Ordinal);
        var end = arguments < 0 ? name.Length : arguments;
        if (arguments >= 0)
        {
            yield return name[..arguments];
        }

        for (var i = end - 1; i > 0; i--)
        {
            if (name[i] is '.' or '+')
            {
                yield return name[..i];
            }
        }

        yield return string.Empty;
    }

    /// <summary>Whether a rule's name is the name or something inside it.</summary>
    private static bool IsWithin(string ruleName, string name) =>
        name.Length == 0
        || ruleName == name
        || (ruleName.Length > name.Length && ruleName.StartsWith(name, StringComparison.Ordinal) && ruleName[name.Length] is '.' or '+' or '(');

    /// <summary>The first of the names that has an entry decides; <see langword="null"/> when none has.</summary>
    private bool? Find(string projectFilePath, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            foreach (var rule in _rules)
            {
                if (rule.ProjectFilePath == projectFilePath && rule.Name == name)
                {
                    return rule.Include;
                }
            }
        }

        return null;
    }
}
