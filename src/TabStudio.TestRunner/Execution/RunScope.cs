namespace TabStudio.TestRunner.Execution;

/// <summary>
/// State shared by every class in one run: fixtures and initializers whose lifetime is the whole run, such as
/// xUnit's collection fixtures and MSTest's assembly initialize and cleanup.
/// </summary>
/// <remarks>
/// Shared state is created on first use by whichever class needs it, under a lock, and cleaned up by
/// <see cref="Finish"/> after the last class.
/// </remarks>
internal sealed class RunScope(TestRunOptions options)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<object, Lazy<object>> _shared = [];
    private readonly List<(string Description, Action Cleanup)> _cleanups = [];

    /// <summary>The run's options.</summary>
    public TestRunOptions Options { get; } = options;

    /// <summary>
    /// Returns the value for a key, creating it on first use. A failure is remembered, so every class that needs
    /// the value fails the same way.
    /// </summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="key">Identifies the value within the run, such as an assembly and collection name.</param>
    /// <param name="create">Creates the value. Runs at most once per key.</param>
    public T GetOrCreate<T>(object key, Func<T> create)
        where T : class
    {
        Lazy<object> lazy;
        lock (_gate)
        {
            if (!_shared.TryGetValue(key, out var existing))
            {
                existing = new Lazy<object>(() => create(), LazyThreadSafetyMode.ExecutionAndPublication);
                _shared[key] = existing;
            }

            lazy = existing;
        }

        // A Lazy created with ExecutionAndPublication caches an exception and rethrows it with its stack trace.
        return (T)lazy.Value;
    }

    /// <summary>Registers cleanup to run when the run finishes, in reverse order of registration.</summary>
    /// <param name="description">What is cleaned up, for the message if it fails.</param>
    /// <param name="cleanup">The cleanup.</param>
    public void AddCleanup(string description, Action cleanup)
    {
        lock (_gate)
        {
            _cleanups.Add((description, cleanup));
        }
    }

    /// <summary>Runs every registered cleanup and returns a message for each one that failed.</summary>
    public List<string> Finish()
    {
        List<(string Description, Action Cleanup)> cleanups;
        lock (_gate)
        {
            cleanups = [.. _cleanups];
            cleanups.Reverse();
            _cleanups.Clear();
        }

        var failures = new List<string>();
        foreach (var (description, cleanup) in cleanups)
        {
            try
            {
                cleanup();
            }
            catch (Exception ex)
            {
                failures.Add($"{description} failed: {OutcomeClassifier.Describe(OutcomeClassifier.Unwrap(ex))}");
            }
        }

        return failures;
    }
}
