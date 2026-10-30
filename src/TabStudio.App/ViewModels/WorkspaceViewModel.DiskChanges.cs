using System.Globalization;
using TabStudio.App.Services;
using TabStudio.Live.Sessions;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Open documents follow changes other programs make to their files: a formatter, git or a generator run in the
/// Terminal, or another app on a desktop. A clean tab takes the new text; a tab with unsaved edits keeps them, and
/// saving it first puts the other version in TabStudio's trash, so neither is lost.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private bool _followingDiskChanges;

    /// <summary>Reloads the clean documents whose files changed on disk, and says which changed under unsaved edits.</summary>
    internal async Task FollowDiskChangesAsync()
    {
        if (_followingDiskChanges || _disposed || IsRemoteWorkspace || LiveSession is LiveGuest)
        {
            return;
        }

        _followingDiskChanges = true;
        try
        {
            foreach (var document in Documents.Where(FollowsItsFile).ToList())
            {
                // A document being saved looks clean while its own write is under way.
                if (document.IsSaving || !document.ChangedOnDisk)
                {
                    continue;
                }

                if (document.IsDirty)
                {
                    StatusText = $"{document.FileName} changed on disk. Saving puts that version in TabStudio's trash and keeps yours.";
                    continue;
                }

                try
                {
                    var content = await TextFile.ReadAsync(document.FilePath);
                    if (!document.IsDirty && !document.IsSaving && Documents.Contains(document))
                    {
                        document.ReloadFromDisk(content);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // The file is being written or went away; the next look tries again.
                }
            }
        }
        finally
        {
            _followingDiskChanges = false;
        }
    }

    /// <summary>Before saving over a file another program changed, keeps that program's version in TabStudio's trash.</summary>
    /// <returns>Where the other version was kept, or <see langword="null"/> when the file had not changed.</returns>
    private string? KeepOtherVersionBeforeSaving(DocumentViewModel document)
    {
        if (!FollowsItsFile(document) || IsRemoteWorkspace || !document.ChangedOnDisk || !File.Exists(document.FilePath))
        {
            return null;
        }

        try
        {
            // Under the file's path in the workspace, so two files of the same name saved together keep both versions.
            var relative = Workspace is { } workspace ? Path.GetRelativePath(workspace.RootDirectory, document.FilePath) : document.FileName;
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                relative = document.FileName;
            }

            var copy = Path.Combine(_paths.Trash, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture), relative);
            for (var i = 2; File.Exists(copy); i++)
            {
                copy = Path.Combine(Path.GetDirectoryName(copy)!, $"{Path.GetFileNameWithoutExtension(relative)} ({i}){Path.GetExtension(relative)}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(document.FilePath, copy);
            return copy;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool FollowsItsFile(DocumentViewModel document) =>
        !document.IsComparison && !document.IsCiLog && !document.IsExtensionsPage && !document.IsNotebook;

    private void OnAppActivityChangedForDiskChanges(object? sender, EventArgs e)
    {
        if (sender is Platform.IAppActivity { IsInBackground: false })
        {
            _ = FollowDiskChangesAsync();
        }
    }
}
