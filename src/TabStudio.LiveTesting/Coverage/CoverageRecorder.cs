using TabStudio.LiveTesting.Instrumentation;

namespace TabStudio.LiveTesting.Coverage;

/// <summary>
/// The process-wide link between instrumented code and the test that runs it: the hit table of the current test,
/// kept in an <see cref="AsyncLocal{T}"/> that the coverage runtime of every instrumented assembly reads.
/// </summary>
/// <remarks>
/// The runtime looks the link up once, when an instrumented assembly first runs, so
/// <see cref="EnsureRegistered"/> must be called before the assemblies of a coverage build are loaded: test
/// discovery can already run their code, through data sources such as <c>[MemberData]</c>.
/// </remarks>
public static class CoverageRecorder
{
    private static readonly AsyncLocal<bool[][]?> Current = new();
    private static readonly Lock Gate = new();
    private static bool _registered;

    /// <summary>Publishes the link in <see cref="AppContext"/>, once per process.</summary>
    public static void EnsureRegistered()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            AppContext.SetData(CoverageRuntimeSource.DataKey, new object[] { CoverageRuntimeSource.ContractVersion, Current });
            _registered = true;
        }
    }

    /// <summary>
    /// Makes a hit table the current one on this thread of execution, and on the tasks it starts, until the
    /// returned scope is disposed.
    /// </summary>
    /// <param name="table">One array per module slot, one element per coverage point.</param>
    internal static IDisposable Enter(bool[][] table)
    {
        EnsureRegistered();
        var previous = Current.Value;
        Current.Value = table;
        return new Scope(previous);
    }

    /// <summary>Puts the previous table back.</summary>
    private sealed class Scope(bool[][]? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
