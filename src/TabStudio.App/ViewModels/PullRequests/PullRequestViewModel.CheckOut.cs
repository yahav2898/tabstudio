using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>Checking out: switching the linked folder to the pull request's branch, so F5 and the Test Explorer run its code.</summary>
public sealed partial class PullRequestViewModel
{
    /// <summary>Whether the changes come from another repository, which cannot be checked out into this folder.</summary>
    public bool IsFromFork => Info?.IsFromFork(Owner, Repository) == true;

    /// <summary>Whether the linked folder is on the pull request's branch.</summary>
    public bool IsCheckedOut => Info is { } info
        && _host.Link is { } link
        && !IsFromFork
        && string.Equals(link.FullName, $"{Owner}/{Repository}", StringComparison.OrdinalIgnoreCase)
        && string.Equals(link.Branch, info.Head.Name, StringComparison.Ordinal);

    /// <summary>What checking out means here, once it is done.</summary>
    public string CheckedOutText => $"This folder is on {HeadBranch}, the pull request's branch: F5 builds and runs its code, and the Test Explorer runs its tests.";

    /// <summary>The check-out button's tooltip: what it does, or why it cannot.</summary>
    public string CheckOutToolTip => IsFromFork
        ? $"The changes come from {Info?.Head.RepositoryFullName ?? "a deleted fork"}, so they cannot be checked out into this folder."
        : $"Switch this folder to {HeadBranch}, to build, run and test the pull request's code.";

    /// <summary>Whether the folder is being switched.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckOutCommand))]
    public partial bool IsCheckingOut { get; private set; }

    [RelayCommand(CanExecute = nameof(CanCheckOut))]
    private async Task CheckOutAsync()
    {
        if (Info is not { } info)
        {
            return;
        }

        ErrorText = null;
        InfoText = null;
        IsCheckingOut = true;
        try
        {
            var error = await _host.CheckOutAsync(info);
            if (error is null)
            {
                InfoText = $"Checked out {info.Head.Name}.";
            }
            else
            {
                ErrorText = error;
            }
        }
        finally
        {
            IsCheckingOut = false;
            OnLinkChanged();
        }
    }

    private bool CanCheckOut() => Info is not null && !IsFromFork && !IsCheckedOut && !IsCheckingOut && _host.Link is not null;
}
