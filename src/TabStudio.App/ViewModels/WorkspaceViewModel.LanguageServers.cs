using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services.LanguageServers;
using TabStudio.Compilation.Features;
using TabStudio.Core.Workspaces;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.Hosting;

namespace TabStudio.App.ViewModels;

/// <summary>A source the Output panel can show, such as the build or the language servers' log.</summary>
/// <param name="Name">The name in the panel's "Show output from" box.</param>
/// <param name="Output">The text.</param>
public sealed record OutputSourceOption(string Name, OutputViewModel Output);

/// <summary>
/// Language servers for every file Roslyn does not own: Python, TypeScript and JavaScript, C and C++, Rust, Go and
/// Java, as the registry and the workspace's <c>.tabstudio/settings.json</c> set them up. A server starts the first
/// time a file of its language opens and serves the editor's IntelliSense, squiggles and the Error List; its log is
/// in the Output panel under "Language Servers", and its state in the status bar.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private LanguageServerServices _languageServerServices = null!;
    private LanguageServerHost? _languageServers;

    /// <summary>The language servers' log: what TabStudio sent and what the servers said.</summary>
    public OutputViewModel LanguageServerOutput => field ??= new OutputViewModel(_ui, _profile.OutputCharacterLimit);

    /// <summary>The debug adapters' own log, apart from the debugged program's output.</summary>
    public OutputViewModel DebugAdapterOutput => field ??= new OutputViewModel(_ui, _profile.OutputCharacterLimit);

    /// <summary>
    /// The full log of setting projects up, such as everything <c>pip install</c> printed. Build and Run shows what
    /// a person acts on, one message when an install fails; the log is one tap away, here.
    /// </summary>
    public OutputViewModel ProjectSetupOutput => field ??= new OutputViewModel(_ui, _profile.OutputCharacterLimit);

    /// <summary>The sources of the Output panel's "Show output from" box.</summary>
    public IReadOnlyList<OutputSourceOption> OutputSources => field ??=
    [
        new OutputSourceOption("Build and Run", Output),
        new OutputSourceOption("Project Setup", ProjectSetupOutput),
        new OutputSourceOption("Language Servers", LanguageServerOutput),
        new OutputSourceOption("Debug Adapters", DebugAdapterOutput),
        new OutputSourceOption("Swarm", SwarmOutput),
    ];

    /// <summary>The source the Output panel shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShownOutput))]
    public partial OutputSourceOption? SelectedOutputSource { get; set; }

    /// <summary>What the Output panel shows: the selected source's text.</summary>
    public OutputViewModel ShownOutput => SelectedOutputSource?.Output ?? Output;

    /// <summary>The active file's language server and its state, for the status bar, such as "Pyright ready".</summary>
    [ObservableProperty]
    public partial string? LanguageServerText { get; private set; }

    /// <summary>The workspace's language servers, once a workspace is open.</summary>
    internal LanguageServerHost? LanguageServers => _languageServers;

    /// <summary>Tells the language servers about files the Solution Explorer created, renamed or deleted.</summary>
    /// <param name="changes">Each file or folder and what happened to it.</param>
    private void NotifyLanguageServersOfFiles(params (string FilePath, FileChangeKind Kind)[] changes)
    {
        if (_languageServers is { } host)
        {
            _ = host.NotifyFilesChangedAsync(changes);
        }
    }

    /// <summary>Shows the language servers' log in the Output panel, as tapping the status bar item does.</summary>
    [RelayCommand]
    private void ShowLanguageServerLog() => ShowOutputSource(LanguageServerOutput);

    /// <summary>Shows the project setup's full log, such as pip's output, in the Output panel.</summary>
    [RelayCommand]
    private void ShowProjectSetupLog() => ShowOutputSource(ProjectSetupOutput);

    /// <summary>Go to Declaration: where the symbol at the caret is declared, as its language server tells.</summary>
    [RelayCommand]
    private Task GoToDeclarationAsync() => GoToServerLocationAsync(typeDefinition: false);

    /// <summary>Go to Type Definition: where the type of the symbol at the caret is defined.</summary>
    [RelayCommand]
    private Task GoToTypeDefinitionAsync() => GoToServerLocationAsync(typeDefinition: true);

    /// <summary>Sets up language servers. Called once, by the constructor.</summary>
    /// <param name="services">The app's language server services; the desktop's launcher when <see langword="null"/>.</param>
    /// <param name="browser">Opens the pages that explain diagnostics; the system browser when <see langword="null"/>.</param>
    private void InitializeLanguageServers(LanguageServerServices? services, IStoragePicker? browser)
    {
        _languageServerServices = services ?? new LanguageServerServices(ProcessServerLauncher.Instance);
        SelectedOutputSource = OutputSources[0];
        PropertyChanged += OnPropertyChangedForLanguageServers;
        var pages = browser ?? new TopLevelStoragePicker();
        ErrorList.HelpLinkRequested += async (_, link) => await pages.OpenUriAsync(link);
        InitializeDebugAdapters();
    }

    /// <summary>Opens a document on its language server, before its editor is created. Called by <see cref="Attach"/>.</summary>
    private void AttachLanguageServer(DocumentViewModel document)
    {
        if (_languageServers is not { } host || document.HasLanguageService || document.HasXamlFeatures || document.IsNotebook || document.IsCiLog || document.IsComparison)
        {
            return;
        }

        if (host.OpenDocument(document.FilePath, document.Document.Text) is { } server)
        {
            document.AttachLanguageServer(server, _ui, FolderName(server.RootDirectory), ReadText);
            return;
        }

        if (host.FindMissingServers(document.FilePath) is [{ } missing, ..] && host.Configuration.Registry.FindLanguage(document.FilePath) is { } language)
        {
            var install = MissingToolHint(missing.Name, missing.Command, missing.Package);
            StatusText = _missingServerStatus = $"{language.Name} IntelliSense needs {missing.Name}, which is not installed.{install}";
            LanguageServerOutput.WriteLine($"[{missing.Name}] {missing.Command} was not found, so {document.FileName} has no IntelliSense.{install}", OutputKind.Info);
        }
    }

    /// <summary>Tells the server a document was saved, and reloads the settings when they were. Called after a save.</summary>
    private void OnDocumentSavedForLanguageServer(DocumentViewModel document)
    {
        document.NotifySavedToServer();
        if (_languageServers is not { } host || Workspace is not { } workspace)
        {
            return;
        }

        var isSettings = string.Equals(document.FilePath, WorkspaceConfiguration.SettingsPath(workspace.RootDirectory), StringComparison.Ordinal);
        var isLaunch = string.Equals(document.FilePath, WorkspaceConfiguration.LaunchPath(workspace.RootDirectory), StringComparison.Ordinal);
        if (isSettings || isLaunch)
        {
            var configuration = WorkspaceConfiguration.Load(workspace.RootDirectory);
            ReportConfigurationProblems(configuration);
            _ = host.UpdateConfigurationAsync(configuration);
            LanguageServerOutput.WriteLine($"Reloaded .tabstudio/{Path.GetFileName(document.FilePath)}.", OutputKind.Info);
            if (isLaunch)
            {
                SelectedLaunchTarget = null;
            }

            OnLaunchTargetChanged();
        }
    }

    /// <summary>Shuts the language servers down. Called by <see cref="Dispose"/>.</summary>
    private void DisposeLanguageServers()
    {
        PropertyChanged -= OnPropertyChangedForLanguageServers;
        if (Interlocked.Exchange(ref _languageServers, null) is { } host)
        {
            _ = host.DisposeAsync().AsTask();
        }
    }

    /// <summary>The message for a command that needs IntelliSense in a file without it.</summary>
    private string NoIntelliSenseReason(DocumentViewModel document)
    {
        if (_languageServers?.FindMissingServers(document.FilePath) is [{ } missing, ..])
        {
            return OfferServerInstall(document, missing) ?? $"This needs {missing.Name}, which is not installed.";
        }

        return _languageServers?.Configuration.Registry.FindLanguage(document.FilePath) is { } language
            ? $"No language server is set up for {language.Name}."
            : "This works in C# files of the solution and in files with a language server.";
    }

    /// <summary>
    /// Shows what a rename changes before it is applied, when it changes more than the file being edited, as
    /// Visual Studio's rename preview does.
    /// </summary>
    /// <returns><see langword="true"/> to apply it.</returns>
    private async Task<bool> PreviewServerRenameAsync(DocumentViewModel document, string oldName, string newName, IReadOnlyList<DocumentEdit> edits)
    {
        if (!document.HasLanguageServer || edits.All(e => string.Equals(e.FilePath, document.FilePath, StringComparison.Ordinal)))
        {
            return true;
        }

        return await Prompt.ConfirmAsync(
            "Preview Changes - Rename",
            $"Rename '{oldName}' to '{newName}': {LspMapping.Summarize(edits)}.",
            "Apply");
    }

    private void OnPropertyChangedForLanguageServers(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Workspace):
                StartLanguageServers(Workspace);
                break;
            case nameof(ActiveDocument):
                UpdateLanguageServerText();
                break;
            case nameof(State) when State is IdeState.Busy or IdeState.Running or IdeState.Debugging:
                // A build or a run writes to its own output: show it, as Visual Studio does.
                SelectedOutputSource = OutputSources[0];
                break;
        }
    }

    private void StartLanguageServers(Workspace? workspace)
    {
        if (Interlocked.Exchange(ref _languageServers, null) is { } previous)
        {
            _ = previous.DisposeAsync().AsTask();
        }

        if (workspace is null)
        {
            return;
        }

        var configuration = WorkspaceConfiguration.Load(workspace.RootDirectory);
        var host = new LanguageServerHost(configuration, _languageServerServices.Launcher, _languageServerServices.CreateHostOptions(ReadText));
        host.LogReceived += OnLanguageServerLog;
        host.ServersChanged += (_, _) => _ui.Post(UpdateLanguageServerText);
        host.DiagnosticsPublished += OnLanguageServerDiagnostics;
        host.MessageShown += OnLanguageServerMessage;
        host.ProgressChanged += OnLanguageServerProgress;
        host.ApplyEditHandler = ApplyServerEditAsync;
        host.ShowDocumentHandler = ShowServerDocumentAsync;
        host.MessageRequestHandler = AnswerServerMessageAsync;
        _languageServers = host;
        ReportConfigurationProblems(configuration);
    }

    private void ReportConfigurationProblems(WorkspaceConfiguration configuration)
    {
        foreach (var problem in configuration.Problems.Concat(configuration.Registry.Problems))
        {
            LanguageServerOutput.WriteLine(problem, OutputKind.Failure);
        }
    }

    private void OnLanguageServerLog(object? sender, LanguageServerLogEventArgs e)
    {
        var kind = e.Level switch
        {
            LanguageServerLogLevel.Error => OutputKind.Error,
            LanguageServerLogLevel.Warning => OutputKind.Failure,
            LanguageServerLogLevel.Trace => OutputKind.Info,
            _ => OutputKind.Program,
        };
        var time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        LanguageServerOutput.WriteLine(e.ServerName is { } name ? $"{time} [{name}] {e.Message}" : $"{time} {e.Message}", kind);
    }

    private void OnLanguageServerDiagnostics(object? sender, LanguageServerDiagnosticsEventArgs e) => _ui.Post(() =>
    {
        // Open documents report their own diagnostics, about the text in their editor.
        if (_disposed || Documents.Any(d => d.HasLanguageServer && string.Equals(d.FilePath, e.FilePath, StringComparison.Ordinal)))
        {
            return;
        }

        ErrorList.SetFileDiagnostics(e.FilePath, LspMapping.ToBuildDiagnostics(e.Diagnostics, FolderName(e.RootDirectory)));
    });

    private void OnLanguageServerMessage(object? sender, LanguageServerMessageEventArgs e) => _ui.Post(() =>
    {
        LanguageServerOutput.WriteLine($"[{e.ServerName}] {e.Message}", e.Type == LanguageServerMessageType.Error ? OutputKind.Error : OutputKind.Info);
        if (e.Type is LanguageServerMessageType.Error or LanguageServerMessageType.Warning)
        {
            StatusText = $"{e.ServerName}: {e.Message}";
        }
    });

    private void OnLanguageServerProgress(object? sender, LanguageServerProgressEventArgs e) => _ui.Post(() =>
    {
        if (ActiveDocument?.LanguageServer is not { } server || server.ServerName != e.ServerName)
        {
            return;
        }

        if (e.IsDone)
        {
            UpdateLanguageServerText();
            return;
        }

        var percentage = e.Percentage is { } value ? $" {value.ToString(CultureInfo.CurrentCulture)}%" : string.Empty;
        LanguageServerText = $"{e.ServerName}: {e.Title ?? e.Message ?? "working"}{percentage}";
    });

    private Task<bool> ApplyServerEditAsync(LspWorkspaceEdit edit, string? label, CancellationToken cancellationToken) =>
        InvokeOnUiAsync(async () =>
        {
            var files = await ApplyDocumentEditsAsync(LspMapping.ToDocumentEdits(edit, ReadText));
            if (label is not null)
            {
                StatusText = label;
            }

            return files > 0 || edit.Changes.Count == 0;
        });

    private Task<bool> ShowServerDocumentAsync(string path, TabStudio.LanguageServers.Documents.TextSpan? selection, CancellationToken cancellationToken) =>
        InvokeOnUiAsync(async () =>
        {
            if (await OpenDocumentAsync(path) is not { } document)
            {
                return false;
            }

            if (selection is { } span)
            {
                document.NavigateToOffset(span.Start);
            }

            return true;
        });

    private Task<string?> AnswerServerMessageAsync(LanguageServerMessageRequest request, CancellationToken cancellationToken) =>
        InvokeOnUiAsync(async () =>
        {
            if (request.Actions.Count == 0)
            {
                StatusText = $"{request.ServerName}: {request.Message}";
                return (string?)null;
            }

            // The in-app dialog has one button besides Cancel: the first action, as the server's suggested one.
            var confirmed = await Prompt.ConfirmAsync(request.ServerName, request.Message, request.Actions[0]);
            return confirmed ? request.Actions[0] : null;
        });

    private async Task<T> InvokeOnUiAsync<T>(Func<Task<T>> work)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ui.Post(async () =>
        {
            try
            {
                result.TrySetResult(await work());
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                result.TrySetException(ex);
            }
        });
        return await result.Task;
    }

    private void UpdateLanguageServerText()
    {
        if (ActiveDocument is not { } document || _languageServers is not { } host)
        {
            LanguageServerText = null;
            return;
        }

        if (document.LanguageServer is { } server)
        {
            LanguageServerText = server.ServerState switch
            {
                LanguageServerState.Starting => $"{server.ServerName} starting",
                LanguageServerState.Running => $"{server.ServerName} ready",
                LanguageServerState.Restarting => $"{server.ServerName} restarting",
                LanguageServerState.Failed => $"{server.ServerName} stopped",
                _ => server.ServerName,
            };
            return;
        }

        LanguageServerText = !document.HasLanguageService && host.FindMissingServers(document.FilePath) is [{ } missing, ..]
            ? $"{missing.Name} not installed"
            : null;
    }

    private void ShowOutputSource(OutputViewModel output)
    {
        SelectedOutputSource = OutputSources.First(s => ReferenceEquals(s.Output, output));
        SelectPanel(BottomPanel.Output);
    }

    private async Task GoToServerLocationAsync(bool typeDefinition)
    {
        if (ActiveDocument is not { } document)
        {
            return;
        }

        if (document.ServerFeatures is not { } features)
        {
            StatusText = typeDefinition ? "Go To Type Definition needs a language server." : "Go To Declaration needs a language server.";
            return;
        }

        if (typeDefinition ? !features.CanGoToTypeDefinition : !features.CanGoToDeclaration)
        {
            StatusText = $"{document.LanguageServer?.ServerName} cannot go to {(typeDefinition ? "type definitions" : "declarations")}.";
            return;
        }

        var offset = Math.Clamp(document.CaretOffset, 0, document.Document.TextLength);
        var result = typeDefinition
            ? await features.FindTypeDefinitionAsync(offset, BackgroundToken)
            : await features.FindDeclarationAsync(offset, BackgroundToken);
        if (result is not { Locations.Count: > 0 })
        {
            StatusText = "Cannot navigate to the symbol under the caret.";
        }
        else if (result.Locations.Count == 1)
        {
            var location = result.Locations[0];
            await NavigateToAsync(location.FilePath, location.Line, location.Column);
        }
        else
        {
            ShowLocations($"'{result.SymbolDisplay}' {(typeDefinition ? "type definitions" : "declarations")}", result.Locations);
        }
    }

    /// <summary>The text of a file for the servers: the editor's text when it is open, else the file on disk.</summary>
    private string? ReadText(string path)
    {
        if (_ui.CheckAccess() && Documents.FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.Ordinal)) is { } open)
        {
            return open.Document.Text;
        }

        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string FolderName(string directory) =>
        Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is { Length: > 0 } name ? name : directory;
}
