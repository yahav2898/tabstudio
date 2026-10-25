using TabStudio.App.Services.Ci;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>What the GitHub Actions window needs from the IDE around it. Implemented by the workspace.</summary>
public interface IActionsHost
{
    /// <summary>The in-app dialog for confirmations.</summary>
    PromptViewModel Prompt { get; }

    /// <summary>The workspace's folder, or <see langword="null"/> before a workspace is open.</summary>
    string? WorkspaceRoot { get; }

    /// <summary>Shows the GitHub Actions window in the side bar.</summary>
    void ShowActions();

    /// <summary>Opens a job's log in a read-only editor tab, or selects its tab when it is open.</summary>
    /// <param name="log">The log to show.</param>
    /// <param name="tabPath">The path the tab is known by; it never exists on disk.</param>
    /// <returns>The log the tab shows: <paramref name="log"/>, or the one of the tab that was already open.</returns>
    CiLogViewModel ShowLog(CiLogViewModel log, string tabPath);

    /// <summary>Shows CI's errors and warnings in the Error List, replacing the previous ones.</summary>
    /// <param name="diagnostics">The diagnostics; none clears them.</param>
    void ShowCiDiagnostics(IReadOnlyList<CiDiagnostic> diagnostics);

    /// <summary>Names the project a file belongs to.</summary>
    /// <param name="fullPath">The file.</param>
    /// <returns>The project's name, or <see langword="null"/>.</returns>
    string? ProjectOf(string fullPath);

    /// <summary>Shows a short message in the status bar.</summary>
    /// <param name="message">The message.</param>
    void ReportStatus(string message);
}
