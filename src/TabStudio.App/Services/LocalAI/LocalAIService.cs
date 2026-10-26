using System.Text.Json;
using System.Collections.Concurrent;
using TabStudio.Agent.Local;
using TabStudio.App.Platform;

namespace TabStudio.App.Services.LocalAI;

/// <summary>One on-device model per app, shared by chat and inline completion; inference has no HTTP client.</summary>
public sealed class LocalAIService : ILocalTextGenerator, IDisposable
{
    private readonly object _stateLock = new();
    private readonly IDeviceStatus _device;
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _downloads = new() { Timeout = Timeout.InfiniteTimeSpan };
    private NativeTextGenerator? _engine;
    private string? _loadedId;
    private CancellationTokenSource? _running;
    private bool _disposed;
    private string? _lastError;
    private readonly ConcurrentDictionary<string, double> _speeds = new(StringComparer.Ordinal);

    /// <summary>Creates model storage. No model or network resource is loaded until explicitly requested.</summary>
    public LocalAIService(AppPaths paths, IDeviceStatus device)
    {
        _paths = paths;
        _device = device;
        Models = new ModelManager(Path.Combine(paths.Root, "LocalAI", "models"), _downloads);
        try
        {
            var state = JsonSerializer.Deserialize<LocalAIState>(File.ReadAllText(StatePath));
            if (state is not null)
            {
                SelectedModelId = state.ModelId;
                InlineEnabled = state.InlineEnabled;
                foreach (var (key, value) in state.Speeds ?? []) _speeds[key] = value;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        _device.ThermalChanged += OnPowerChanged;
        _device.PowerChanged += OnPowerChanged;
        _device.MemoryPressureRaised += OnMemoryPressure;
    }

    /// <summary>The verified GGUF storage.</summary>
    public ModelManager Models { get; }
    /// <summary>The selected GGUF, also used by the local agent.</summary>
    public string SelectedModelId { get; private set; } = ModelCatalog.All[0].Id;
    /// <summary>The current catalog entry.</summary>
    public LocalModel SelectedModel => ModelCatalog.All.FirstOrDefault(m => m.Id == SelectedModelId) ?? ModelCatalog.All[0];
    /// <summary>Whether the user enabled background completions.</summary>
    public bool InlineEnabled { get; private set; }
    /// <summary>The last inference failure, also visible in model settings.</summary>
    public string? LastError => _lastError;
    /// <summary>Why completion is paused.</summary>
    public string? PauseReason => InferencePolicy.PauseReason(_device.Thermal == ThermalState.Hot, _device.BatteryPercent, _device.IsCharging);
    /// <summary>Raised for power, model and enablement changes; may be on a platform thread.</summary>
    public event EventHandler? Changed;
    /// <summary>New device-specific speed measurements.</summary>
    public event EventHandler? StatisticsChanged;
    /// <summary>Whether inference has loaded a model.</summary>
    public bool IsLoaded => _engine is not null;
    /// <summary>RAM reported by the device.</summary>
    public long AvailableDeviceRam => _device.Device.TotalMemoryBytes;
    private string StatePath => Path.Combine(_paths.State, "local-ai.json");

    /// <summary>The speed measured for this model on this device; zero until first inference.</summary>
    public double Speed(LocalModel model) => _speeds.TryGetValue(model.Id, out var speed) ? speed : 0;

    /// <summary>Changes settings and invalidates pending inline completions.</summary>
    public void Configure(string modelId, bool inlineEnabled)
    {
        if (!ModelCatalog.All.Any(m => m.Id == modelId)) throw new ArgumentException("Unknown local model.", nameof(modelId));
        SelectedModelId = modelId;
        InlineEnabled = inlineEnabled;
        SaveState();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Generates after verifying and loading the selected file; no download is attempted.</summary>
    public async Task<LocalCompletion> GenerateAsync(string prompt, int maxTokens, Action<string>? onText = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var model = SelectedModel;
            using var running = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _running = running;
            if (_loadedId != model.Id)
            {
                _engine?.Dispose();
                _engine = null;
                _loadedId = null;
                if (IntPtr.Size < 8) throw new InvalidOperationException("Local AI requires a 64-bit device.");
                if (model.RamBytes > _device.Device.TotalMemoryBytes) throw new InvalidOperationException("This model needs more RAM than the device has. Select a smaller model.");
                await Models.VerifyAsync(model, running.Token).ConfigureAwait(false);
                _engine = await Task.Run(() => new NativeTextGenerator(Models.PathFor(model)), running.Token).ConfigureAwait(false);
                _loadedId = model.Id;
            }

            _lastError = null;
            var completion = await _engine!.GenerateAsync(prompt, maxTokens, onText, running.Token).ConfigureAwait(false);
            _speeds[model.Id] = completion.TokensPerSecond;
            SaveState();
            StatisticsChanged?.Invoke(this, EventArgs.Empty);
            return completion;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _lastError = ex.Message;
            StatisticsChanged?.Invoke(this, EventArgs.Empty);
            throw;
        }
        finally { _running = null; _gate.Release(); }
    }

    /// <summary>A bounded background FIM completion. Chat holds priority while the engine is occupied.</summary>
    public async Task<string?> CompleteAsync(string path, string text, int caret, IReadOnlyList<OpenFileContext> neighbors, CancellationToken cancellationToken)
    {
        if (!InlineEnabled || PauseReason is not null || !SelectedModel.SupportsFim || !Models.IsInstalled(SelectedModel) || _gate.CurrentCount == 0) return null;
        var prompt = FimPromptBuilder.Build(path, text, caret, neighbors);
        var result = await GenerateAsync(prompt.Text, 96, cancellationToken: cancellationToken).ConfigureAwait(false);
        return FimPromptBuilder.Clean(result.Text, prompt.Stops, text[caret..]);
    }

    /// <summary>Releases RAM when idle or before deleting a loaded model.</summary>
    public async Task UnloadAsync(CancellationToken cancellationToken = default)
    {
        CancelRunning();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { _engine?.Dispose(); _engine = null; _loadedId = null; }
        finally { _gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops inference and deletes a model while holding the engine gate, preventing reload/delete races.</summary>
    public async Task DeleteModelAsync(LocalModel model, CancellationToken cancellationToken = default)
    {
        CancelRunning();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loadedId == model.Id) { _engine?.Dispose(); _engine = null; _loadedId = null; }
            await Models.DeleteAsync(model, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _device.ThermalChanged -= OnPowerChanged;
        _device.PowerChanged -= OnPowerChanged;
        _device.MemoryPressureRaised -= OnMemoryPressure;
        CancelRunning();
        // Never block the UI waiting for a model to finish loading.
        _ = Task.Run(ReleaseAsync);
    }

    private async Task ReleaseAsync()
    {
        await UnloadAsync().ConfigureAwait(false);
        Models.Dispose();
        _downloads.Dispose();
    }

    private void OnPowerChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
    private void OnMemoryPressure(object? sender, MemoryPressure e) { CancelRunning(); _ = UnloadAsync(); }
    private void CancelRunning() { try { _running?.Cancel(); } catch (ObjectDisposedException) { } }
    private void SaveState()
    {
        lock (_stateLock)
        {
        Directory.CreateDirectory(_paths.State);
        var temporary = StatePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new LocalAIState(SelectedModelId, InlineEnabled, _speeds.ToDictionary())));
        File.Move(temporary, StatePath, overwrite: true);
        }
    }

    private sealed record LocalAIState(string ModelId, bool InlineEnabled, Dictionary<string, double>? Speeds);
}
