using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.SourceControl;

namespace TabStudio.App.ViewModels;

/// <summary>Fetch, pull, sync, publishing a folder to GitHub, and opening the repository's page.</summary>
public sealed partial class GitChangesViewModel
{
    /// <summary>
    /// What a published folder's <c>.gitignore</c> starts with when it has none: build output, IDE files, and
    /// TabStudio's own sync folder, so Git on a computer leaves it alone too.
    /// </summary>
    internal const string DefaultGitIgnore = "bin/\nobj/\n.vs/\n*.user\n.tabstudio/\n";

    /// <summary>The name of the repository "Publish to GitHub" creates.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    public partial string PublishName { get; set; } = string.Empty;

    /// <summary>Whether the published repository is private.</summary>
    [ObservableProperty]
    public partial bool PublishPrivate { get; set; } = true;

    /// <summary>Asks GitHub how many commits the folder lacks, without changing any file.</summary>
    [RelayCommand(CanExecute = nameof(CanUseGitHub))]
    private async Task FetchAsync()
    {
        if (!await RunAsync((_, _, _) => Task.FromResult<string?>(null), checkGitHub: true))
        {
            return;
        }

        InfoText = IncomingCommits switch
        {
            null => $"{BranchName} is a commit, not a branch, so there is nothing to fetch.",
            0 => $"Up to date with {BranchName} on GitHub.",
            1 => $"GitHub has 1 new commit on {BranchName}. Pull to get it.",
            int count => $"GitHub has {count.ToString(CultureInfo.CurrentCulture)} new commits on {BranchName}. Pull to get them.",
        };
        _host.ReportStatus(InfoText);
    }

    [RelayCommand(CanExecute = nameof(CanUseGitHub))]
    private async Task PullAsync()
    {
        if (await SaveBeforeChangingFilesAsync())
        {
            await PullCoreAsync();
        }
    }

    /// <summary>Pulls, then commits and pushes the changes when there is a commit message, as Visual Studio's Sync does.</summary>
    [RelayCommand(CanExecute = nameof(CanUseGitHub))]
    private async Task SyncAsync()
    {
        if (!await SaveBeforeChangingFilesAsync() || !await PullCoreAsync() || HasConflicts || !HasChanges)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            InfoText = $"{InfoText} Enter a commit message to push your changes.";
            return;
        }

        await CommitCoreAsync();
    }

    /// <summary>
    /// Links the Git clone that holds the workspace to its GitHub repository, comparing its files with the commit it
    /// holds instead of downloading it again. A private repository's clone needs the owner's account signed in.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLinkCheckout))]
    private async Task LinkCheckoutAsync()
    {
        if (Checkout is not { } checkout || _disposed)
        {
            return;
        }

        ErrorText = null;
        InfoText = null;
        IsBusy = true;
        ProgressText = $"Linking to {checkout.FullName}";
        try
        {
            await _gate.WaitAsync(_lifetime.Token);
            try
            {
                var sync = await GitHubSync.LinkCheckoutAsync(Account.Account.CreateClient(), checkout, _lifetime.Token);
                _sync = sync;
                IsLinked = true;
                Checkout = null;
                await RefreshCoreAsync(checkGitHub: false);
            }
            catch (Exception ex) when (IsExpected(ex) || ex is HttpRequestException)
            {
                ErrorText = ex is HttpRequestException
                    ? $"GitHub could not be reached to link {checkout.FullName}. Check the connection and tap Link to GitHub."
                    : SyncErrors.Describe(ex);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        finally
        {
            IsBusy = false;
            ProgressText = null;
        }
    }

    private bool CanLinkCheckout() => Checkout is not null && !IsLinked && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private async Task PublishAsync()
    {
        if (!Account.IsSignedIn)
        {
            ErrorText = "Sign in to GitHub to publish this folder.";
            Account.Open();
            return;
        }

        var name = PublishName.Trim();
        if (!RepositoryNamePattern().IsMatch(name))
        {
            ErrorText = "A repository name can use letters, digits, '.', '-' and '_', up to 100 characters.";
            return;
        }

        if (_workspaceRoot is not { } root || !await SaveBeforeChangingFilesAsync())
        {
            return;
        }

        ErrorText = null;
        InfoText = null;
        IsBusy = true;
        ProgressText = "Creating the repository";
        var progress = new Progress<SyncProgress>(p => ProgressText = DescribeProgress(p));
        try
        {
            await _gate.WaitAsync(_lifetime.Token);
            try
            {
                var gitignore = Path.Combine(root, ".gitignore");
                if (!File.Exists(gitignore))
                {
                    await File.WriteAllTextAsync(gitignore, DefaultGitIgnore, _lifetime.Token);
                }

                var isPrivate = PublishPrivate;
                var sync = await GitHubSync.PublishAsync(Account.Account.CreateClient(), root, name, isPrivate, "Add project files", progress, _lifetime.Token);
                _sync = sync;
                IsLinked = true;
                IncomingCommits = 0;
                await RefreshCoreAsync(checkGitHub: false);
                InfoText = $"Published to github.com/{sync.Link.FullName} as a {(isPrivate ? "private" : "public")} repository.";
                _host.ReportStatus(InfoText);
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                ShowError(ex);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        finally
        {
            IsBusy = false;
            ProgressText = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsLinked))]
    private async Task OpenOnGitHubAsync()
    {
        if (_sync?.Link is { } link)
        {
            var address = link.Branch is { } branch
                ? new Uri($"{link.WebUri}/tree/{string.Join('/', branch.Split('/').Select(Uri.EscapeDataString))}")
                : link.WebUri;
            await _browser.OpenUriAsync(address);
        }
    }

    /// <summary>Pulls GitHub's commits into the folder and brings open documents and the workspace up to date.</summary>
    private Task<bool> PullCoreAsync() => RunAsync(async (sync, progress, token) =>
    {
        var before = await _host.CaptureDocumentsAsync();
        var result = await sync.PullAsync(progress, token);
        IncomingCommits = 0;
        if (result.WasUpToDate)
        {
            return $"Already up to date with {sync.Link.Branch} on GitHub.";
        }

        var typedConflicts = await _host.ReloadDocumentsAsync(FullPaths(result.Updated.Concat(result.Conflicts)), before);
        await _host.ReloadWorkspaceAsync();
        var files = result.Updated.Count == 1 ? "1 file" : $"{result.Updated.Count.ToString(CultureInfo.CurrentCulture)} files";
        var message = result.Conflicts.Count switch
        {
            0 => $"Pulled {sync.Link.Branch} from GitHub: {files} updated.",
            1 => $"Pulled {sync.Link.Branch} from GitHub. 1 file has conflicting changes: resolve it under Merge Conflicts.",
            var count => $"Pulled {sync.Link.Branch} from GitHub. {count.ToString(CultureInfo.CurrentCulture)} files have conflicting changes: resolve them under Merge Conflicts.",
        };
        return message + TypedEditsNote(typedConflicts);
    });

    /// <summary>The sentence a Git operation's message ends with when edits typed while it ran conflict with it.</summary>
    private static string TypedEditsNote(int conflicts) => conflicts switch
    {
        0 => string.Empty,
        1 => " What you typed meanwhile conflicts with it in 1 open document: look for <<<<<<< Yours, keep what you want and save.",
        _ => $" What you typed meanwhile conflicts with it in {conflicts.ToString(CultureInfo.CurrentCulture)} open documents: look for <<<<<<< Yours, keep what you want and save.",
    };

    private bool CanUseGitHub() => IsLinked && !IsBusy;

    private bool CanPublish() => HasWorkspace && !IsLinked && !IsBusy && !string.IsNullOrWhiteSpace(PublishName);

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryNamePattern();
}
