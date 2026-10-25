using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.SourceControl;

namespace TabStudio.App.ViewModels;

/// <summary>The link to GitHub as of the last status, for features that follow the folder's commit, such as CI.</summary>
public sealed partial class GitChangesViewModel
{
    /// <summary>
    /// The repository, branch and commit the folder is at, after the last refresh; <see langword="null"/> when the
    /// workspace is not linked. It changes after a commit, a pull and a branch switch.
    /// </summary>
    [ObservableProperty]
    public partial GitHubLink? Link { get; private set; }
}
