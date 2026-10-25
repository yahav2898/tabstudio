using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels.DesktopMode;

/// <summary>
/// One property in the Properties panel of a XAML element: its value as the XAML has it, edited as text or picked
/// from a list for enums and booleans, and written back to the XAML when committed.
/// </summary>
public sealed partial class XamlPropertyRowViewModel : ObservableObject
{
    private readonly XamlPropertiesViewModel _owner;
    private string _written = string.Empty;

    internal XamlPropertyRowViewModel(XamlPropertiesViewModel owner, string name, string typeName, IReadOnlyList<string>? choices)
    {
        _owner = owner;
        Name = name;
        TypeName = typeName;
        Choices = choices;
    }

    /// <summary>The property's XAML name, such as <c>Margin</c> or <c>Grid.Row</c>.</summary>
    public string Name { get; }

    /// <summary>Its type, such as <c>Thickness</c>.</summary>
    public string TypeName { get; }

    /// <summary>Whether the type says more than the name, which it does not for <c>HorizontalAlignment</c>.</summary>
    public bool ShowsTypeName => !string.Equals(Name, TypeName, StringComparison.Ordinal);

    /// <summary>The values it can take, for enums and booleans.</summary>
    public IReadOnlyList<string>? Choices { get; }

    /// <summary>Whether the XAML sets it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    public partial bool IsSet { get; private set; }

    /// <summary>The value being edited; the XAML's until the user changes it.</summary>
    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether the value is picked from <see cref="Choices"/>: for enums and booleans, unless the XAML sets something
    /// else, such as a binding. Decided when the XAML is read, so the editor does not change while typing.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesText))]
    public partial bool UsesChoices { get; private set; }

    /// <summary>Whether the value is typed.</summary>
    public bool UsesText => !UsesChoices;

    /// <summary>The picked choice, for the drop-down; picking one writes it to the XAML.</summary>
    public string? SelectedChoice
    {
        get => Choices is not null && Choices.Contains(Value, StringComparer.Ordinal) ? Value : null;
        set
        {
            if (value is not null && !string.Equals(value, Value, StringComparison.Ordinal))
            {
                Value = value;
                Commit();
            }
        }
    }

    /// <summary>Whether the search shows this row.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>
    /// Writes the edited value to the XAML, as Enter does, and gives the keyboard back to the IDE on the big screen. An
    /// empty value removes the attribute, as Reset does.
    /// </summary>
    [RelayCommand]
    public void Commit()
    {
        if (string.Equals(Value, _written, StringComparison.Ordinal))
        {
            _owner.FinishEdit();
        }
        else
        {
            _owner.Write(this, Value, finished: true);
        }
    }

    /// <summary>
    /// Writes the edited value when the editor loses the focus, such as to the next property: the keyboard stays where
    /// the user put it.
    /// </summary>
    public void CommitOnLeave()
    {
        if (!string.Equals(Value, _written, StringComparison.Ordinal))
        {
            _owner.Write(this, Value, finished: false);
        }
    }

    /// <summary>Gives up an edit that was not committed, showing the XAML's value again.</summary>
    public void Revert() => Value = _written;

    /// <summary>Takes the property out of the XAML, so it has its default value again.</summary>
    [RelayCommand(CanExecute = nameof(IsSet))]
    private void Reset() => _owner.Write(this, string.Empty, finished: true);

    /// <summary>Shows what the XAML has now. An edit the user has not committed yet is kept.</summary>
    /// <param name="written">The value in the XAML, or empty when it does not set the property.</param>
    /// <param name="isSet">Whether the XAML sets it.</param>
    internal void Show(string written, bool isSet)
    {
        var untouched = string.Equals(Value, _written, StringComparison.Ordinal);
        _written = written;
        IsSet = isSet;
        UsesChoices = Choices is not null && (!isSet || Choices.Contains(written, StringComparer.Ordinal));
        if (untouched)
        {
            Value = written;
        }

        OnPropertyChanged(nameof(SelectedChoice));
    }
}

/// <summary>A group of rows in the Properties panel, such as "Layout", which can be folded.</summary>
public sealed partial class XamlPropertyGroupViewModel : ObservableObject
{
    /// <summary>Creates a group.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="rows">Its rows.</param>
    /// <param name="isExpanded">Whether it starts open.</param>
    public XamlPropertyGroupViewModel(string name, IReadOnlyList<XamlPropertyRowViewModel> rows, bool isExpanded)
    {
        Name = name;
        Rows = rows;
        IsExpanded = isExpanded;
    }

    /// <summary>The group's name, such as "Set in XAML" or "Layout".</summary>
    public string Name { get; }

    /// <summary>Its rows.</summary>
    public IReadOnlyList<XamlPropertyRowViewModel> Rows { get; }

    /// <summary>Whether it is open.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Whether the search leaves any of its rows.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>"Layout · 12", the header's count of rows the search leaves.</summary>
    [ObservableProperty]
    public partial int VisibleCount { get; set; }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    /// <summary>Shows the rows whose name contains the search, and opens the group while searching.</summary>
    /// <param name="search">The search text; empty shows every row.</param>
    /// <param name="expandedWithoutSearch">Whether the group is open when there is no search.</param>
    internal void Filter(string search, bool expandedWithoutSearch)
    {
        var count = 0;
        foreach (var row in Rows)
        {
            row.IsVisible = search.Length == 0 || row.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
            count += row.IsVisible ? 1 : 0;
        }

        VisibleCount = count;
        IsVisible = count > 0;
        IsExpanded = search.Length > 0 || expandedWithoutSearch;
    }
}
