using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The GitHub account dialog: signing in with a personal access token and signing out. One instance serves the
/// start screen, the Git Changes window and the Git menu.
/// </summary>
public sealed partial class GitHubAccountViewModel : ObservableObject, IDisposable
{
    private readonly IStoragePicker _browser;

    /// <summary>Creates the dialog's view model.</summary>
    /// <param name="account">The account.</param>
    /// <param name="browser">Opens GitHub's token page.</param>
    public GitHubAccountViewModel(GitHubAccount account, IStoragePicker browser)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(browser);
        Account = account;
        _browser = browser;
        Account.Changed += OnAccountChanged;
        Update();
    }

    /// <summary>The account.</summary>
    public GitHubAccount Account { get; }

    /// <summary>Whether the dialog is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>The token the user pastes.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string Token { get; set; } = string.Empty;

    /// <summary>Whether GitHub is checking the token.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Why signing in failed, in plain words.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>Whether an account is signed in.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(ButtonText))]
    public partial bool IsSignedIn { get; private set; }

    /// <summary>The signed-in account's user name.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(ButtonText))]
    public partial string? Login { get; private set; }

    /// <summary>The signed-in account's display name, if it has one.</summary>
    [ObservableProperty]
    public partial string? DisplayName { get; private set; }

    /// <summary>"Signed in to GitHub as octocat", or that nobody is.</summary>
    public string StatusText => IsSignedIn ? $"Signed in to GitHub as {Login}" : "Not signed in to GitHub";

    /// <summary>The label of a button that opens this dialog: the account name, or "Sign in to GitHub".</summary>
    public string ButtonText => IsSignedIn ? Login ?? "GitHub" : "Sign in to GitHub";

    /// <summary>Shows the dialog.</summary>
    [RelayCommand]
    public void Open()
    {
        Token = string.Empty;
        ErrorText = null;
        IsOpen = true;
    }

    /// <inheritdoc />
    public void Dispose() => Account.Changed -= OnAccountChanged;

    [RelayCommand]
    private void Close()
    {
        Token = string.Empty;
        IsOpen = false;
    }

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync()
    {
        ErrorText = null;
        IsBusy = true;
        try
        {
            await Account.SignInAsync(Token);
            Token = string.Empty;
            IsOpen = false;
        }
        catch (GitHubApiException ex)
        {
            ErrorText = ex.IsUnauthorized
                ? "GitHub did not accept this token. Check that you copied all of it and that it has not expired."
                : SyncErrors.Describe(ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorText = $"The token could not be saved: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SignOut() => Account.SignOut();

    [RelayCommand]
    private Task CreateTokenAsync() => _browser.OpenUriAsync(GitHubAccount.NewTokenUri);

    private bool CanSignIn() => !IsBusy && !string.IsNullOrWhiteSpace(Token);

    private void OnAccountChanged(object? sender, EventArgs e) => Update();

    private void Update()
    {
        IsSignedIn = Account.IsSignedIn;
        Login = Account.User?.Login;
        DisplayName = Account.User?.Name;
    }
}
