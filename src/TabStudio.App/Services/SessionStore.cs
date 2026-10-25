using System.Text.Json;

namespace TabStudio.App.Services;

/// <summary>An open document in a saved session.</summary>
/// <param name="Path">Full path of the file.</param>
/// <param name="CaretOffset">Where the caret was.</param>
/// <param name="UnsavedText">The editor's text when it had unsaved changes, otherwise <see langword="null"/>.</param>
public sealed record SessionDocument(string Path, int CaretOffset, string? UnsavedText);

/// <summary>Everything needed to reopen the IDE where the user left it.</summary>
/// <param name="WorkspacePath">The solution, project or folder that was open.</param>
/// <param name="Documents">The open documents, in tab order.</param>
/// <param name="ActiveDocument">The path of the selected tab.</param>
/// <param name="StartupProject">The project file of the startup project.</param>
public sealed record SessionState(
    string WorkspacePath,
    IReadOnlyList<SessionDocument> Documents,
    string? ActiveDocument,
    string? StartupProject);

/// <summary>
/// Saves and restores the IDE session, so TabStudio reopens exactly where the user left it even after
/// Android closed it in the background.
/// </summary>
public sealed class SessionStore
{
    private readonly string _file;
    private readonly Lock _gate = new();

    /// <summary>Creates the store over the session file in the app's state folder.</summary>
    /// <param name="paths">Where the app keeps its state.</param>
    public SessionStore(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _file = paths.SessionFile;
    }

    /// <summary>The last saved session, or <see langword="null"/> when there is none or its workspace is gone.</summary>
    public SessionState? Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_file))
                {
                    return null;
                }

                using var stream = File.OpenRead(_file);
                var state = JsonSerializer.Deserialize(stream, StateJsonContext.Default.SessionState);
                return state is not null && (File.Exists(state.WorkspacePath) || Directory.Exists(state.WorkspacePath)) ? state : null;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>Saves a session, replacing the previous one atomically.</summary>
    /// <param name="state">The session.</param>
    public void Save(SessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_gate)
        {
            try
            {
                var temporary = _file + ".tmp";
                using (var stream = File.Create(temporary))
                {
                    JsonSerializer.Serialize(stream, state, StateJsonContext.Default.SessionState);
                }

                File.Move(temporary, _file, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Saving the session is best effort; the files themselves are saved separately.
            }
        }
    }

    /// <summary>Forgets the session, after the user closed the workspace on purpose.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            try
            {
                File.Delete(_file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Nothing to do: a stale session is ignored once its workspace is gone.
            }
        }
    }
}
