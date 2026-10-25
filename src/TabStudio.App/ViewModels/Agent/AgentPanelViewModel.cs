using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Agent;
using TabStudio.Agent.Anthropic;
using TabStudio.Agent.Mcp;
using TabStudio.Agent.Tools;
using TabStudio.App.Services;
using TabStudio.App.Services.Agent;

namespace TabStudio.App.ViewModels.Agent;

/// <summary>
/// The agent tool window: a conversation with Claude about the open workspace, the changes it proposes with
/// their diffs, Keep and Undo for each, and the agent's settings.
/// </summary>
public sealed partial class AgentPanelViewModel : ObservableObject, IDisposable
{
    private readonly AgentSettingsStore _settingsStore;
    private readonly HttpClient _http;
    private readonly IUiDispatcher _ui;
    private readonly IAgentWorkspace _workspace;
    private AgentSettings _settings;
    private AgentSession? _session;
    private CancellationTokenSource? _turn;
    private AssistantMessageViewModel? _streaming;

    /// <summary>Creates the panel.</summary>
    /// <param name="settingsStore">Where the settings are kept.</param>
    /// <param name="http">The HTTP client for Claude and MCP servers.</param>
    /// <param name="ui">The UI dispatcher.</param>
    /// <param name="workspace">The workspace the agent works on.</param>
    public AgentPanelViewModel(AgentSettingsStore settingsStore, HttpClient http, IUiDispatcher ui, IAgentWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(workspace);
        _settingsStore = settingsStore;
        _http = http;
        _ui = ui;
        _workspace = workspace;
        _settings = settingsStore.Load();
        LoadSettingsFields();
        IsSettingsOpen = !_settings.HasApiKey;
    }

    /// <summary>Raised when the conversation grows, so the view can follow the newest text.</summary>
    public event EventHandler? ContentGrew;

    /// <summary>Raised when the input box should take the keyboard focus.</summary>
    public event EventHandler? FocusRequested;

    /// <summary>The conversation.</summary>
    public ObservableCollection<AgentItemViewModel> Items { get; } = [];

    /// <summary>Changes waiting for Keep or Undo.</summary>
    public ObservableCollection<PendingChangeViewModel> Changes { get; } = [];

    /// <summary>Whether there are changes to review.</summary>
    public bool HasChanges => Changes.Count > 0;

    /// <summary>"3 files changed", above the changes.</summary>
    public string ChangesTitle => Changes.Count == 1 ? "1 file changed" : $"{Changes.Count.ToString(CultureInfo.CurrentCulture)} files changed";

    /// <summary>Whether the conversation is empty, to show the welcome text.</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>What the user is typing.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>Whether the agent is working.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand), nameof(StopCommand), nameof(NewConversationCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>The last error, shown above the input.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>Tokens used in this conversation.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsageSuffix))]
    public partial string UsageText { get; private set; } = string.Empty;

    /// <summary>The tokens used, as they follow the model's name in the header.</summary>
    public string UsageSuffix => UsageText.Length == 0 ? string.Empty : $" · {UsageText}";

    /// <summary>Whether the settings are shown instead of the conversation.</summary>
    [ObservableProperty]
    public partial bool IsSettingsOpen { get; set; }

    /// <summary>The API key being edited.</summary>
    [ObservableProperty]
    public partial string ApiKeyText { get; set; } = string.Empty;

    /// <summary>The model being edited.</summary>
    [ObservableProperty]
    public partial string SelectedModel { get; set; } = AnthropicClient.DefaultModel;

    /// <summary>The standing instructions being edited.</summary>
    [ObservableProperty]
    public partial string InstructionsText { get; set; } = string.Empty;

    /// <summary>MCP servers being edited, one per line: <c>name url [token]</c>.</summary>
    [ObservableProperty]
    public partial string McpServersText { get; set; } = string.Empty;

    /// <summary>The models to choose from.</summary>
    public IReadOnlyList<string> Models { get; } = AgentSettings.Models;

    /// <summary>The model in use, for the header.</summary>
    public string ModelLabel => _settings.Model;

    /// <summary>Whether an API key is set.</summary>
    public bool HasApiKey => _settings.HasApiKey;

    /// <summary>Stops the agent if it is working.</summary>
    public void Dispose()
    {
        _turn?.Cancel();
        _turn?.Dispose();
        _turn = null;
    }

    /// <summary>Puts the keyboard focus in the input box.</summary>
    public void FocusInput() => FocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Asks the agent something on the user's behalf, such as "Fix with Agent" from the Error List. While the agent
    /// is busy, or before it has an API key, the request waits in the input box for the user to send.
    /// </summary>
    /// <param name="prompt">The request.</param>
    public async Task AskAsync(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        InputText = prompt;
        if (IsBusy)
        {
            FocusInput();
            return;
        }

        await SendAsync();
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = InputText.Trim();
        if (text.Length == 0 || IsBusy)
        {
            return;
        }

        if (!_settings.HasApiKey)
        {
            IsSettingsOpen = true;
            return;
        }

        ErrorText = null;
        InputText = string.Empty;
        Items.Add(new UserMessageViewModel(text));
        OnPropertyChanged(nameof(IsEmpty));
        ContentGrew?.Invoke(this, EventArgs.Empty);
        IsBusy = true;
        _turn = new CancellationTokenSource();
        try
        {
            var session = _session ??= await CreateSessionAsync(_turn.Token);
            var result = await session.SendAsync(text, _turn.Token);
            var notice = result.End switch
            {
                AgentTurnEnd.RoundLimit => "The agent paused after many steps. Send a message to let it continue.",
                AgentTurnEnd.TokenLimit => "The answer reached its length limit.",
                AgentTurnEnd.Canceled => "Stopped.",
                AgentTurnEnd.Refused => "Claude declined this request.",
                _ => null,
            };
            if (notice is not null)
            {
                Items.Add(new AgentNoticeViewModel(notice));
            }

            UsageText = FormatUsage(session.Usage);
        }
        catch (Exception ex) when (ex is AnthropicException or McpException or InvalidOperationException or HttpRequestException)
        {
            ErrorText = ex.Message;
            if (ex is AnthropicException { IsAuthenticationError: true })
            {
                IsSettingsOpen = true;
            }
        }
        catch (OperationCanceledException)
        {
            Items.Add(new AgentNoticeViewModel("Stopped."));
        }
        finally
        {
            _streaming = null;
            _turn?.Dispose();
            _turn = null;
            IsBusy = false;
        }
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Stop() => _turn?.Cancel();

    [RelayCommand(CanExecute = nameof(CanStartOver))]
    private void NewConversation()
    {
        _session = null;
        Items.Clear();
        Changes.Clear();
        UsageText = string.Empty;
        ErrorText = null;
        OnPropertyChanged(nameof(IsEmpty));
        OnChangesChanged();
    }

    private bool CanStartOver() => !IsBusy;

    [RelayCommand]
    private async Task KeepAsync(PendingChangeViewModel? change)
    {
        if (change is not null && _session is { } session)
        {
            await RunChangeActionAsync(() => session.ApproveAsync(change.Change.FullPath));
        }
    }

    [RelayCommand]
    private void Undo(PendingChangeViewModel? change)
    {
        if (change is not null)
        {
            _session?.Reject(change.Change.FullPath);
        }
    }

    [RelayCommand]
    private async Task KeepAllAsync()
    {
        if (_session is { } session)
        {
            await RunChangeActionAsync(() => session.ApproveAllAsync());
        }
    }

    [RelayCommand]
    private void UndoAll()
    {
        foreach (var change in Changes.ToList())
        {
            _session?.Reject(change.Change.FullPath);
        }
    }

    [RelayCommand]
    private static void ToggleDiff(PendingChangeViewModel? change)
    {
        if (change is not null)
        {
            change.IsExpanded = !change.IsExpanded;
        }
    }

    [RelayCommand]
    private static void ToggleResult(ToolCallViewModel? call)
    {
        if (call is not null)
        {
            call.IsExpanded = !call.IsExpanded;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        LoadSettingsFields();
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void CancelSettings()
    {
        LoadSettingsFields();
        IsSettingsOpen = !_settings.HasApiKey;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var servers = new List<McpServerSetting>();
        foreach (var line in McpServersText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !Uri.TryCreate(parts[1], UriKind.Absolute, out var url) || url.Scheme is not ("https" or "http"))
            {
                ErrorText = $"Each MCP server line needs a name and an http(s) address: \"{line}\".";
                return;
            }

            servers.Add(new McpServerSetting(parts[0], parts[1], parts.Length > 2 ? parts[2] : null));
        }

        _settings = new AgentSettings(
            string.IsNullOrWhiteSpace(ApiKeyText) ? null : ApiKeyText.Trim(),
            string.IsNullOrWhiteSpace(SelectedModel) ? AnthropicClient.DefaultModel : SelectedModel,
            string.IsNullOrWhiteSpace(InstructionsText) ? null : InstructionsText.Trim(),
            servers);
        _settingsStore.Save(_settings);
        _session = null;
        ErrorText = null;
        IsSettingsOpen = !_settings.HasApiKey;
        OnPropertyChanged(nameof(ModelLabel));
        OnPropertyChanged(nameof(HasApiKey));
    }

    private void LoadSettingsFields()
    {
        ApiKeyText = _settings.ApiKey ?? string.Empty;
        SelectedModel = _settings.Model;
        InstructionsText = _settings.Instructions ?? string.Empty;
        McpServersText = string.Join('\n', _settings.McpServers.Select(s => s.Token is null ? $"{s.Name} {s.Url}" : $"{s.Name} {s.Url} {s.Token}"));
    }

    private async Task<AgentSession> CreateSessionAsync(CancellationToken cancellationToken)
    {
        var tools = new List<AgentTool>();
        foreach (var server in _settings.McpServers)
        {
            try
            {
                var provider = new McpToolProvider(new McpClient(_http, new McpServer(server.Name, new Uri(server.Url), server.Token)));
                tools.AddRange(await provider.GetToolsAsync(cancellationToken));
            }
            catch (McpException ex)
            {
                Items.Add(new AgentNoticeViewModel($"{ex.Message} The agent continues without it."));
            }
        }

        var session = new AgentSession(
            new AnthropicClient(_http, _settings.ApiKey!),
            _workspace,
            new AgentOptions(Model: _settings.Model, Instructions: _settings.Instructions),
            tools);
        session.Event += (_, e) => _ui.Post(() => OnAgentEvent(e));
        session.Changes.Changed += (_, _) => _ui.Post(SyncChanges);
        return session;
    }

    private void OnAgentEvent(AgentEvent agentEvent)
    {
        switch (agentEvent)
        {
            case AgentTextDelta delta:
                if (_streaming is null)
                {
                    _streaming = new AssistantMessageViewModel();
                    Items.Add(_streaming);
                }

                _streaming.Append(delta.Text);
                break;
            case AgentToolStarted started:
                _streaming = null;
                Items.Add(new ToolCallViewModel(started.CallId, started.Description));
                break;
            case AgentToolFinished finished:
                if (Items.OfType<ToolCallViewModel>().LastOrDefault(c => c.CallId == finished.CallId) is { } call)
                {
                    call.Finish(finished.Result.Content, finished.Result.IsError);
                }

                break;
        }

        ContentGrew?.Invoke(this, EventArgs.Empty);
    }

    private void SyncChanges()
    {
        Changes.Clear();
        if (_session is not null)
        {
            foreach (var change in _session.Changes.Pending)
            {
                Changes.Add(new PendingChangeViewModel(change));
            }
        }

        OnChangesChanged();
    }

    private async Task RunChangeActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorText = $"The change could not be applied: {ex.Message}";
        }
    }

    private void OnChangesChanged()
    {
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(ChangesTitle));
    }

    private static string FormatUsage(TokenUsage usage)
    {
        var input = usage.InputTokens + usage.CacheReadTokens + usage.CacheWriteTokens;
        return $"{Thousands(input)} in · {Thousands(usage.OutputTokens)} out";
    }

    private static string Thousands(int tokens) =>
        tokens >= 1000 ? $"{(tokens / 1000.0).ToString("0.#", CultureInfo.CurrentCulture)}k" : tokens.ToString(CultureInfo.CurrentCulture);
}
