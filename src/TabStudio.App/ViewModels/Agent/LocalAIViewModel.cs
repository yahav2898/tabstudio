using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Agent.Local;
using TabStudio.App.Services;
using TabStudio.App.Services.LocalAI;

namespace TabStudio.App.ViewModels.Agent;

/// <summary>The model manager embedded in agent settings: licenses, downloads, RAM and measured speed.</summary>
public sealed partial class LocalAIViewModel : ObservableObject, IDisposable
{
    private bool _initializing = true;
    private readonly LocalAIService _service;
    private readonly IUiDispatcher _ui;
    private CancellationTokenSource? _download;

    /// <summary>Creates the settings view over the app's inference service.</summary>
    public LocalAIViewModel(LocalAIService service, IUiDispatcher ui)
    {
        _service = service;
        _ui = ui;
        SelectedModel = service.SelectedModel;
        InlineEnabled = service.InlineEnabled;
        _initializing = false;
        _service.Changed += OnServiceChanged;
        _service.StatisticsChanged += OnServiceChanged;
    }

    /// <summary>The curated models.</summary>
    public IReadOnlyList<LocalModel> Models { get; } = ModelCatalog.All;
    /// <summary>The model whose details and license are displayed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details), nameof(LicenseText), nameof(Installed), nameof(Status))]
    public partial LocalModel SelectedModel { get; set; }
    /// <summary>Background completion is opt-in.</summary>
    [ObservableProperty]
    public partial bool InlineEnabled { get; set; }
    /// <summary>Acceptance applies only to the currently displayed model and license.</summary>
    [ObservableProperty]
    public partial bool LicenseAccepted { get; set; }
    /// <summary>A download or deletion is in progress.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }
    /// <summary>Download progress or the last error.</summary>
    [ObservableProperty]
    public partial string ProgressText { get; private set; } = string.Empty;
    /// <summary>The selected model's upstream license in full.</summary>
    public string LicenseText => SelectedModel.LicenseText;
    /// <summary>Whether this model's verified download is present.</summary>
    public bool Installed => _service.Models.IsInstalled(SelectedModel);
    /// <summary>Size, RAM and speed; speed is measured on this device, never invented from host benchmarks.</summary>
    public string Details => string.Create(CultureInfo.CurrentCulture, $"Download {SelectedModel.Size / 1e9:0.00} GB · RAM estimate {SelectedModel.RamBytes / 1e9:0.0} GB · {SelectedModel.License}\n{(_service.Speed(SelectedModel) > 0 ? $"Last measured: {_service.Speed(SelectedModel):0.0} tokens/s on this device" : "Speed: not yet measured on this device")}\n{(SelectedModel.SupportsFim ? "Chat and inline completion" : "Chat model; select a Coder model for inline completion")}");
    /// <summary>Power and installation status.</summary>
    public string Status => _service.LastError ?? _service.PauseReason ?? (Installed ? "Ready · inference stays on this device" : "Download a model to use offline AI");

    partial void OnSelectedModelChanged(LocalModel value)
    {
        LicenseAccepted = false;
        if (!_initializing) _service.Configure(value.Id, InlineEnabled);
    }

    partial void OnInlineEnabledChanged(bool value)
    {
        if (!_initializing && SelectedModel is not null) _service.Configure(SelectedModel.Id, value);
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (IsBusy) return;
        if (!LicenseAccepted) { ProgressText = "Read and accept the displayed model license first."; return; }
        var model = SelectedModel;
        IsBusy = true;
        _download = new CancellationTokenSource();
        try
        {
            var progress = new Progress<long>(bytes => ProgressText = string.Create(CultureInfo.CurrentCulture, $"Downloading {bytes / 1e6:0} / {model.Size / 1e6:0} MB"));
            await _service.Models.DownloadAsync(model, LicenseAccepted, progress, _download.Token);
            ProgressText = "Download verified with SHA-256. Ready offline.";
        }
        catch (OperationCanceledException) { ProgressText = "Download canceled; partial file removed."; }
        catch (Exception ex) when (ex is IOException or HttpRequestException or UnauthorizedAccessException or InvalidOperationException) { ProgressText = ex.Message; }
        finally { _download.Dispose(); _download = null; IsBusy = false; Refresh(); }
    }

    [RelayCommand]
    private void CancelDownload() => _download?.Cancel();

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        var model = SelectedModel;
        try
        {
            await _service.DeleteModelAsync(model);
            ProgressText = "Model deleted.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ProgressText = ex.Message; }
        finally { IsBusy = false; Refresh(); }
    }

    [RelayCommand]
    private async Task UnloadAsync() { await _service.UnloadAsync(); ProgressText = "The model is unloaded, and its memory is free."; }

    /// <inheritdoc />
    public void Dispose()
    {
        _download?.Cancel();
        _service.Changed -= OnServiceChanged;
        _service.StatisticsChanged -= OnServiceChanged;
    }

    private void OnServiceChanged(object? sender, EventArgs e) => _ui.Post(Refresh);
    private void Refresh() { OnPropertyChanged(nameof(Details)); OnPropertyChanged(nameof(Installed)); OnPropertyChanged(nameof(Status)); }
}
