using TabStudio.App.ViewModels;
using TabStudio.Swarm;
using TabStudio.Swarm.Coordinator;

namespace TabStudio.App.Swarm;

/// <summary>
/// A value of the app paused on a device of the swarm, for the tablet's Locals and Watch panels and DataTips: what the
/// device's debugger shows of it, and its members or items, read from the device the first time they are shown.
/// </summary>
/// <param name="pause">The pause it was read in.</param>
/// <param name="variable">The value, as the device sent it.</param>
internal sealed class SwarmDebugVariable(SwarmPause pause, SwarmVariable variable) : IDebugVariable
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
        if (!variable.HasChildren || !pause.IsCurrent)
        {
            return [];
        }

        using var timeout = new CancellationTokenSource(ReadTimeout);
        try
        {
            var children = pause.GetChildrenAsync(variable, timeout.Token).GetAwaiter().GetResult();
            return [.. children.Select(child => (child.Name, (IDebugVariable)new SwarmDebugVariable(pause, child)))];
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The device continued, left or did not answer in time: the row shows no children.
            return [];
        }
    }

    /// <inheritdoc />
    public Task<string> SetValueAsync(string expression, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Values of an app paused on a device of the swarm cannot be changed from the tablet.");
}
