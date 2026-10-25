using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.App.Services.Agent;
using TabStudio.App.ViewModels.Agent;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The agent in the workspace: its tool window in the side bar, "Fix with Agent" from the Error List and the
/// Compatibility Doctor, what it sees of the workspace, and how its approved changes are applied.
/// </summary>
public sealed partial class WorkspaceViewModel : IAgentWorkspaceHost
{
    /// <summary>One HTTP client for Claude and MCP servers, with room for long answers.</summary>
    private static readonly Lazy<HttpClient> SharedAgentHttp = new(() => new HttpClient { Timeout = TimeSpan.FromMinutes(10) });

    private AgentPanelViewModel? _agent;

    /// <summary>The agent tool window, created the first time it is shown.</summary>
    public AgentPanelViewModel Agent => _agent ??= ConnectAgentActivity(new AgentPanelViewModel(new AgentSettingsStore(_paths), AgentHttp, _ui, new IdeAgentWorkspace(this, _ui)));

    /// <summary>The HTTP client the agent talks to Claude and MCP servers with; tests give it a fake.</summary>
    internal HttpClient AgentHttp { get; init; } = SharedAgentHttp.Value;

    /// <inheritdoc />
    Workspace? IAgentWorkspaceHost.Workspace => Workspace;

    /// <inheritdoc />
    ILanguageService? IAgentWorkspaceHost.Language => _language;

    /// <inheritdoc />
    string? IAgentWorkspaceHost.GetOpenDocumentText(string fullPath) =>
        Documents.FirstOrDefault(d => string.Equals(d.FilePath, fullPath, StringComparison.Ordinal))?.Document.Text;

    /// <summary>
    /// Applies a change the user approved. An open document is changed in its editor, as one step the user can
    /// undo, and saved; other files are written directly. New and deleted files update the Solution Explorer.
    /// </summary>
    async Task IAgentWorkspaceHost.ApplyAgentChangeAsync(string fullPath, string? text)
    {
        var open = Documents.FirstOrDefault(d => string.Equals(d.FilePath, fullPath, StringComparison.Ordinal));
        if (text is null)
        {
            await CloseDocumentsUnderAsync(fullPath, save: false);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            await ReloadWorkspaceAsync();
            return;
        }

        if (open is not null)
        {
            open.Document.Replace(0, open.Document.TextLength, text);
            await SaveDocumentAsync(open);
            return;
        }

        var isNew = !File.Exists(fullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, text);
        if (isNew)
        {
            await ReloadWorkspaceAsync();
        }
        else if (_language?.ContainsDocument(fullPath) == true)
        {
            _language.UpdateText(fullPath, text);
        }
    }

    /// <summary>Shows the agent in the side bar, as View > Claude Agent does.</summary>
    [RelayCommand]
    private void ShowAgent()
    {
        SelectSideBarPanel(SideBarPanel.Agent);
        Agent.FocusInput();
    }

    /// <summary>"Fix with Agent": shows the agent and asks it to fix an error or warning.</summary>
    /// <param name="diagnostic">The problem.</param>
    public Task FixWithAgentAsync(BuildDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        if (ErrorList.LayoutFindingOf(diagnostic) is { } layout)
        {
            return FixLayoutWithAgentAsync(layout);
        }

        SelectSideBarPanel(SideBarPanel.Agent);
        return Agent.AskAsync(DescribeForAgent(diagnostic));
    }

    /// <summary>"Fix with Agent" in the Compatibility Doctor: asks the agent to make a project run on this device.</summary>
    [RelayCommand]
    private async Task FixCompatibilityWithAgentAsync(CompatibilityRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        SelectSideBarPanel(SideBarPanel.Agent);
        await Agent.AskAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"""
            The Compatibility Doctor says the project {row.Project} ({row.Type}) cannot run on this Android tablet as it is: "{row.Status}. {row.Explanation}"
            Find the code that stands in the way, such as APIs that only exist on Windows, and change it so the project builds and runs here, keeping what it does. Explain anything you cannot change.
            """));
    }

    /// <summary>The request "Fix with Agent" sends for a diagnostic, with where it is.</summary>
    private string DescribeForAgent(BuildDiagnostic diagnostic)
    {
        var kind = diagnostic.Severity == DiagnosticSeverityLevel.Error ? "error" : "warning";
        var text = new StringBuilder();
        var code = diagnostic.Id.Length > 0 ? diagnostic.Id + ": " : string.Empty;
        text.Append(CultureInfo.InvariantCulture, $"Fix this {kind}, then check that the project builds:\n{code}{diagnostic.Message}\n");
        if (diagnostic.Location is { } location)
        {
            var root = Workspace?.RootDirectory;
            var file = root is not null && location.FilePath.StartsWith(root, StringComparison.Ordinal)
                ? Path.GetRelativePath(root, location.FilePath).Replace('\\', '/')
                : location.FilePath;
            text.Append(CultureInfo.InvariantCulture, $"In {file}, line {location.Line}, column {location.Column}.\n");
        }

        if (diagnostic.ProjectName is { } project)
        {
            text.Append(CultureInfo.InvariantCulture, $"Project: {project}.\n");
        }

        if (ErrorList.CiSourceOf(diagnostic) is { } ci)
        {
            text.Append(CultureInfo.InvariantCulture, $"CI reported it: the check \"{ci.CheckName}\" on commit {ci.ShortSha}. The file may have changed since that commit.\n");
        }

        return text.ToString().TrimEnd();
    }

    private void InitializeAgent() =>
        ErrorList.FixWithAgentRequested += async (_, diagnostic) => await FixWithAgentAsync(diagnostic);
}
