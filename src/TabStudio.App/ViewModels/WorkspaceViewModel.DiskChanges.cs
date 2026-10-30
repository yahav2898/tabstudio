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
                if (!document.ChangedOnDisk)
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
                    if (!document.IsDirty && Documents.Contains(document))
                    {
                        document.ReloadFromDisk(content.Text);
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
            var folder = Path.Combine(_paths.Trash, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            var copy = Path.Combine(folder, document.FileName);
            File.Copy(document.FilePath, copy, overwrite: true);
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
