namespace TabStudio.Compilation.Running;

/// <summary>
/// Temporarily sets a hosted app's environment while it owns ProcessConsole. In-process .NET apps share the
/// process environment; callers must acquire the same console lease before entering this scope.
/// </summary>
public sealed class RunEnvironment : IDisposable
{
    private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);
    /// <summary>Enters an environment scope after acquiring the process console lease.</summary>
    /// <param name="variables">Variables to set for the hosted application.</param>
    public RunEnvironment(IReadOnlyDictionary<string, string?>? variables)
    {
        try
        {
            foreach (var (key, value) in variables ?? new Dictionary<string, string?>())
            {
                _previous[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }
        }
        catch { Dispose(); throw; }
    }
    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (key, value) in _previous) Environment.SetEnvironmentVariable(key, value);
        _previous.Clear();
    }
}
