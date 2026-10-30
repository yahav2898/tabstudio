using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels.Actions;
using TabStudio.App.ViewModels.Agent;
using TabStudio.App.ViewModels.Shell;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The activity bar (docs/design/README.md, "Activity bar"): one icon per tool window on the side bar's outer edge,
/// in step with <see cref="SelectedSideBarPanel"/> and <see cref="IsSideBarVisible"/>, with badges that say what is
/// going on in each window without opening it: changed files, CI, failed tests, the agent's proposed changes.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private bool _syncingActivity;

    /// <summary>The activity bar's items, one per tool window, and Extensions at the bottom.</summary>
    public ActivityBarViewModel Activity { get; } = new();

    /// <summary>Fills the activity bar and keeps it in step with the side bar. Called once by the constructor.</summary>
    private void InitializeActivity()
    {
        AddActivity(SideBarPanel.SolutionExplorer, "Solution Explorer", "Icon.Solution", "Ctrl+Alt+L");
        AddActivity(SideBarPanel.GitChanges, "Git Changes", "Icon.Branch");
        AddActivity(SideBarPanel.Actions, "GitHub Actions", "Icon.Actions");
        AddActivity(SideBarPanel.PullRequests, "Pull Requests", "Icon.PullRequest");
        AddActivity(SideBarPanel.TestExplorer, "Test Explorer", "Icon.Flask", "Ctrl+E, T");
        AddActivity(SideBarPanel.Agent, "Claude", "Icon.Agent");
        AddActivity(SideBarPanel.AppPreview, "App Preview", "Icon.Window");
        AddActivity(SideBarPanel.WebPreview, "Web Preview", "Icon.Globe");
        AddActivity(SideBarPanel.Swarm, "Swarm", "Icon.Swarm");
        AddActivity(SideBarPanel.Live, "Live Share", "Icon.Live");
        AddActivity(SideBarPanel.Services, "Services", "Icon.Server");
        AddActivity(SideBarPanel.Containers, "Containers", "Icon.Package");
        AddActivity(SideBarPanel.Database, "Database Explorer", "Icon.Database");
        AddActivity(SideBarPanel.WindowsBridge, "Windows Bridge", "Icon.Computer");
        AddActivity(SideBarPanel.RemoteExplorer, "Remote Explorer (SSH)", "Icon.Remote");
        Activity.Add(new ActivityItem("Extensions", "Extensions", Icon("Icon.Extensions"), "Ctrl+Shift+X")
        {
            IsAtBottom = true,
            IsAvailable = HasExtensions,
            Command = ShowExtensionsCommand,
        });

        SyncActivitySelection();
        Activity.Activated += OnActivityActivated;
        Activity.PropertyChanged += OnActivityPropertyChanged;
        PropertyChanged += OnPropertyChangedForActivity;
        GitChanges.Changes.CollectionChanged += OnBadgeSourceChanged;
        Actions.PropertyChanged += OnBadgeSourceChanged;
        Tests.PropertyChanged += OnBadgeSourceChanged;
        AppPreview.PropertyChanged += OnBadgeSourceChanged;
        WebPreview.PropertyChanged += OnBadgeSourceChanged;
        if (Swarm is { } swarm)
        {
            swarm.PropertyChanged += OnBadgeSourceChanged;
        }

        if (WindowsBridgePane is { } bridge)
        {
            bridge.PropertyChanged += OnBadgeSourceChanged;
        }

        UpdateActivityBadges();
        UpdateLiveActivity();
    }

    /// <summary>Puts the agent's proposed changes and its work on its item. Called once, when the agent is created.</summary>
    /// <param name="agent">The agent's tool window.</param>
    /// <returns>The agent, for the property that creates it.</returns>
    private AgentPanelViewModel ConnectAgentActivity(AgentPanelViewModel agent)
    {
        agent.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentPanelViewModel.IsBusy))
            {
                UpdateAgentBadge(agent);
            }
        };
        agent.Changes.CollectionChanged += (_, _) => UpdateAgentBadge(agent);
        UpdateAgentBadge(agent);
        return agent;
    }

    private static Geometry? Icon(string key) => ThemeBrushes.Find<Geometry>(key);

    private static string Count(int count, string one, string many) =>
        count == 1 ? $"1 {one}" : $"{count.ToString(CultureInfo.CurrentCulture)} {many}";

    private void AddActivity(SideBarPanel panel, string title, string icon, string? gesture = null) =>
        Activity.Add(new ActivityItem(panel.ToString(), title, Icon(icon), gesture));

    private ActivityItem Item(SideBarPanel panel) => Activity.Find(panel.ToString())!;

    /// <summary>A tap on a tool window's icon shows it, as its menu item does; Pull Requests also lists them.</summary>
    private void OnActivityActivated(object? sender, ActivityItem item)
    {
        if (!Enum.TryParse<SideBarPanel>(item.Id, out var panel))
        {
            return;
        }

        if (panel == SideBarPanel.PullRequests)
        {
            ShowPullRequestsCommand.Execute(null);
        }
        else
        {
            SelectSideBarPanel(panel);
        }
    }

    /// <summary>A tap on the shown tool window's icon folds the side bar away, or brings it back.</summary>
    private void OnActivityPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_syncingActivity && e.PropertyName == nameof(ActivityBarViewModel.IsSideBarOpen))
        {
            IsSideBarVisible = Activity.IsSideBarOpen;
        }
    }

    private void OnPropertyChangedForActivity(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SelectedSideBarPanel):
                SyncActivitySelection();
                UpdateActivityBadges();
                break;
            case nameof(IsSideBarVisible):
                _syncingActivity = true;
                Activity.IsSideBarOpen = IsSideBarVisible;
                _syncingActivity = false;
                break;
        }
    }

    /// <summary>Marks the side bar's tool window in the activity bar, leaving the side bar open or folded as it is.</summary>
    private void SyncActivitySelection()
    {
        _syncingActivity = true;
        Activity.Select(SelectedSideBarPanel.ToString());
        Activity.IsSideBarOpen = IsSideBarVisible;
        _syncingActivity = false;
    }

    private void OnBadgeSourceChanged(object? sender, EventArgs e) => UpdateActivityBadges();

    /// <summary>What each tool window's badge says now, and which tool windows have something to show.</summary>
    private void UpdateActivityBadges()
    {
        var changes = GitChanges.Changes.Count;
        Item(SideBarPanel.GitChanges).ShowCount(changes, ActivityTone.Neutral, Count(changes, "changed file", "changed files"));

        var actions = Item(SideBarPanel.Actions);
        actions.IsAvailable = Actions.IsCiVisible || IsActionsSelected;
        switch (Actions.CiVisual)
        {
            case RunVisual.Running or RunVisual.Queued:
                actions.ShowActivity("CI is running on this commit");
                break;
            case RunVisual.Failed:
                actions.ShowDot(ActivityTone.Error, Actions.ChecksSummary is { Length: > 0 } summary ? summary : "CI failed on this commit");
                break;
            default:
                actions.ClearBadge();
                break;
        }

        var tests = Item(SideBarPanel.TestExplorer);
        if (Tests.IsRunning)
        {
            tests.ShowActivity("Running tests");
        }
        else
        {
            tests.ShowCount(Tests.FailedCount, ActivityTone.Error, Count(Tests.FailedCount, "test failed", "tests failed"));
        }

        Item(SideBarPanel.AppPreview).IsAvailable = AppPreview.HasApp || IsAppPreviewSelected;
        Item(SideBarPanel.WebPreview).IsAvailable = WebPreview.HasApp || IsWebPreviewSelected;

        Item(SideBarPanel.WindowsBridge).IsAvailable = WindowsBridgePane is { IsAvailable: true } || IsWindowsBridgeSelected;

        var swarm = Item(SideBarPanel.Swarm);
        swarm.IsAvailable = Swarm is not null;
        if (Swarm is { IsOn: true } on)
        {
            swarm.ShowCount(on.Devices.Count, ActivityTone.Success, Count(on.Devices.Count, "device in the swarm", "devices in the swarm"));
        }
        else
        {
            swarm.ClearBadge();
        }
    }

    private void UpdateAgentBadge(AgentPanelViewModel agent)
    {
        if (Activity.Find(nameof(SideBarPanel.Agent)) is not { } item)
        {
            return;
        }

        if (agent.IsBusy)
        {
            item.ShowActivity("Claude is working");
        }
        else
        {
            item.ShowCount(agent.Changes.Count, ActivityTone.Accent, Count(agent.Changes.Count, "change to review", "changes to review"));
        }
    }
}

