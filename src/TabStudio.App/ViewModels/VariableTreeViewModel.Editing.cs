using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels;

/// <summary>Edit Value on a row of the Locals or Watch panel, for programs a debug adapter paused.</summary>
public sealed partial class VariableTreeViewModel
{
    /// <summary>Raised when "Edit Value" is chosen on a row; the workspace asks for the new value and sets it.</summary>
    public event EventHandler<VariableViewModel>? EditValueRequested;

    [RelayCommand]
    private void EditValue(VariableViewModel? row)
    {
        if (row is { IsEditable: true })
        {
            EditValueRequested?.Invoke(this, row);
        }
    }
}
