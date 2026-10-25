namespace TabStudio.App.Services;

/// <summary>
/// Language features whose code actions may have work left after their edits are applied, such as a language
/// server's command that runs once the edited text reached it.
/// </summary>
public interface ICodeActionFollowUp
{
    /// <summary>Finishes a code action after the editor applied its edits.</summary>
    /// <param name="id">The action's <see cref="TabStudio.Compilation.Features.CodeActionItem.Id"/>.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    Task RunAfterEditsAsync(string id, CancellationToken cancellationToken);
}
