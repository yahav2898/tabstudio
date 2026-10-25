using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Services;

/// <summary>
/// The GitHub account TabStudio works as. Signing in takes a personal access token, which GitHub checks before
/// it is remembered; every source control operation then gets a <see cref="GitHubClient"/> made with it.
/// </summary>
/// <remarks>
/// <para>
/// The token is kept in a file in <see cref="AppPaths.State"/>. On Android that folder is in the app's private
/// storage, which other apps cannot read; on Linux and macOS the file is readable by the user only. The token
/// is sent to GitHub's API and nowhere else, and signing out deletes the file.
/// </para>
/// <para>
/// Signed out, TabStudio can still open public repositories, with GitHub's lower limit of 60 requests an hour.
/// </para>
/// </remarks>
public sealed class GitHubAccount
{
    /// <summary>
    /// GitHub's page for creating a classic token with the <c>repo</c> scope, named TabStudio: everything the IDE
    /// does with an account in one step.
    /// </summary>
    public static readonly Uri NewTokenUri = new("https://github.com/settings/tokens/new?scopes=repo&description=TabStudio");

    private const string FileName = "github-account.json";

    private static readonly Lazy<HttpClient> SharedHttp = new(() => new HttpClient { Timeout = TimeSpan.FromMinutes(10) });

    private readonly string _file;
    private readonly HttpClient _http;
    private readonly Uri? _apiAddress;
    private string? _token;

    /// <summary>Creates the account and reads the remembered sign-in, if there is one. Nothing is sent to GitHub.</summary>
    /// <param name="paths">Where the token is remembered.</param>
    /// <param name="http">The HTTP client for GitHub's API; a shared one when <see langword="null"/>.</param>
    /// <param name="apiAddress">GitHub's API address; github.com's when <see langword="null"/>. Tests pass a fake's.</param>
    public GitHubAccount(AppPaths paths, HttpClient? http = null, Uri? apiAddress = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _file = Path.Combine(paths.State, FileName);
        _http = http ?? SharedHttp.Value;
        _apiAddress = apiAddress;
        if (Load() is { } stored)
        {
            _token = stored.Token;
            User = new GitHubUser(stored.Login, stored.Name);
        }
    }

    /// <summary>Raised after signing in or out, on the thread that did it.</summary>
    public event EventHandler? Changed;

    /// <summary>The signed-in account, or <see langword="null"/>.</summary>
    public GitHubUser? User { get; private set; }

    /// <summary>Whether a token is remembered.</summary>
    public bool IsSignedIn => _token is not null;

    /// <summary>Creates a client that works as this account, or signed out.</summary>
    public GitHubClient CreateClient() => new(_http, _token, _apiAddress);

    /// <summary>Checks a token with GitHub and, if GitHub accepts it, remembers it.</summary>
    /// <param name="token">A fine-grained or classic personal access token.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The account the token belongs to.</returns>
    /// <exception cref="GitHubApiException">GitHub refused the token or could not be reached.</exception>
    public async Task<GitHubUser> SignInAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var trimmed = token.Trim();
        var user = await new GitHubClient(_http, trimmed, _apiAddress).GetUserAsync(cancellationToken);
        Save(new StoredGitHubAccount(trimmed, user.Login, user.Name));
        _token = trimmed;
        User = user;
        Changed?.Invoke(this, EventArgs.Empty);
        return user;
    }

    /// <summary>Forgets the token.</summary>
    public void SignOut()
    {
        try
        {
            File.Delete(_file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The token is forgotten for this session either way; the next sign-in overwrites the file.
        }

        _token = null;
        User = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private StoredGitHubAccount? Load()
    {
        try
        {
            using var stream = File.OpenRead(_file);
            var stored = JsonSerializer.Deserialize(stream, GitHubAccountJsonContext.Default.StoredGitHubAccount);
            return stored is { Token.Length: > 0, Login.Length: > 0 } ? stored : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void Save(StoredGitHubAccount account)
    {
        var temporary = _file + ".tmp";
        using (var stream = File.Create(temporary))
        {
            RestrictToUser(temporary);
            JsonSerializer.Serialize(stream, account, GitHubAccountJsonContext.Default.StoredGitHubAccount);
        }

        File.Move(temporary, _file, overwrite: true);
    }

    /// <summary>Makes the file readable by the user only, where the file system supports it.</summary>
    private static void RestrictToUser(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // The State folder is private to the app on Android; elsewhere the file keeps the folder's permissions.
        }
    }
}

/// <summary>The remembered sign-in.</summary>
/// <param name="Token">The personal access token.</param>
/// <param name="Login">The account's user name.</param>
/// <param name="Name">The account's display name, if it has one.</param>
internal sealed record StoredGitHubAccount(string Token, string Login, string? Name);

/// <summary>Source-generated JSON for the remembered sign-in.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(StoredGitHubAccount))]
internal sealed partial class GitHubAccountJsonContext : JsonSerializerContext
{
}
