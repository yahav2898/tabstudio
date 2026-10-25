using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;

namespace TabStudio.App.ViewModels;

/// <summary>Asks the user to confirm something, such as removing a toolchain: title, message, button, destructive.</summary>
/// <returns>Whether the user confirmed.</returns>
public delegate Task<bool> ConfirmAction(string title, string message, string confirmText, bool destructive);

/// <summary>
/// The Toolchain Store (Tools &gt; Toolchain Store): the toolchains this build supports, with their versions, sizes
/// and states, and Install, Update, Remove and Cancel; and the space they take on the device.
/// </summary>
/// <remarks>
/// It lists only the catalog's listed toolchains, the ones TabStudio fully supports. Operations run one at a time
/// in the <see cref="ToolchainManager"/>; one started while another runs waits and says so.
/// </remarks>
public sealed partial class ToolchainStoreViewModel : ObservableObject, IDisposable
{
    private readonly ToolchainServices _toolchains;
    private readonly IUiDispatcher _ui;
    private readonly ConfirmAction _confirm;
    private readonly Dictionary<string, (Task Task, CancellationTokenSource Cancellation)> _running = new(StringComparer.Ordinal);

    /// <summary>Creates the Store.</summary>
    /// <param name="toolchains">The device's toolchains.</param>
    /// <param name="ui">The UI dispatcher.</param>
    /// <param name="confirm">Asks the user to confirm removing a toolchain.</param>
    public ToolchainStoreViewModel(ToolchainServices toolchains, IUiDispatcher ui, ConfirmAction confirm)
    {
        ArgumentNullException.ThrowIfNull(toolchains);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(confirm);
        _toolchains = toolchains;
        _ui = ui;
        _confirm = confirm;
        foreach (var status in Manager.Statuses.Where(s => s.Toolchain.Listed))
        {
            Items.Add(new ToolchainItemViewModel(this, status));
        }

        Manager.StatusChanged += OnStatusChanged;
        Manager.Changed += OnChanged;
        UpdateDiskUsage();
    }

    /// <summary>The manager.</summary>
    public ToolchainManager Manager => _toolchains.Manager;

    /// <summary>The toolchains the Store offers.</summary>
    public ObservableCollection<ToolchainItemViewModel> Items { get; } = [];

    /// <summary>Whether the Store is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>Whether it is checking the mirror for newer versions.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
    public partial bool IsChecking { get; private set; }

    /// <summary>The space toolchains take, such as "Toolchains 182 MB · Linux 9.1 MB · Downloads 0 bytes".</summary>
    [ObservableProperty]
    public partial string DiskUsageText { get; private set; } = string.Empty;

    /// <summary>The free space, such as "21 GB free".</summary>
    [ObservableProperty]
    public partial string FreeSpaceText { get; private set; } = string.Empty;

    /// <summary>Whether the user can install toolchains on this device.</summary>
    public bool CanInstallHere => Manager.CanInstall;

    /// <summary>Where toolchains come from, for the subtitle.</summary>
    public string Subtitle => CanInstallHere
        ? $"Languages and tools that run on this tablet, from {_toolchains.Catalog.Linux.DisplayName}."
        : "Languages and tools TabStudio runs with.";

    /// <summary>Why toolchains cannot be installed here, such as on a desktop computer, which uses its own.</summary>
    public string? Notice => Manager.Problem ?? (CanInstallHere
        ? null
        : "On this computer TabStudio uses the toolchains installed on it. Install them with the computer's package manager.");

    /// <summary>Whether there is a notice.</summary>
    public bool HasNotice => Notice is not null;

    /// <summary>Opens the Store and reads the toolchains' states.</summary>
    /// <param name="highlight">A toolchain to point out, such as the one a project needs.</param>
    public void Open(string? highlight = null)
    {
        foreach (var item in Items)
        {
            item.IsHighlighted = item.Id == highlight;
        }

        IsOpen = true;
        _ = RefreshAsync();
    }

    /// <summary>Installs a toolchain, or returns the install already running.</summary>
    /// <param name="id">The toolchain.</param>
    /// <returns>Completes when the install ends, whether it succeeded, failed or was cancelled; the status says which.</returns>
    public Task InstallAsync(string id) => Run(id, cancellation => Manager.InstallAsync(id, cancellation));

    /// <summary>Updates a toolchain.</summary>
    /// <param name="id">The toolchain.</param>
    public Task UpdateAsync(string id) => Run(id, cancellation => Manager.UpdateAsync(id, cancellation));

    /// <summary>Asks, then removes a toolchain.</summary>
    /// <param name="id">The toolchain.</param>
    public async Task RemoveAsync(string id)
    {
        var status = Manager.Status(id);
        var size = status.InstalledSize is { } used ? $" ({DiskSpace.Format(used)})" : string.Empty;
        if (await _confirm(
            $"Remove {status.Toolchain.Name}?",
            $"Removes {status.Toolchain.Name} and what only it uses{size}. Your projects keep their own files, such as .venv and node_modules. You can install it again from the Store.",
            "Remove",
            true))
        {
            await Run(id, cancellation => Manager.RemoveAsync(id, cancellation));
        }
    }

    /// <summary>Cancels a toolchain's install or update; what was downloaded is kept for the next attempt.</summary>
    /// <param name="id">The toolchain.</param>
    public void Cancel(string id)
    {
        if (_running.TryGetValue(id, out var running))
        {
            running.Cancellation.Cancel();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Manager.StatusChanged -= OnStatusChanged;
        Manager.Changed -= OnChanged;
        foreach (var running in _running.Values)
        {
            running.Cancellation.Cancel();
        }
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    private async Task CheckForUpdatesAsync()
    {
        IsChecking = true;
        try
        {
            await Manager.RefreshAsync(checkForUpdates: true);
        }
        catch (Exception ex) when (ex is ToolchainInstallException or IOException or UnauthorizedAccessException)
        {
            // Offline: the states stay as they were.
        }
        finally
        {
            IsChecking = false;
            UpdateDiskUsage();
        }
    }

    private bool CanCheckForUpdates() => CanInstallHere && !IsChecking;

    private async Task RefreshAsync()
    {
        await _toolchains.EnsureStatusAsync();
        UpdateItems();
        UpdateDiskUsage();
    }

    private Task Run(string id, Func<CancellationToken, Task> operation)
    {
        if (_running.TryGetValue(id, out var running))
        {
            return running.Task;
        }

        var cancellation = new CancellationTokenSource();
        var task = RunCoreAsync(id, operation, cancellation);
        if (!task.IsCompleted)
        {
            _running[id] = (task, cancellation);
        }

        return task;
    }

    private async Task RunCoreAsync(string id, Func<CancellationToken, Task> operation, CancellationTokenSource cancellation)
    {
        try
        {
            await operation(cancellation.Token);
        }
        catch (ToolchainInstallException)
        {
            // The toolchain's status carries the message, which its row shows.
        }
        catch (OperationCanceledException)
        {
            // Cancelled: the downloads are kept for the next attempt.
        }
        finally
        {
            _running.Remove(id);
            cancellation.Dispose();
            UpdateItems();
            UpdateDiskUsage();
        }
    }

    private void OnStatusChanged(object? sender, ToolchainStatus status) => _ui.Post(() =>
    {
        if (Items.FirstOrDefault(i => i.Id == status.Toolchain.Id) is { } item)
        {
            item.Status = status;
        }
    });

    private void OnChanged(object? sender, EventArgs e) => _ui.Post(() =>
    {
        UpdateItems();
        UpdateDiskUsage();
    });

    private void UpdateItems()
    {
        foreach (var item in Items)
        {
            item.Status = Manager.Status(item.Id);
        }
    }

    private void UpdateDiskUsage()
    {
        var usage = Manager.DiskUsage;
        DiskUsageText = CanInstallHere
            ? $"Toolchains {DiskSpace.Format(usage.Toolchains)} · Linux {DiskSpace.Format(usage.Base)} · Downloads {DiskSpace.Format(usage.Downloads)}"
            : string.Empty;
        FreeSpaceText = CanInstallHere && usage.Free > 0 ? $"{DiskSpace.Format(usage.Free)} free" : string.Empty;
    }
}
