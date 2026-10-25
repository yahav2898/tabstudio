using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Ci;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.ViewModels;

/// <summary>An Error List row that CI reported, with the check it came from.</summary>
public sealed partial record ErrorItemViewModel
{
    /// <summary>Where CI reported it, for rows from CI; <see langword="null"/> for the IDE's own diagnostics.</summary>
    public CiDiagnostic? Ci { get; init; }

    /// <summary>Whether CI reported it.</summary>
    public bool IsFromCi => Ci is not null;

    /// <summary>"Build and test · 1a2b3c4", for the row's CI badge.</summary>
    public string? CiSource => Ci is { } ci ? $"{ci.CheckName} · {ci.ShortSha}" : null;

    /// <summary>The CI badge's tooltip.</summary>
    public string? CiToolTip => Ci is { } ci ? $"Reported by CI: the check \"{ci.CheckName}\" on commit {ci.ShortSha}" : null;
}

/// <summary>
/// CI's errors and warnings in the Error List: the annotations of the workspace commit's failed checks, with a
/// "CI" badge and the check's name, and a filter button to show or hide them. Problems the IDE also finds itself
/// are listed once, as the IDE's.
/// </summary>
public sealed partial class ErrorListViewModel
{
    private IReadOnlyList<CiDiagnostic> _ci = [];
    private Dictionary<BuildDiagnostic, CiDiagnostic> _ciByDiagnostic = new(ReferenceEqualityComparer.Instance);

    /// <summary>Whether CI's errors and warnings are listed.</summary>
    [ObservableProperty]
    public partial bool ShowCi { get; set; } = true;

    /// <summary>Whether CI reported any problem in the workspace's files.</summary>
    public bool HasCiDiagnostics => _ci.Count > 0;

    /// <summary>"CI (3)", for the filter button.</summary>
    public string CiLabel => $"CI ({_ci.Count.ToString(System.Globalization.CultureInfo.CurrentCulture)})";

    /// <summary>Shows CI's errors and warnings, replacing the previous ones.</summary>
    /// <param name="diagnostics">The diagnostics; none clears them.</param>
    public void SetCiDiagnostics(IReadOnlyList<CiDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _ci = diagnostics;
        _ciByDiagnostic = new Dictionary<BuildDiagnostic, CiDiagnostic>(ReferenceEqualityComparer.Instance);
        foreach (var diagnostic in diagnostics)
        {
            _ciByDiagnostic[diagnostic.Diagnostic] = diagnostic;
        }

        OnPropertyChanged(nameof(HasCiDiagnostics));
        OnPropertyChanged(nameof(CiLabel));
        Refresh();
    }

    /// <summary>Where CI reported a diagnostic, when it did.</summary>
    /// <param name="diagnostic">A diagnostic of a row.</param>
    public CiDiagnostic? CiSourceOf(BuildDiagnostic diagnostic) =>
        diagnostic is not null && _ciByDiagnostic.TryGetValue(diagnostic, out var source) ? source : null;

    [RelayCommand]
    private void ToggleCi() => ShowCi = !ShowCi;

    partial void OnShowCiChanged(bool value) => Refresh();

    /// <summary>CI's diagnostics to list, unless hidden, without those the IDE reports itself at the same line.</summary>
    private IEnumerable<BuildDiagnostic> VisibleCiDiagnostics(IReadOnlyList<BuildDiagnostic> local)
    {
        if (!ShowCi || _ci.Count == 0)
        {
            return [];
        }

        var known = local
            .Where(d => d.Location is not null)
            .Select(d => (d.Location!.FilePath, d.Location.Line, d.Message))
            .ToHashSet();
        return _ci.Select(c => c.Diagnostic).Where(d => d.Location is null || !known.Contains((d.Location.FilePath, d.Location.Line, d.Message)));
    }

    private ErrorItemViewModel CreateItem(BuildDiagnostic diagnostic) =>
        new(diagnostic) { Ci = _ciByDiagnostic.TryGetValue(diagnostic, out var ci) ? ci : null, Layout = LayoutFindingOf(diagnostic), BridgePc = BridgePcOf(diagnostic) };
}
