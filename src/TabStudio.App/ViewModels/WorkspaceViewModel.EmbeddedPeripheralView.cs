using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Toolchains.Embedded.Peripherals;

namespace TabStudio.App.ViewModels;

/// <summary>One decoded field in an explicitly requested register snapshot.</summary>
/// <param name="Name">The vendor field name.</param>
/// <param name="Bits">Its inclusive bit range.</param>
/// <param name="Value">Decoded value, or an em dash when no current selection was read.</param>
/// <param name="Description">The vendor description and matching enumeration label.</param>
public sealed record EmbeddedRegisterFieldRow(string Name, string Bits, string Value, string Description);

public sealed partial class WorkspaceViewModel
{
    /// <summary>The loaded device's immutable peripheral metadata; browsing never reads the target.</summary>
    public IReadOnlyList<SvdPeripheral> EmbeddedPeripheralCatalog => _embeddedSvd?.Peripherals ?? [];

    /// <summary>The selected peripheral's register metadata.</summary>
    public IReadOnlyList<SvdRegister> EmbeddedRegisterCatalog => SelectedEmbeddedPeripheral?.Registers ?? [];

    /// <summary>The selected register's decoded fields.</summary>
    public ObservableCollection<EmbeddedRegisterFieldRow> EmbeddedRegisterFields { get; } = [];

    /// <summary>The vendor device name, without inferring a chip from a USB identity.</summary>
    public string EmbeddedPeripheralDevice => _embeddedSvd?.Name ?? "Load the selected chip's CMSIS-SVD";

    /// <summary>Whether the Peripherals dock is selected.</summary>
    public bool IsEmbeddedPeripheralsSelected => SelectedPanel == BottomPanel.Peripherals;

    /// <summary>Keeps the dock tab available after loading metadata.</summary>
    public bool ShowsEmbeddedPeripheralsTab => _embeddedSvd is not null || IsEmbeddedPeripheralsSelected;

    /// <summary>The metadata selection. Changing it never performs USB/DAP I/O.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmbeddedRegisterCatalog))]
    public partial SvdPeripheral? SelectedEmbeddedPeripheral { get; set; }

    /// <summary>The register to inspect through an explicit read.</summary>
    [ObservableProperty]
    public partial SvdRegister? SelectedEmbeddedRegister { get; set; }

    /// <summary>Last requested snapshot, labeled with its halt epoch rather than presented as live.</summary>
    [ObservableProperty]
    public partial string EmbeddedPeripheralSnapshot { get; set; } = "Metadata only. Pause the target and choose Read selected register.";

    /// <summary>The selected register's address, access semantics and vendor description.</summary>
    [ObservableProperty]
    public partial string EmbeddedPeripheralDescription { get; set; } = "";

    [ObservableProperty]
    private partial bool EmbeddedPeripheralBusy { get; set; }

    partial void OnSelectedEmbeddedPeripheralChanged(SvdPeripheral? value)
    {
        SelectedEmbeddedRegister = value?.Registers is { Count: > 0 } registers ? registers[0] : null;
    }

    partial void OnSelectedEmbeddedRegisterChanged(SvdRegister? value)
    {
        EmbeddedPeripheralDescription = value is null ? "" : string.Create(CultureInfo.InvariantCulture,
            $"0x{value.Address:X} · {value.SizeBits} bits · {value.Access} · {value.Description}");
        EmbeddedPeripheralSnapshot = "Metadata only; no value has been read for this selection.";
        SetEmbeddedPeripheralFields(value, null);
        NotifyEmbeddedPeripheralCommands();
    }

    partial void OnEmbeddedPeripheralBusyChanged(bool value) => NotifyEmbeddedPeripheralCommands();

    private void RefreshEmbeddedPeripheralCatalog()
    {
        SelectedEmbeddedPeripheral = null;
        OnPropertyChanged(nameof(EmbeddedPeripheralCatalog));
        OnPropertyChanged(nameof(EmbeddedPeripheralDevice));
        OnPropertyChanged(nameof(ShowsEmbeddedPeripheralsTab));
        var peripherals = EmbeddedPeripheralCatalog;
        SelectedEmbeddedPeripheral = peripherals.Count > 0 ? peripherals[0] : null;
    }

    /// <summary>Opens the dock without reading registers or starting a debugger.</summary>
    [RelayCommand]
    private void ShowEmbeddedPeripherals() => SelectPanel(BottomPanel.Peripherals);

    private bool CanReadSelectedEmbeddedRegister() => IsPaused && _adapter is { HasEnded: false } && SelectedEmbeddedRegister is not null && !EmbeddedPeripheralBusy;

    /// <summary>Reads the selected register through the existing side-effect and halt-epoch checks.</summary>
    [RelayCommand(CanExecute = nameof(CanReadSelectedEmbeddedRegister))]
    private async Task ReadSelectedEmbeddedRegisterAsync()
    {
        if (!CanReadSelectedEmbeddedRegister() || SelectedEmbeddedPeripheral is not { } peripheral || SelectedEmbeddedRegister is not { } register) return;
        EmbeddedPeripheralBusy = true;
        EmbeddedPeripheralSnapshot = "Reading the selected halted register; see Output for any refusal.";
        try { await InspectEmbeddedRegisterCoreAsync(peripheral, register, showOutput: false); }
        finally
        {
            if (EmbeddedPeripheralSnapshot.StartsWith("Reading", StringComparison.Ordinal))
                EmbeddedPeripheralSnapshot = "No register snapshot was read. See Output and the status bar for the refusal or error.";
            EmbeddedPeripheralBusy = false;
            ShowEmbeddedPeripherals();
        }
    }

    /// <summary>Edits through the existing reviewed allowlist, exact confirmation, epoch and readback policy.</summary>
    [RelayCommand(CanExecute = nameof(CanReadSelectedEmbeddedRegister))]
    private async Task EditSelectedEmbeddedRegisterAsync()
    {
        if (!CanReadSelectedEmbeddedRegister() || SelectedEmbeddedPeripheral is not { } peripheral || SelectedEmbeddedRegister is not { } register) return;
        EmbeddedPeripheralBusy = true;
        try { await EditEmbeddedRegisterFieldCoreAsync(peripheral, register); }
        finally { EmbeddedPeripheralBusy = false; ShowEmbeddedPeripherals(); }
    }

    private void NotifyEmbeddedPeripheralCommands()
    {
        ReadSelectedEmbeddedRegisterCommand.NotifyCanExecuteChanged();
        EditSelectedEmbeddedRegisterCommand.NotifyCanExecuteChanged();
    }

    private void UpdateEmbeddedPeripheralSnapshot(SvdPeripheral peripheral, SvdRegister register, ulong value, int epoch)
    {
        if (!ReferenceEquals(SelectedEmbeddedPeripheral, peripheral) || !ReferenceEquals(SelectedEmbeddedRegister, register)) return;
        SetEmbeddedPeripheralFields(register, value);
        EmbeddedPeripheralSnapshot = string.Create(CultureInfo.InvariantCulture,
            $"Snapshot 0x{value:X} · halt epoch {epoch}. Not live; read again after stepping or changing cores.");
    }

    private void SetEmbeddedPeripheralFields(SvdRegister? register, ulong? value)
    {
        EmbeddedRegisterFields.Clear();
        if (register is null) return;
        foreach (var field in register.Fields)
        {
            var bits = field.BitWidth == 1 ? field.BitOffset.ToString(CultureInfo.InvariantCulture)
                : string.Create(CultureInfo.InvariantCulture, $"{field.BitOffset + field.BitWidth - 1}:{field.BitOffset}");
            var decoded = value is { } current ? field.Decode(current) : (ulong?)null;
            var label = decoded is { } actual ? field.Values.FirstOrDefault(item => item.Value == actual)?.Name : null;
            EmbeddedRegisterFields.Add(new(field.Name, bits,
                decoded is { } number ? string.Create(CultureInfo.InvariantCulture, $"{number} (0x{number:X})") : "—",
                field.Description + (label is null ? "" : " · " + label)));
        }
    }
}
