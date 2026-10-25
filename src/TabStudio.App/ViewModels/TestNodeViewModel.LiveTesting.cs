using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels;

/// <summary>A Test Explorer row's Live Unit Testing menu, Include and Exclude, and whether the row is excluded.</summary>
public sealed partial class TestNodeViewModel
{
    /// <summary>
    /// Whether Live Unit Testing is on and runs none of the row's tests because they are excluded: the row then shows
    /// a struck-out flask.
    /// </summary>
    [ObservableProperty]
    public partial bool IsExcludedFromLiveTesting { get; private set; }

    /// <summary>Recomputes <see cref="IsExcludedFromLiveTesting"/>, after the row's tests or the include and exclude list changed.</summary>
    internal void RefreshLiveTesting() =>
        IsExcludedFromLiveTesting = (_actions as ILiveTestingNodeActions)?.IsExcludedFromLiveTesting(this) == true;

    /// <summary>Includes the row's tests in Live Unit Testing.</summary>
    [RelayCommand]
    private void IncludeInLiveTesting() => (_actions as ILiveTestingNodeActions)?.SetLiveTesting(this, include: true);

    /// <summary>Excludes the row's tests from Live Unit Testing: they never run live.</summary>
    [RelayCommand]
    private void ExcludeFromLiveTesting() => (_actions as ILiveTestingNodeActions)?.SetLiveTesting(this, include: false);
}
