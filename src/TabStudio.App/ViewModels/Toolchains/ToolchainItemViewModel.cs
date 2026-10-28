using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Theming;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;

namespace TabStudio.App.ViewModels;

/// <summary>One toolchain in the Toolchain Store: what it is, its state, and what can be done with it.</summary>
public sealed partial class ToolchainItemViewModel : ObservableObject
{
    private readonly ToolchainStoreViewModel _store;

    /// <summary>Creates the row.</summary>
    /// <param name="store">The Store.</param>
    /// <param name="status">The toolchain's status.</param>
    public ToolchainItemViewModel(ToolchainStoreViewModel store, ToolchainStatus status)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(status);
        _store = store;
        Status = status;
    }

    /// <summary>The toolchain's current status.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionText), nameof(SizeText), nameof(StateText), nameof(IsBusy), nameof(IsInstalled), nameof(HasUpdate))]
    [NotifyPropertyChangedFor(nameof(Progress), nameof(ProgressText), nameof(HasProgress), nameof(IsProgressIndeterminate))]
    [NotifyPropertyChangedFor(nameof(Error), nameof(HasError), nameof(Note), nameof(HasNote))]
    [NotifyPropertyChangedFor(nameof(ShowsInstall), nameof(ShowsUpdate), nameof(ShowsRemove), nameof(ShowsCancel))]
    [NotifyPropertyChangedFor(nameof(ShowsPrimaryInstall), nameof(ShowsSecondaryInstall))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(UpdateCommand), nameof(RemoveCommand), nameof(CancelCommand))]
    public partial ToolchainStatus Status { get; set; }

    /// <summary>Whether the row is the one the Store was opened for, such as from the Compatibility Doctor.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPrimaryInstall), nameof(ShowsSecondaryInstall))]
    public partial bool IsHighlighted { get; set; }

    /// <summary>Install is the Store's one primary action for the toolchain it was opened for (docs/design/README.md, "Accent").</summary>
    public bool ShowsPrimaryInstall => ShowsInstall && IsHighlighted;

    /// <summary>Install, for every other toolchain: a secondary button, so the Store has one primary action at most.</summary>
    public bool ShowsSecondaryInstall => ShowsInstall && !IsHighlighted;

    /// <summary>The toolchain.</summary>
    public ToolchainDefinition Toolchain => Status.Toolchain;

    /// <summary>The toolchain's id.</summary>
    public string Id => Toolchain.Id;

    /// <summary>The name, such as "Python".</summary>
    public string Name => Toolchain.Name;

    /// <summary>What it installs and what it is for.</summary>
    public string Description => Toolchain.Description;

    /// <summary>The languages it runs, such as "Python".</summary>
    public string Languages => string.Join(" · ", Toolchain.Languages);

    /// <summary>The two letters on its tile, such as "Py".</summary>
    public string Badge => Toolchain.Id switch
    {
        "python" => "Py",
        "node" => "JS",
        "cpp" => "C++",
        "rust" => "Rs",
        "go" => "Go",
        "java" => "Jv",
        "kotlin" => "Kt",
        "php" => "Php",
        "swift" => "Sw",
        "android" => "An",
        "flutter" => "Fl",
        "xc8" => "X8",
        "xc32" => "X32",
        _ => Toolchain.Name[..Math.Min(2, Toolchain.Name.Length)],
    };

    /// <summary>The tile's color, close to the language's own.</summary>
    public IBrush BadgeBrush => ThemeBrushes.Get(Toolchain.Id switch
    {
        "python" => "Brush.Toolchain.Python",
        "node" => "Brush.Toolchain.Node",
        "cpp" => "Brush.Toolchain.Cpp",
        "rust" => "Brush.Toolchain.Rust",
        "go" => "Brush.Toolchain.Go",
        "java" => "Brush.Toolchain.Java",
        "kotlin" => "Brush.Toolchain.Kotlin",
        "php" => "Brush.Toolchain.Php",
        "swift" => "Brush.Toolchain.Swift",
        "windows" => "Brush.Toolchain.Windows",
        "android" => "Brush.Toolchain.Android",
        "flutter" => "Brush.Toolchain.Flutter",
        "xc8" or "xc32" => "Brush.Toolchain.Microchip",
        _ => "Brush.Toolchain.Other",
    });

    /// <summary>The version: installed, or what installing gets.</summary>
    public string VersionText => Status switch
    {
        { State: ToolchainState.UpdateAvailable, Version: { } installed, AvailableVersion: { } available } => $"{installed} → {available}",
        { Version: { } installed } => installed,
        { AvailableVersion: { } available } => available,
        _ => string.Empty,
    };

    /// <summary>The space it takes, or what installing it downloads and takes.</summary>
    public string SizeText
    {
        get
        {
            var about = Status.SizesAreExact ? string.Empty : "about ";
            return Status switch
            {
                { State: ToolchainState.UpdateAvailable, DownloadSize: > 0 } => $"Update downloads {about}{DiskSpace.Format(Status.DownloadSize)}",
                { InstalledSize: { } used } when Status.IsReady => $"Uses {DiskSpace.Format(used)}",
                { State: ToolchainState.Unavailable } => string.Empty,
                _ => $"Downloads {about}{DiskSpace.Format(Status.DownloadSize)} · {DiskSpace.Format(Status.InstallSize)} installed",
            };
        }
    }

    /// <summary>The state, such as "Installed".</summary>
    public string StateText => Status.State switch
    {
        ToolchainState.Installed => "Installed",
        ToolchainState.UpdateAvailable => "Update available",
        ToolchainState.Installing => "Installing",
        ToolchainState.Updating => "Updating",
        ToolchainState.Removing => "Removing",
        ToolchainState.Unavailable => "Not available",
        _ => "Not installed",
    };

    /// <summary>Whether it is installed.</summary>
    public bool IsInstalled => Status.IsReady;

    /// <summary>Whether a newer version is available.</summary>
    public bool HasUpdate => Status.State == ToolchainState.UpdateAvailable;

    /// <summary>Whether an operation runs on it.</summary>
    public bool IsBusy => Status.IsBusy;

    /// <summary>How far the operation has got, from 0 to 100.</summary>
    public double Progress => (Status.Progress?.Fraction ?? 0) * 100;

    /// <summary>Whether the operation's progress is unknown, such as while it waits for another one.</summary>
    public bool IsProgressIndeterminate => Status.Progress is { Fraction: null };

    /// <summary>What the operation does now, such as "Downloading python3 3.14.8 · 6.1 of 18 MB".</summary>
    public string ProgressText => Status.Progress is not { } progress
        ? string.Empty
        : progress.BytesTotal > 0 && progress.Stage == InstallStage.Downloading
            ? string.Create(CultureInfo.CurrentCulture, $"{progress.Message} · {DiskSpace.Format(progress.BytesDone)} of {DiskSpace.Format(progress.BytesTotal)}")
            : progress.Message;

    /// <summary>Whether an operation is in progress.</summary>
    public bool HasProgress => Status.IsBusy && Status.Progress is not null;

    /// <summary>Why the last operation failed.</summary>
    public string? Error => Status.Error;

    /// <summary>Whether the last operation failed.</summary>
    public bool HasError => Status.Error is not null && !Status.IsBusy;

    /// <summary>A note, such as "Uses Python from this computer." or the first install's setup.</summary>
    public string? Note => Status.Note;

    /// <summary>Whether there is a note.</summary>
    public bool HasNote => !string.IsNullOrEmpty(Status.Note) && !HasError && !Status.IsBusy;

    /// <summary>Whether the Install button is shown.</summary>
    public bool ShowsInstall => CanInstall();

    /// <summary>Whether the Update button is shown.</summary>
    public bool ShowsUpdate => CanUpdate();

    /// <summary>Whether the Remove button is shown.</summary>
    public bool ShowsRemove => CanRemove();

    /// <summary>Whether the Cancel button is shown: while it installs or updates.</summary>
    public bool ShowsCancel => CanCancel();

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task InstallAsync() => _store.InstallAsync(Id);

    private bool CanInstall() => _store.CanInstallHere && Status.State == ToolchainState.NotInstalled;

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private Task UpdateAsync() => _store.UpdateAsync(Id);

    private bool CanUpdate() => _store.CanInstallHere && Status.State == ToolchainState.UpdateAvailable;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private Task RemoveAsync() => _store.RemoveAsync(Id);

    private bool CanRemove() => _store.CanInstallHere && Status.IsReady;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _store.Cancel(Id);

    private bool CanCancel() => Status.State is ToolchainState.Installing or ToolchainState.Updating;
}
