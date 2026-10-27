using TabStudio.Core.Diagnostics;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The host's Error List on a TabStudio Live guest: the errors of the host's builds, on the guest's copy of the files,
/// beside the guest's own live analysis. Rows that both report are shown once, as the list shows every row once.
/// </summary>
public sealed partial class ErrorListViewModel
{
    private IReadOnlyList<BuildDiagnostic> _liveHost = [];

    /// <summary>Shows the host's Error List, replacing what it showed before; none clears it.</summary>
    /// <param name="diagnostics">The host's rows, on this device's paths.</param>
    public void SetLiveHostDiagnostics(IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _liveHost = diagnostics;
        Refresh();
    }

    /// <summary>The host's rows, for the list.</summary>
    private IEnumerable<BuildDiagnostic> LiveHostDiagnostics => _liveHost;

    /// <summary>This device's own rows, without the host's, as a TabStudio Live host shares them.</summary>
    public IEnumerable<BuildDiagnostic> OwnDiagnostics => AllDiagnostics.Except(_liveHost);
}
