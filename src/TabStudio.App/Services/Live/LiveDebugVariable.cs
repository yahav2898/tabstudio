using TabStudio.App.ViewModels;
using TabStudio.Live.Sessions;
using TabStudio.Swarm;

namespace TabStudio.App.Services.Live;

/// <summary>
/// A value of the host's paused program on a TabStudio Live guest, for the Locals and Watch panels and DataTips: what
/// the host's debugger shows of it, and its members or items, read from the host the first time they are shown.
/// </summary>
/// <param name="guest">The guest's session.</param>
/// <param name="pauseId">The pause it was read in.</param>
/// <param name="variable">The value, as the host sent it.</param>
internal sealed class LiveDebugVariable(LiveGuest guest, long pauseId, SwarmVariable variable) : IDebugVariable
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);

    /// <inheritdoc />
    public string Value => variable.Value;

    /// <inheritdoc />
    public string Type => variable.Type;

    /// <inheritdoc />
    public bool HasChildren => variable.HasChildren;

    /// <inheritdoc />
    public string? EvaluateName => variable.Path;

    /// <inheritdoc />
    public bool CanSetValue => false;

    /// <inheritdoc />
    public IReadOnlyList<(string Name, IDebugVariable Value)> ReadChildren()
    {
        if (!variable.HasChildren || guest.HasEnded || guest.Debug?.PauseId != pauseId)
        {
            return [];
        }

        using var timeout = new CancellationTokenSource(ReadTimeout);
        try
        {
            // Called off the UI thread, which is the session's: the request starts there and is waited for here.
            var children = guest.Dispatcher.InvokeAsync(() => guest.GetVariablesAsync(pauseId, variable.Path, timeout.Token)).GetAwaiter().GetResult();
            return [.. children.Select(child => (child.Name, (IDebugVariable)new LiveDebugVariable(guest, pauseId, child)))];
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The host's program continued, or the host did not answer in time: the row shows no children.
            return [];
        }
    }

    /// <inheritdoc />
    public Task<string> SetValueAsync(string expression, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Values of the host's paused program cannot be changed from a guest.");
}
