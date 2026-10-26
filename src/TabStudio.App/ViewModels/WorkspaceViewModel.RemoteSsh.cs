using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.Core.Diagnostics;
using TabStudio.Remote;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.ViewModels;

/// <summary>The SSH workspace: SFTP files, remote commands, shells, web tunnels and server publishing.</summary>
public sealed partial class WorkspaceViewModel
{
    private RemoteSshServices _remoteServices = null!;
    private RemoteWorkspace? _remoteWorkspace;
    private PortForward? _remoteForward;
    private string? _remoteMarker;

    public RemoteExplorerViewModel RemoteExplorer { get; private set; } = null!;
    public bool IsRemoteWorkspace => _remoteMarker is not null;
    public bool IsRemoteExplorerSelected => SelectedSideBarPanel == SideBarPanel.RemoteExplorer;
    public RemoteWorkspace? RemoteWorkspace => _remoteWorkspace;
    internal Task CheckSshDeviceKeyAsync(CancellationToken cancellationToken) => Task.Run(_remoteServices.CheckDeviceKey, cancellationToken);

    private void InitializeRemoteSsh(RemoteSshServices? services)
    {
        _remoteServices = services ?? new RemoteSshServices(_paths);
        RemoteExplorer = new RemoteExplorerViewModel(_remoteServices)
        {
            OpenFolder = OpenRemoteFolderAsync,
            RefreshFolder = RefreshRemoteFolderAsync,
            ReloadFile = ReloadRemoteFileAsync,
            ForwardPort = ForwardRemotePortAsync,
            SaveRunProfile = () =>
            {
                if (Workspace is null || !IsRemoteWorkspace) { throw new IOException("Open a remote folder first."); }
                RemoteExplorer.ReadRunProfile().Save(Workspace.RootDirectory);
                RemoteExplorer.Status = "Run profile saved for this remote folder. F5 runs it on the host.";
                return Task.CompletedTask;
            },
            Disconnect = DisconnectRemote,
            CacheRoot = () => IsRemoteWorkspace ? Workspace?.RootDirectory : null,
            Navigate = NavigateToAsync,
        };
    }

    [RelayCommand]
    private void ShowRemoteExplorer() => SelectSideBarPanel(SideBarPanel.RemoteExplorer);

    /// <summary>Connects and caches the remote source folder, then opens it using the normal editor and explorer.</summary>
    public async Task OpenRemoteFolderAsync(SshHost host, string remoteFolder, CancellationToken cancellationToken = default)
    {
        if (State is not (IdeState.Ready or IdeState.Failed)) { throw new IOException("Wait for the current operation, or stop the running program, before opening another folder."); }
        var before = State;
        State = IdeState.Busy;
        try { await OpenRemoteFolderCoreAsync(host, remoteFolder, cancellationToken); }
        finally { if (State == IdeState.Busy) { State = before; } }
    }

    private async Task OpenRemoteFolderCoreAsync(SshHost host, string remoteFolder, CancellationToken cancellationToken)
    {
        if (State is IdeState.Running or IdeState.Debugging or IdeState.Paused) { throw new IOException("Stop the running program before opening another folder."); }
        remoteFolder = RemoteWorkspace.Normalize(remoteFolder);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(host.KeyId + "\n" + remoteFolder)))[..24];
        var cacheRoot = Path.Combine(_paths.Cache, "SSH", id);
        var reconnecting = IsRemoteWorkspace && Workspace?.RootDirectory == cacheRoot;
        if (!reconnecting && !await SaveAllCoreAsync()) { throw new IOException("Save the current edits before changing folders."); }
        RemoteExplorer.Status = "Connecting to " + host.DisplayName;
        var connection = await ConnectRemoteAsync(host, cancellationToken);
        if (connection is null) { return; }
        var remote = new RemoteWorkspace(connection, remoteFolder, cacheRoot);
        try
        {
            if (!reconnecting) { await remote.RefreshAsync(new Progress<string>(p => RemoteExplorer.Status = "Downloading " + p), cancellationToken); }
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch { remote.Dispose(); throw; }
        // All old documents have been saved. A folder switch must not leave language services or old shells attached.
        if (!reconnecting)
        {
            foreach (var document in Documents.ToList()) { Detach(document); document.Dispose(); }
            Documents.Clear(); ActiveDocument = null;
            _language?.Dispose(); _language = null;
        }
        DisconnectRemote();
        _remoteWorkspace = remote;
        _remoteMarker = host.DisplayName;
        Directory.CreateDirectory(Path.Combine(remote.LocalRoot, ".tabstudio"));
        await File.WriteAllTextAsync(Path.Combine(remote.LocalRoot, ".tabstudio", "remote-workspace.txt"), host.DisplayName, cancellationToken);
        if (!reconnecting) { await LoadAsync(remote.LocalRoot); }
        Terminal.RemoteShellFactory = size => Task.Run(() => new SshRuntime(remote).Start(new SshRuntime(remote).Shell(remote.LocalRoot, size)));
        RemoteExplorer.IsConnected = true;
        RemoteExplorer.SelectedHost = host;
        RemoteExplorer.RemoteFolder = remote.RemoteRoot;
        RemoteExplorer.Status = "Connected to " + host.DisplayName + " · " + remote.RemoteRoot;
        StatusText = RemoteExplorer.Status;
        SelectSideBarPanel(SideBarPanel.SolutionExplorer);
        NotifyRemoteState();
    }

    private async Task<SshConnection?> ConnectRemoteAsync(SshHost host, CancellationToken cancellationToken)
    {
        try { return await _remoteServices.ConnectAsync(host, cancellationToken); }
        catch (SshHostKeyException ex) when (!ex.Changed)
        {
            var confirmed = await Prompt.ConfirmAsync("Trust SSH server?", $"{host.HostName}:{host.Port}\n{ex.Fingerprint}\nCompare this fingerprint with the server administrator's fingerprint before trusting it.", "Trust and connect").WaitAsync(cancellationToken);
            if (!confirmed) { RemoteExplorer.Status = "Connection cancelled; the server key was not trusted."; return null; }
            _remoteServices.Hosts.Trust(host, ex.Fingerprint);
            return await _remoteServices.ConnectAsync(host, cancellationToken);
        }
    }

    private void LoadRemoteState(string root)
    {
        var marker = Path.Combine(root, ".tabstudio", "remote-workspace.txt");
        _remoteMarker = File.Exists(marker) ? File.ReadAllText(marker) : null;
        if (IsRemoteWorkspace)
        {
            var profile = RemoteRunProfile.Load(root);
            RemoteExplorer.BuildScript = profile.BuildCommand;
            RemoteExplorer.RunScript = profile.RunCommand;
            RemoteExplorer.PreviewPort = profile.ForwardPort?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            if (_remoteWorkspace is null) { RemoteExplorer.Status = "Cached remote folder. Reconnect in Remote Explorer to save, build or run."; }
            if (_remoteWorkspace is null) { Terminal.RemoteShellFactory = _ => Task.FromException<ToolchainProcess>(new IOException("Reconnect in Remote Explorer to open a remote shell.")); }
        }
        NotifyRemoteState();
    }

    private async Task RefreshRemoteFolderAsync()
    {
        var remote = _remoteWorkspace ?? throw new IOException("Reconnect to the host first.");
        if (Documents.Any(d => d.IsDirty)) { throw new IOException("Save your edits before refreshing the remote folder."); }
        await remote.RefreshAsync();
        foreach (var document in Documents.ToList()) { Detach(document); document.Dispose(); }
        Documents.Clear(); ActiveDocument = null;
        await ReloadWorkspaceAsync();
        RemoteExplorer.Status = "Remote source cache refreshed.";
    }

    private async Task ReloadRemoteFileAsync(CancellationToken cancellationToken)
    {
        var remote = _remoteWorkspace ?? throw new IOException("Reconnect to the host first.");
        var document = ActiveDocument ?? throw new IOException("Open the file to reload first.");
        if (!await Prompt.ConfirmAsync("Reload " + document.FileName + " from server?", "The editor will load the current server version. Copy any edits you want to keep before continuing. A local recovery copy will also be kept in the source cache's .tabstudio/recovery directory.", "Reload", destructive: true).WaitAsync(cancellationToken)) { return; }
        var recovery = Path.Combine(remote.LocalRoot, ".tabstudio", "recovery");
        Directory.CreateDirectory(recovery);
        await File.WriteAllTextAsync(Path.Combine(recovery, Guid.NewGuid().ToString("N") + "-" + document.FileName), document.CurrentText, cancellationToken);
        await remote.ReloadFileAsync(document.FilePath, cancellationToken);
        var path = document.FilePath;
        Documents.Remove(document); Detach(document); document.Dispose(); ActiveDocument = null;
        await OpenDocumentAsync(path);
        RemoteExplorer.Status = "Loaded the server version. You can reapply your edits and save against this version.";
    }

    private async Task ForwardRemotePortAsync()
    {
        var remote = _remoteWorkspace ?? throw new IOException("Connect to a host first.");
        if (!int.TryParse(RemoteExplorer.PreviewPort, out var port) || port is < 1 or > 65535) { throw new ArgumentException("Enter a remote port between 1 and 65535."); }
        var forward = await Task.Run(() => new PortForward(remote.Connection, port));
        _remoteForward?.Dispose(); _remoteForward = forward;
        WebPreview.Show(remote.Connection.Host.Name + ":" + port, forward.Address, null);
        SelectSideBarPanel(SideBarPanel.WebPreview);
        RemoteExplorer.Status = $"Remote port {port} forwarded to {forward.Address}";
    }

    private void DisconnectRemote()
    {
        if (_remoteWorkspace is not null) { _run?.Cancel(); }
        Terminal.RemoteShellFactory = null;
        Terminal.StopShell();
        _remoteForward?.Dispose(); _remoteForward = null;
        _remoteWorkspace?.Dispose(); _remoteWorkspace = null;
        RemoteExplorer.IsConnected = false;
        RemoteExplorer.Status = "Disconnected. Cached files remain available; reconnect to save or run.";
        if (IsRemoteWorkspace) { WebPreview.ShowStopped(); }
        if (IsRemoteWorkspace) { Terminal.RemoteShellFactory = _ => Task.FromException<ToolchainProcess>(new IOException("Reconnect in Remote Explorer to open a remote shell.")); }
        NotifyRemoteState();
    }

    private void NotifyRemoteState()
    {
        OnPropertyChanged(nameof(IsRemoteWorkspace)); OnPropertyChanged(nameof(RemoteWorkspace));
        OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(RunHostText));
        RunCommand.NotifyCanExecuteChanged(); StartDebuggingCommand.NotifyCanExecuteChanged(); BuildCommand.NotifyCanExecuteChanged();
    }

    private async Task RunRemoteAsync(bool buildOnly)
    {
        if (_remoteWorkspace is not { } remote) { StatusText = "Reconnect in Remote Explorer before running commands."; return; }
        if (!await SaveAllCoreAsync()) { return; }
        Output.Clear(); SelectedPanel = BottomPanel.Output; IsBottomPanelVisible = true;
        State = IdeState.Running; _run = new CancellationTokenSource();
        _runDirectory = remote.LocalRoot;
        try
        {
            var profile = RemoteExplorer.ReadRunProfile();
            profile.Save(remote.LocalRoot);
            var runtime = new SshRuntime(remote);
            async Task<bool> Execute(string script)
            {
                if (string.IsNullOrWhiteSpace(script)) { return true; }
                Output.WriteLine($"[{remote.Connection.Host.Name}] > {script}", OutputKind.Info);
                var result = await OutputProcess.RunAsync(runtime, new ProcessStartSpec { Program = "sh", Arguments = ["-lc", script], WorkingDirectory = remote.LocalRoot }, Output, null, _run.Token);
                var diagnostics = result.Lines.SelectMany(RemoteErrorLocations).Select(location => new BuildDiagnostic("SSH", DiagnosticSeverityLevel.Error, location.Message ?? "Remote build error", new DiagnosticLocation(location.FilePath, 0, 0, location.Line, location.Column), remote.Connection.Host.Name)).ToArray();
                ErrorList.SetFileDiagnostics(remote.LocalRoot, diagnostics);
                Output.WriteLine($"Remote command exited with code {result.Exit?.ExitCode}.", result.Succeeded ? OutputKind.Success : OutputKind.Failure);
                return result.Succeeded;
            }
            var succeeded = await Execute(profile.BuildCommand);
            if (succeeded && !buildOnly)
            {
                if (profile.ForwardPort is not null) { await ForwardRemotePortAsync(); }
                succeeded = await Execute(profile.RunCommand);
            }
            State = succeeded || _run.IsCancellationRequested ? IdeState.Ready : IdeState.Failed;
            StatusText = _run.IsCancellationRequested ? "Remote command stopped" : succeeded ? "Ready" : "Remote command failed";
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or Renci.SshNet.Common.SshException or OperationCanceledException)
        {
            Output.WriteLine(ex.Message, OutputKind.Failure); State = IdeState.Failed; StatusText = ex.Message;
        }
        finally { _run.Dispose(); _run = null; }
    }

    private IEnumerable<RemoteError> RemoteErrorLocations(string line)
    {
        if (_remoteWorkspace is not { } remote) { yield break; }
        var match = Regex.Match(line, @"(?<file>[^\r\n()]+?)\((?<line>\d+),(?<col>\d+)\)\s*:\s*(?<message>(?:error|warning).*|.*)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success) { match = Regex.Match(line, @"(?<file>[^\r\n:]+):(?<line>\d+):(?:(?<col>\d+):)?\s*(?<message>.*)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)); }
        if (match.Success && int.TryParse(match.Groups["line"].Value, out var number) && remote.ToLocalPath(match.Groups["file"].Value.Trim()) is { } path && File.Exists(path))
        {
            yield return new RemoteError(path, number, int.TryParse(match.Groups["col"].Value, out var column) ? column : 1, match.Index, match.Length, match.Groups["message"].Value);
        }
    }
    private sealed record RemoteError(string FilePath, int Line, int Column, int Start, int Length, string? Message);

    [RelayCommand]
    private async Task PublishToServerAsync()
    {
        if (Workspace is not { } workspace || StartupProject is not { } project || !IsWebApp(project) || IsRemoteWorkspace) { StatusText = "Open a local ASP.NET Core project to publish."; return; }
        if (!await SaveAllCoreAsync()) { return; }
        try
        {
            var saved = ServerPublishProfile.Load(project.DirectoryPath);
            ShowRemoteExplorer();
            var hostName = await Prompt.AskAsync("Publish to server", "SSH host alias (configure it in Remote Explorer first)", "Next", saved?.HostName ?? RemoteExplorer.SelectedHost?.Name ?? string.Empty);
            if (hostName is null) { return; }
            var host = _remoteServices.Hosts.Load().FirstOrDefault(h => h.Name == hostName) ?? throw new IOException("Save this host in Remote Explorer before publishing.");
            var destination = await Prompt.AskAsync("Publish destination", "Server application directory. Its current symlink will point at the new release.", "Next", saved?.RemoteDirectory ?? "/srv/my-app");
            if (destination is null) { return; }
            var service = await Prompt.AskAsync("systemd service", "Existing service name. It must run the app from the destination's current directory.", "Next", saved?.ServiceName ?? "my-app.service");
            if (service is null) { return; }
            var rid = await Prompt.AskAsync("Server runtime", "Server .NET runtime identifier, for example linux-x64 or linux-arm64 (linux-musl-x64 for Alpine).", "Next", saved?.RuntimeIdentifier ?? "linux-x64");
            if (rid is null) { return; }
            var profile = new ServerPublishProfile(host.Name, destination, service, rid);
            profile.Validate();
            profile.Save(project.DirectoryPath);
            if (!await Prompt.ConfirmAsync("Publish to " + host.Name + "?", $"Publish {project.Name} locally, upload a new release to {destination}, switch current and restart {service}.", "Publish")) { return; }
            using var connection = await ConnectRemoteAsync(host, CancellationToken.None);
            if (connection is null) { return; }
            State = IdeState.Running; _run = new CancellationTokenSource();
            Output.Clear(); SelectedPanel = BottomPanel.Output; IsBottomPanelVisible = true;
            var output = Path.Combine(_paths.Cache, "Publish", Guid.NewGuid().ToString("N"));
            try
            {
                await EnsurePublishingSdkAsync(_run.Token);
                var args = new List<string> { "publish", project.FilePath, "-c", "Release", "-o", output, "--self-contained", "false", "-p:UseAppHost=false" };
                if (profile.RuntimeIdentifier is { } runtimeId) { args.AddRange(["-r", runtimeId]); }
                var result = await OutputProcess.RunAsync(_toolchains.Runtime, new ProcessStartSpec { Program = "dotnet", Arguments = args, WorkingDirectory = workspace.RootDirectory }, Output, null, _run.Token);
                if (!result.Succeeded) { State = IdeState.Failed; StatusText = "Local publish failed; the server was not changed."; return; }
                await ServerPublisher.UploadAndRestartAsync(connection, output, profile, new Progress<string>(p => Output.WriteLine(p, OutputKind.Info)), _run.Token);
                State = IdeState.Ready; StatusText = "Published to " + host.Name;
            }
            finally { _run.Dispose(); _run = null; if (Directory.Exists(output)) { Directory.Delete(output, recursive: true); } }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or Renci.SshNet.Common.SshException or OperationCanceledException)
        {
            State = IdeState.Failed; StatusText = ex.Message; Output.WriteLine(ex.Message, OutputKind.Failure);
        }
    }

    private async Task EnsurePublishingSdkAsync(CancellationToken cancellationToken)
    {
        if (_toolchains.Runtime.HasCommand("dotnet")) { return; }
        if (!_toolchains.Platform.UsesLinuxEnvironment) { throw new IOException("Install the .NET SDK on this computer so dotnet is on PATH before publishing."); }
        if (!await Prompt.ConfirmAsync("Install .NET SDK for publishing?", "Local dotnet publish needs the full .NET 10 SDK in the tablet's Linux environment (several hundred MB). Install Alpine's dotnet10-sdk package?", "Install")) { throw new OperationCanceledException("SDK installation cancelled."); }
        await _toolchains.Manager.PrepareRuntimeAsync(new Progress<TabStudio.Toolchains.Installation.InstallProgress>(p => Output.WriteLine(p.Message, OutputKind.Info)), cancellationToken);
        var installed = await OutputProcess.RunAsync(_toolchains.Runtime, new ProcessStartSpec { Program = "apk", Arguments = ["add", "--no-cache", "dotnet10-sdk"] }, Output, null, cancellationToken);
        if (!installed.Succeeded || !_toolchains.Runtime.HasCommand("dotnet")) { throw new IOException("The .NET SDK could not be installed in the local Linux environment. See Output for the package manager's error."); }
    }
}
