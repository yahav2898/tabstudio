using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;

namespace TabStudio.App.ViewModels;

/// <summary>What brings a missing language server or debug adapter: its toolchain, from the Toolchain Store.</summary>
/// <param name="ToolName">The server or adapter, such as <c>Pyright</c>.</param>
/// <param name="Toolchain">The toolchain that installs it, such as Python.</param>
/// <param name="IsUpdate">Whether the toolchain is installed and its update brings the tool.</param>
/// <param name="DownloadText">How much the install downloads, such as <c>about 48 MB</c>.</param>
public sealed record ToolInstallOffer(string ToolName, ToolchainDefinition Toolchain, bool IsUpdate, string DownloadText)
{
    /// <summary>The verb of the offer: Install, or Update.</summary>
    public string Verb => IsUpdate ? "Update" : "Install";

    /// <summary>The offer's button, such as <c>Install Python</c>.</summary>
    public string ActionText => $"{Verb} {Toolchain.Name}";

    /// <summary>The status bar's button, such as <c>Install Pyright</c>, which stands for the missing server there.</summary>
    public string StatusBarText => IsUpdate ? $"Update {Toolchain.Name}" : $"Install {ToolName}";

    /// <summary>What the offer brings, for its tooltip, such as <c>Pyright comes with Python from the Toolchain Store</c>.</summary>
    public string ToolTip => $"{ToolName} comes with {Toolchain.Name} from the Toolchain Store ({DownloadText})";
}

/// <summary>
/// Language servers and debug adapters from the Toolchain Store: a missing one offers to install the toolchain that
/// brings it (in the status bar, when a command needs IntelliSense, and when F5 needs an adapter), and once a
/// toolchain is installed, the servers of the open files start by themselves.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private readonly HashSet<string> _readyToolchains = new(StringComparer.Ordinal);

    // The status bar's "IntelliSense needs …, which is not installed" message, while it shows.
    private string? _missingServerStatus;

    /// <summary>
    /// Raised on the UI thread when a document's editor must be built again, such as when a language server that was
    /// just installed starts for it: the editor sets up IntelliSense, squiggles and semantic colors when it is built.
    /// </summary>
    public event EventHandler<DocumentViewModel>? EditorRebuildRequested;

    /// <summary>The install that brings the active file's missing language server, for the status bar's button.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLanguageServerText))]
    public partial ToolInstallOffer? ServerInstallOffer { get; private set; }

    /// <summary>Whether the status bar shows <see cref="LanguageServerText"/>; an install offer takes its place.</summary>
    public bool ShowsLanguageServerText => LanguageServerText is not null && ServerInstallOffer is null;

    /// <summary>The status bar's Install button: installs the toolchain that brings the active file's language server.</summary>
    [RelayCommand]
    private Task InstallServerToolchainAsync() => ServerInstallOffer is { } offer ? InstallToolAsync(offer) : Task.CompletedTask;

    /// <summary>Connects language servers to the Store. Called once, when the toolchains are set up.</summary>
    private void InitializeToolchainServers()
    {
        _toolchains.Manager.StatusChanged += OnToolchainStatusChangedForServers;
        PropertyChanged += OnPropertyChangedForToolchainServers;
    }

    /// <summary>Undoes <see cref="InitializeToolchainServers"/>. Called when the toolchains are disposed.</summary>
    private void DisposeToolchainServers()
    {
        _toolchains.Manager.StatusChanged -= OnToolchainStatusChangedForServers;
        PropertyChanged -= OnPropertyChangedForToolchainServers;
        StopAdapterProgram();
    }

    /// <summary>
    /// What installs a missing server or adapter: its package's toolchain, when the Store can install it here and it
    /// is not installed yet, or has an update.
    /// </summary>
    /// <param name="toolName">The server or adapter, such as <c>Pyright</c>.</param>
    /// <param name="package">Its id in the toolchain catalog, such as <c>pyright</c> (its definition's <c>package</c>).</param>
    private ToolInstallOffer? FindToolOffer(string toolName, string? package)
    {
        var manager = _toolchains.Manager;
        var candidate = package == "clangd" && ActiveDocument is { } document
            ? _toolchains.Catalog.ForFile(document.FilePath)
            : package is null ? null : _toolchains.Catalog.ForTool(package);
        if (package is null || !manager.CanInstall || candidate is not { Listed: true } toolchain)
        {
            return null;
        }

        var status = manager.Status(toolchain.Id);
        var isUpdate = status.State == ToolchainState.UpdateAvailable;
        if (status.State == ToolchainState.Unavailable || (status.IsReady && !isUpdate))
        {
            return null;
        }

        var about = status.SizesAreExact ? string.Empty : "about ";
        return new ToolInstallOffer(toolName, toolchain, isUpdate, about + DiskSpace.Format(status.DownloadSize));
    }

    /// <summary>The end of a message about a missing server or adapter: how to get it on this device.</summary>
    private string MissingToolHint(string toolName, string command, string? package)
    {
        if (FindToolOffer(toolName, package) is { } offer)
        {
            return $" {offer.Verb} {offer.Toolchain.Name} from the Toolchain Store to get it.";
        }

        return _toolchains.Manager.CanInstall
            ? $" Install it in the Terminal, so that {command} is on the PATH."
            : $" Install it on this computer, so that {command} is on the PATH.";
    }

    /// <summary>
    /// Offers to install the toolchain that brings a file's missing language server, over the editor, when a command
    /// needs IntelliSense there. Called by <see cref="NoIntelliSenseReason"/>.
    /// </summary>
    /// <returns>The reason for the status bar, or <see langword="null"/> when the Store cannot bring the server.</returns>
    private string? OfferServerInstall(DocumentViewModel document, LanguageServerDefinition missing)
    {
        if (FindToolOffer(missing.Name, missing.Package) is not { } offer)
        {
            return null;
        }

        var language = _languageServers?.Configuration.Registry.FindLanguage(document.FilePath)?.Name ?? document.FileType.DisplayName;
        var key = $"server:{missing.Id}";
        _dismissedNotices.Remove(key);
        ToolchainNotice = Notice(
            key,
            $"{language} IntelliSense needs {missing.Name}",
            $"{missing.Name} comes with {offer.Toolchain.Name} from the Toolchain Store: completion, squiggles and navigation in {language} files. {offer.Verb} it now? It downloads {offer.DownloadText}.",
            offer.ActionText,
            () => InstallToolAsync(offer));
        return $"This needs {missing.Name}, which comes with {offer.Toolchain.Name} from the Toolchain Store.";
    }

    /// <summary>
    /// Offers to install the toolchain that brings a missing debug adapter, when F5 needs it, and installs it when the
    /// user agrees. Called by the debugger before it starts the adapter.
    /// </summary>
    /// <returns>The adapter's program once installed, or <see langword="null"/>.</returns>
    private async Task<string?> InstallMissingAdapterAsync(DebugAdapterDefinition adapter)
    {
        // The adapter by its own name, such as debugpy, rather than its display name, Python (debugpy).
        if (FindToolOffer(adapter.Package ?? adapter.Name, adapter.Package) is not { } offer)
        {
            return null;
        }

        var confirmed = await Prompt.ConfirmAsync(
            $"{offer.Verb} {offer.Toolchain.Name}?",
            $"Debugging needs {offer.ToolName}, which comes with {offer.Toolchain.Name} from the Toolchain Store. {offer.Verb} it now? It downloads {offer.DownloadText}.",
            offer.Verb);
        return confirmed && await InstallToolAsync(offer) ? _languageServerServices.Launcher.ResolveFirst(adapter.Commands) : null;
    }

    /// <summary>Installs or updates the toolchain an offer names, in the Store, and closes the Store once it is ready.</summary>
    /// <returns>Whether the toolchain is ready.</returns>
    private async Task<bool> InstallToolAsync(ToolInstallOffer offer)
    {
        var id = offer.Toolchain.Id;
        StatusText = $"{(offer.IsUpdate ? "Updating" : "Installing")} {offer.Toolchain.Name} for {offer.ToolName}";
        ToolchainStore.Open(id);
        await (offer.IsUpdate ? ToolchainStore.UpdateAsync(id) : ToolchainStore.InstallAsync(id));
        var status = _toolchains.Manager.Status(id);
        if (status.State != ToolchainState.Installed)
        {
            // The Store stays open on the toolchain, with the reason in its row.
            StatusText = status.Error is null ? "Ready" : $"{offer.Toolchain.Name} was not installed";
            return false;
        }

        ToolchainStore.CloseCommand.Execute(null);
        StatusText = $"{offer.Toolchain.Name} is installed, with {offer.ToolName}";
        return true;
    }

    private void OnToolchainStatusChangedForServers(object? sender, ToolchainStatus status) => _ui.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        if (!status.IsReady)
        {
            _readyToolchains.Remove(status.Toolchain.Id);
        }
        else if (_readyToolchains.Add(status.Toolchain.Id))
        {
            StartInstalledServers();
        }

        UpdateServerInstallOffer();
    });

    private void OnPropertyChangedForToolchainServers(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Workspace) when Workspace is { } workspace:
                // Servers and adapters run in the workspace, so it must be visible to the toolchains' programs.
                _toolchains.ShareFolder(workspace.RootDirectory);
                break;
            case nameof(RunHostText):
                OnPropertyChanged(nameof(HasRunHost));
                break;
            case nameof(ActiveDocument) or nameof(LanguageServerText):
                UpdateServerInstallOffer();
                OnPropertyChanged(nameof(ShowsLanguageServerText));
                break;
        }
    }

    /// <summary>Starts the servers that were missing for open documents, now that a toolchain brought them.</summary>
    private void StartInstalledServers()
    {
        if (_languageServers is not { } host)
        {
            return;
        }

        var started = false;

        foreach (var document in Documents.ToList())
        {
            if (document.HasLanguageService || document.HasLanguageServer || document.IsNotebook || document.IsCiLog || document.IsComparison
                || host.FindServer(document.FilePath) is null)
            {
                continue;
            }

            AttachLanguageServer(document);
            if (document.HasLanguageServer)
            {
                LanguageServerOutput.WriteLine($"[{document.LanguageServer!.ServerName}] Installed: starting it for {document.FileName}.", OutputKind.Info);
                EditorRebuildRequested?.Invoke(this, document);
                started = true;
            }
        }

        // However it was installed (the Store, F5's offer or the editor's), the status bar no longer says it is missing.
        if (started && _missingServerStatus is { } missing && StatusText == missing)
        {
            StatusText = "Ready";
        }

        if (started)
        {
            _missingServerStatus = null;
        }

        UpdateLanguageServerText();
    }

    private void UpdateServerInstallOffer() =>
        ServerInstallOffer = ActiveDocument is { HasLanguageService: false, HasLanguageServer: false } document
            && _languageServers?.FindMissingServers(document.FilePath) is [{ } missing, ..]
            ? FindToolOffer(missing.Name, missing.Package)
            : null;
}
