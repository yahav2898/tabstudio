using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels;

/// <summary>A Test Explorer row's Live Unit Testing menu: Include and Exclude.</summary>
public sealed partial class TestNodeViewModel
{
    /// <summary>Includes the row's tests in Live Unit Testing.</summary>
    [RelayCommand]
    private void IncludeInLiveTesting() => (_actions as ILiveTestingNodeActions)?.SetLiveTesting(this, include: true);

    /// <summary>Excludes the row's tests from Live Unit Testing: they never run live.</summary>
    [RelayCommand]
    private void ExcludeFromLiveTesting() => (_actions as ILiveTestingNodeActions)?.SetLiveTesting(this, include: false);
}
