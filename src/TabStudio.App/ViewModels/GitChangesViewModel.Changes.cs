using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.SourceControl;
using TabStudio.SourceControl.Merging;

namespace TabStudio.App.ViewModels;

/// <summary>Committing, comparing, undoing and ignoring changes, and settling merge conflicts.</summary>
public sealed partial class GitChangesViewModel
{
    /// <summary>The message of the last commit, which a new pull request starts with.</summary>
    private string? _lastCommitMessage;

    /// <inheritdoc />
    public Task OpenAsync(GitChangeItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.CanOpen ? _host.OpenFileAsync(item.FullPath) : CompareAsync(item);
    }

    /// <inheritdoc />
    public async Task CompareAsync(GitChangeItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_sync is not { } sync)
        {
            return;
        }

        ErrorText = null;
        try
        {
            var unmodified = item.Status == GitFileStatus.Added ? null : await sync.GetBaseContentAsync(item.RelativePath, _lifetime.Token);
            var current = File.Exists(item.FullPath) ? await File.ReadAllBytesAsync(item.FullPath, _lifetime.Token) : null;
            if (!FileComparison.TryReadText(unmodified, out var before) || !FileComparison.TryReadText(current, out var after))
            {
                ErrorText = $"{item.Name} is a binary file, so its changes cannot be shown line by line.";
                return;
            }

            var label = $"{sync.Link.Branch ?? "commit"} @ {sync.Link.BaseCommit[..Math.Min(7, sync.Link.BaseCommit.Length)]}";
            var diff = await Task.Run(() => InlineDiff.Create(before, after), _lifetime.Token);
            _host.ShowComparison(new FileComparison(item.FullPath, item.RelativePath, label, diff));
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            ShowError(ex);
        }
    }

    /// <inheritdoc />
    public async Task UndoChangesAsync(GitChangeItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var message = item.Status switch
        {
            GitFileStatus.Added => "The file is new, so undoing its changes deletes it.",
            GitFileStatus.Deleted => "The file comes back as it is on GitHub.",
            _ => "Your changes to it are lost: it goes back to the version from GitHub.",
        };
        if (!await _host.Prompt.ConfirmAsync($"Undo changes to {item.Name}?", message, "Undo Changes", destructive: true))
        {
            return;
        }

        await RunAsync(async (sync, _, token) =>
        {
            await sync.DiscardAsync([item.RelativePath], token);
            await _host.ReloadDocumentsAsync([item.FullPath]);
            await _host.ReloadWorkspaceAsync();
            return $"Undid the changes to {item.Name}.";
        });
    }

    /// <inheritdoc />
    public async Task IgnoreAsync(GitChangeItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.CanIgnore)
        {
            return;
        }

        await RunAsync(async (sync, _, token) =>
        {
            var path = Path.Combine(sync.Root, ".gitignore");
            var existing = File.Exists(path) ? await File.ReadAllTextAsync(path, token) : string.Empty;
            var separator = existing.Length == 0 || existing.EndsWith('\n') ? string.Empty : "\n";
            await File.WriteAllTextAsync(path, existing + separator + IgnoreRule(item.RelativePath) + "\n", token);
            await _host.ReloadDocumentsAsync([path]);
            return $"Added {item.Name} to .gitignore.";
        });
    }

    /// <inheritdoc />
    public async Task ResolveAsync(GitChangeItemViewModel item, ConflictResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(item);

        // The editor may hold the user's fix, or changes the resolution replaces: either way, save first.
        if (!await _host.SaveDocumentsAsync())
        {
            ErrorText = "Some documents could not be saved, so the conflict was left as it is.";
            return;
        }

        if (resolution == ConflictResolution.MarkResolved && await HasConflictMarkersAsync(item.FullPath))
        {
            ErrorText = $"{item.Name} still has conflict markers ({TextMerge.OursMarker}, {TextMerge.SeparatorMarker}, {TextMerge.TheirsMarker}). Edit them out, or choose Keep Mine or Take GitHub's.";
            return;
        }

        await RunAsync(async (sync, _, token) =>
        {
            await sync.ResolveAsync(item.RelativePath, resolution, token);
            if (resolution != ConflictResolution.MarkResolved)
            {
                await _host.ReloadDocumentsAsync([item.FullPath]);
                await _host.ReloadWorkspaceAsync();
            }

            return resolution switch
            {
                ConflictResolution.KeepMine => $"Kept your version of {item.Name}.",
                ConflictResolution.TakeTheirs => $"Took GitHub's version of {item.Name}.",
                _ => $"Marked {item.Name} as resolved.",
            };
        });
    }

    [RelayCommand(CanExecute = nameof(CanCommit))]
    private async Task CommitAsync()
    {
        if (await SaveBeforeChangingFilesAsync())
        {
            await CommitCoreAsync();
        }
    }

    /// <summary>Commits the checked files, or every changed file when none is checked, and pushes them.</summary>
    private async Task<bool> CommitCoreAsync()
    {
        var message = CommitMessage.Trim();
        if (message.Length == 0)
        {
            ErrorText = "Enter a commit message.";
            return false;
        }

        var paths = SelectedCount > 0 ? Changes.Where(c => c.IsSelected).Select(c => c.RelativePath).ToList() : null;
        var committed = await RunAsync(async (sync, progress, token) =>
        {
            var result = await sync.CommitAsync(message, paths, progress, token);
            CommitMessage = string.Empty;
            _lastCommitMessage = message;

            // GitHub took the commit on top of the branch's head, so nothing is incoming any more.
            IncomingCommits = 0;
            var files = result.Files == 1 ? "1 file" : $"{result.Files.ToString(CultureInfo.CurrentCulture)} files";
            return $"Committed {result.Commit.ShortSha} ({files}) and pushed it to {sync.Link.Branch} on GitHub.";
        });

        if (committed)
        {
            await FindPullRequestAsync();
        }

        return committed;
    }

    private bool CanCommit() => IsLinked && !IsBusy && !IsDetached && Changes.Count > 0 && !string.IsNullOrWhiteSpace(CommitMessage);

    /// <summary>Saves open documents, so what is committed or replaced is what the user sees.</summary>
    private async Task<bool> SaveBeforeChangingFilesAsync()
    {
        if (await _host.SaveDocumentsAsync())
        {
            return true;
        }

        ErrorText = "Some documents could not be saved. Save them, then try again.";
        return false;
    }

    private static async Task<bool> HasConflictMarkersAsync(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var content = await File.ReadAllBytesAsync(path);
        return FileComparison.TryReadText(content, out var text) && TextMerge.HasConflictMarkers(text);
    }

    /// <summary>A <c>.gitignore</c> line that matches exactly one file, relative to the repository root.</summary>
    private static string IgnoreRule(string relativePath)
    {
        var rule = new StringBuilder("/");
        foreach (var c in relativePath)
        {
            if (c is '\\' or '*' or '?' or '[')
            {
                rule.Append('\\');
            }

            rule.Append(c);
        }

        // Trailing spaces are dropped from a pattern up to the first escaped one.
        if (rule[rule.Length - 1] == ' ')
        {
            rule.Length--;
            rule.Append("\\ ");
        }

        return rule.ToString();
    }
}
