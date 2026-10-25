using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels;

/// <summary>A choice in the Test Explorer's "Group By" menu.</summary>
public sealed partial class TestGroupByOption : ObservableObject
{
    /// <summary>Creates the choice.</summary>
    /// <param name="value">The grouping.</param>
    /// <param name="name">Its name in the menu.</param>
    /// <param name="select">Chooses it.</param>
    public TestGroupByOption(TestGroupBy value, string name, IRelayCommand select)
    {
        Value = value;
        Name = name;
        Select = select;
    }

    /// <summary>The grouping.</summary>
    public TestGroupBy Value { get; }

    /// <summary>Its name in the menu.</summary>
    public string Name { get; }

    /// <summary>Chooses it.</summary>
    public IRelayCommand Select { get; }

    /// <summary>Whether it is the current grouping.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
