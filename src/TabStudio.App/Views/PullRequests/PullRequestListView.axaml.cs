using Avalonia.Controls;

namespace TabStudio.App.Views.PullRequests;

/// <summary>The Pull Requests window: the repository's pull requests by filter; tapping one opens it.</summary>
public sealed partial class PullRequestListView : UserControl
{
    /// <summary>Creates the window.</summary>
    public PullRequestListView() => InitializeComponent();
}
