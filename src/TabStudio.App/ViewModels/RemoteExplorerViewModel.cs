using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.Remote;

namespace TabStudio.App.ViewModels;

/// <summary>Hosts, SSH config import and run profiles in the Remote Explorer tool window.</summary>
public sealed partial class RemoteExplorerViewModel : ObservableObject, IDisposable
{
    private readonly RemoteSshServices _services;
    private CancellationTokenSource? _operation;
    public RemoteExplorerViewModel(RemoteSshServices services)
    {
        _services = services;
        Hosts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasHosts));
        try { foreach (var host in services.Hosts.Load()) { Hosts.Add(host); } }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException) { Status = ex.Message; }
    }
    public ObservableCollection<SshHost> Hosts { get; } = [];

    /// <summary>Whether a host is saved; until one is, the form that adds one is open.</summary>
    public bool HasHosts => Hosts.Count > 0;

    /// <summary>Whether a public key was made, to copy to the server.</summary>
    public bool HasPublicKey => PublicKey.Length > 0;

    partial void OnPublicKeyChanged(string value) => OnPropertyChanged(nameof(HasPublicKey));
    public Func<SshHost, string, CancellationToken, Task>? OpenFolder { get; set; }
    public Func<Task>? RefreshFolder { get; set; }
    public Func<CancellationToken, Task>? ReloadFile { get; set; }
    public Func<Task>? ForwardPort { get; set; }
    public Func<Task>? SaveRunProfile { get; set; }
    public Action? Disconnect { get; set; }
    public Func<string?>? CacheRoot { get; set; }
    public Func<string, int, int, Task>? Navigate { get; set; }
    public ObservableCollection<RemoteSearchHit> SearchResults { get; } = [];

    [ObservableProperty] public partial SshHost? SelectedHost { get; set; }
    [ObservableProperty] public partial string HostName { get; set; } = string.Empty;
    [ObservableProperty] public partial string User { get; set; } = Environment.UserName;
    [ObservableProperty] public partial string Alias { get; set; } = string.Empty;
    [ObservableProperty] public partial string Port { get; set; } = "22";
    [ObservableProperty] public partial string IdentityFile { get; set; } = string.Empty;
    [ObservableProperty] public partial string RemoteFolder { get; set; } = "/home";
    [ObservableProperty] public partial string Status { get; set; } = "Choose a host to open a folder over SSH.";
    [ObservableProperty] public partial string PublicKey { get; private set; } = string.Empty;
    [ObservableProperty] public partial string ConfigText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; private set; }
    [ObservableProperty] public partial bool IsConnected { get; set; }
    [ObservableProperty] public partial string BuildScript { get; set; } = "dotnet build";
    [ObservableProperty] public partial string RunScript { get; set; } = "dotnet run --no-launch-profile";
    [ObservableProperty] public partial string PreviewPort { get; set; } = "5000";
    [ObservableProperty] public partial string SearchQuery { get; set; } = string.Empty;
    [ObservableProperty] public partial RemoteSearchHit? SelectedResult { get; set; }

    partial void OnSelectedHostChanged(SshHost? value)
    {
        if (value is null) { return; }
        HostName = value.HostName; User = value.User; Alias = value.Name;
        Port = value.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        IdentityFile = value.IdentityFile ?? string.Empty;
    }

    [RelayCommand]
    private void SaveHost()
    {
        try
        {
            var host = ReadHost();
            var list = Hosts.Where(h => !h.Name.Equals(host.Name, StringComparison.OrdinalIgnoreCase)).Append(host).ToList();
            _services.Hosts.Save(list);
            Hosts.Clear(); foreach (var item in list) { Hosts.Add(item); }
            SelectedHost = host;
            Status = "Host saved. Generate its device key and install the public key on the server before connecting.";
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or IOException or UnauthorizedAccessException) { Status = ex.Message; }
    }

    /// <summary>Removes the selected host and forgets its server's saved key; adding it again asks to verify the key.</summary>
    [RelayCommand]
    private void RemoveHost()
    {
        if (SelectedHost is not { } host)
        {
            Status = "Choose a host to remove.";
            return;
        }

        try
        {
            var list = Hosts.Where(h => !ReferenceEquals(h, host)).ToList();
            _services.Hosts.Save(list);
            _services.Hosts.Forget(host);
            Hosts.Clear(); foreach (var item in list) { Hosts.Add(item); }
            SelectedHost = null;
            Status = $"Removed {host.Name} and forgot its server key. Add it again to verify the server's new key.";
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or IOException or UnauthorizedAccessException) { Status = ex.Message; }
    }

    [RelayCommand]
    private async Task GenerateKeyAsync() => await OperateAsync(async _ =>
    {
        var host = ReadHost();
        using var identity = await Task.Run(() => _services.Identity(host));
        PublicKey = identity.PublicKey;
        Status = PublicKey.Length > 0 ? "Add this public key to the server's ~/.ssh/authorized_keys. The private key stays on this device." : "This host uses its imported IdentityFile.";
    });

    [RelayCommand]
    private async Task ImportConfigAsync() => await OperateAsync(async token =>
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var text = string.IsNullOrWhiteSpace(ConfigText) ? await File.ReadAllTextAsync(Path.Combine(home, ".ssh", "config"), token) : ConfigText;
        var imported = SshConfig.Parse(text, home, Environment.UserName);
        var hosts = Hosts.Concat(imported).GroupBy(h => h.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList();
        _services.Hosts.Save(hosts);
        Hosts.Clear(); foreach (var host in hosts) { Hosts.Add(host); }
        SelectedHost = imported.Count > 0 ? imported[0] : null;
        Status = $"Imported {imported.Count} hosts. Include, Match, ProxyJump and ProxyCommand are not supported; routed hosts are skipped.";
    });

    [RelayCommand]
    private async Task OpenFolderAsync() => await OperateAsync(async token =>
    {
        var host = SelectedHost ?? ReadHost();
        if (OpenFolder is not null) { await OpenFolder(host, RemoteFolder, token); }
    });

    [RelayCommand] private async Task RefreshAsync() => await OperateAsync(async _ => { if (RefreshFolder is not null) { await RefreshFolder(); } });
    [RelayCommand] private async Task ReloadFileAsync() => await OperateAsync(async token => { if (ReloadFile is not null) { await ReloadFile(token); } });
    [RelayCommand] private async Task ForwardAsync() => await OperateAsync(async _ => { if (ForwardPort is not null) { await ForwardPort(); } });
    [RelayCommand] private async Task SaveProfileAsync() => await OperateAsync(async _ => { if (SaveRunProfile is not null) { await SaveRunProfile(); } });
    [RelayCommand] private void DisconnectHost() => Disconnect?.Invoke();
    [RelayCommand] private void Cancel() => _operation?.Cancel();
    [RelayCommand]
    private async Task SearchFilesAsync() => await OperateAsync(async token =>
    {
        var root = CacheRoot?.Invoke() ?? throw new IOException("Open a remote folder first.");
        var results = await RemoteSearch.FindAsync(root, SearchQuery, token);
        SearchResults.Clear(); foreach (var result in results) { SearchResults.Add(result); }
        Status = $"{results.Count} matches in the cached remote source (up to 200). Refresh to include server changes.";
    });
    [RelayCommand]
    private async Task OpenResultAsync()
    {
        if (SelectedResult is { } result && Navigate is not null) { await Navigate(result.FilePath, result.Line, result.Column); }
    }

    public RemoteRunProfile ReadRunProfile()
    {
        int? port = null;
        if (!string.IsNullOrWhiteSpace(PreviewPort))
        {
            if (!int.TryParse(PreviewPort, out var number) || number is < 1 or > 65535) { throw new ArgumentException("Enter a remote port between 1 and 65535, or leave it blank."); }
            port = number;
        }
        return new(BuildScript, RunScript, port);
    }
    private SshHost ReadHost()
    {
        // A typo said "The input string '' was not in a correct format.", and a long number crashed the app.
        if (!int.TryParse(Port.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            throw new ArgumentException("Enter a port between 1 and 65535; SSH usually listens on 22.");
        }

        var host = new SshHost(string.IsNullOrWhiteSpace(Alias) ? HostName.Trim() : Alias.Trim(), HostName.Trim(), User.Trim(), port, string.IsNullOrWhiteSpace(IdentityFile) ? null : IdentityFile);
        host.Validate(); return host;
    }
    private async Task OperateAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) { return; }
        IsBusy = true;
        _operation = new CancellationTokenSource();
        try { await action(_operation.Token); }
        catch (OperationCanceledException) { Status = "Cancelled."; }
        catch (Exception ex) when (ex is IOException or ArgumentException or FormatException or UnauthorizedAccessException or System.Text.Json.JsonException or Renci.SshNet.Common.SshException) { Status = ex.Message; }
        finally { IsBusy = false; _operation.Dispose(); _operation = null; }
    }
    public void Dispose() => _operation?.Cancel();
}
