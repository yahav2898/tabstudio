using System.ComponentModel;
using TabStudio.App.Services;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The Properties panel under the Solution Explorer: folded into its header unless the user opens it, which the session
/// remembers, and hidden while a designer shows Properties of its own, so two never show at once.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>
    /// Whether the side bar shows its Properties panel: not while the Windows Forms designer, which has Properties of
    /// its own beside the form, is the active document; the Solution Explorer then takes the whole side bar.
    /// </summary>
    public bool ShowsSideBarProperties => ActiveDocument?.FormsDesigner is null;

    /// <summary>Saves the session when Properties folds or opens. Called once by the constructor.</summary>
    private void InitializeProperties() => Properties.PropertyChanged += OnPropertiesPropertyChanged;

    /// <summary>Opens or folds Properties as the saved session had it. Called by <see cref="LoadAsync"/>.</summary>
    private void RestoreShellState(SessionState? session)
    {
        if (session?.IsPropertiesExpanded is { } expanded)
        {
            Properties.IsExpanded = expanded;
        }
    }

    /// <summary>Adds the shell's state, such as whether Properties is open, to a session about to be saved.</summary>
    private SessionState WithShellState(SessionState state) => state with { IsPropertiesExpanded = Properties.IsExpanded };

    private void OnPropertiesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PropertiesViewModel.IsExpanded))
        {
            ScheduleSessionSave();
        }
    }
}
