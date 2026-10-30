namespace TabStudio.Agent;

/// <summary>A file the agent wants to create, change or delete.</summary>
/// <param name="FullPath">The file.</param>
/// <param name="RelativePath">The file relative to the workspace root, with <c>/</c>.</param>
/// <param name="Original">The text before the agent touched it, or <see langword="null"/> for a new file.</param>
/// <param name="Proposed">The text the agent wants, or <see langword="null"/> to delete the file.</param>
public sealed record ProposedChange(string FullPath, string RelativePath, string? Original, string? Proposed)
{
    /// <summary>Whether the agent creates the file.</summary>
    public bool IsNew => Original is null && Proposed is not null;

    /// <summary>Whether the agent deletes the file.</summary>
    public bool IsDeletion => Proposed is null;
}

/// <summary>
/// The agent's pending changes: an overlay over the workspace. The agent reads its own changes back as if they
/// were applied, while nothing on disk or in the editors changes until the user approves.
/// </summary>
public sealed class ChangeSet
{
    private readonly Dictionary<string, ProposedChange> _changes = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Raised when a change is added, updated, approved or rejected.</summary>
    public event EventHandler? Changed;

    /// <summary>The pending changes, by path.</summary>
    public IReadOnlyList<ProposedChange> Pending
    {
        get
        {
            lock (_gate)
            {
                return [.. _changes.Values.OrderBy(c => c.RelativePath, StringComparer.Ordinal)];
            }
        }
    }

    /// <summary>The pending files as the workspace should see them: full path to text, or null when deleted.</summary>
    public IReadOnlyDictionary<string, string?> Overlay
    {
        get
        {
            lock (_gate)
            {
                return _changes.ToDictionary(c => c.Key, c => c.Value.Proposed, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>Whether a file has a pending change, and the text it would have.</summary>
    /// <param name="fullPath">The file.</param>
    /// <param name="text">The pending text, or <see langword="null"/> when the change deletes it.</param>
    public bool TryGetPending(string fullPath, out string? text)
    {
        lock (_gate)
        {
            if (_changes.TryGetValue(fullPath, out var change))
            {
                text = change.Proposed;
                return true;
            }
        }

        text = null;
        return false;
    }

    /// <summary>Records new text for a file.</summary>
    /// <param name="fullPath">The file.</param>
    /// <param name="relativePath">The file relative to the root.</param>
    /// <param name="original">Its text before any pending change, or null when it does not exist.</param>
    /// <param name="proposed">The new text, or null to delete it.</param>
    public void Propose(string fullPath, string relativePath, string? original, string? proposed)
    {
        lock (_gate)
        {
            var before = _changes.TryGetValue(fullPath, out var existing) ? existing.Original : original;
            if (string.Equals(before, proposed, StringComparison.Ordinal))
            {
                // Back to where it started: nothing left to approve.
                _changes.Remove(fullPath);
            }
            else
            {
                _changes[fullPath] = new ProposedChange(fullPath, relativePath, before, proposed);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The pending change to a file, if there is one.</summary>
    /// <param name="fullPath">The file.</param>
    public ProposedChange? Find(string fullPath)
    {
        lock (_gate)
        {
            return _changes.GetValueOrDefault(fullPath);
        }
    }

    /// <summary>Removes a change after it was applied or rejected.</summary>
    /// <param name="fullPath">The file.</param>
    /// <returns>The change, or null when there was none.</returns>
    public ProposedChange? Take(string fullPath)
    {
        ProposedChange? change;
        lock (_gate)
        {
            if (!_changes.Remove(fullPath, out change))
            {
                return null;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return change;
    }
}
