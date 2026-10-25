using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Porting;

namespace TabStudio.App.ViewModels;

/// <summary>A completed port, with source links, the single agent task and a reversible workspace addition.</summary>
public sealed partial class PortSummaryViewModel : ObservableObject
{
    private readonly WorkspaceViewModel _workspace;

    public PortSummaryViewModel(WorkspaceViewModel workspace, PortResult result)
    {
        _workspace = workspace;
        Result = result;
    }

    public PortResult Result { get; }
    public string Title => Result.SourceName + " → " + Result.ProjectName;
    public string Summary => $"{Result.Files.Count} files · {Result.ConversionCount} conversions · {Result.Notes.Count} port notes";
    public IReadOnlyList<PortNote> Notes => Result.Notes;

    [ObservableProperty]
    public partial bool IsUndone { get; set; }

    [RelayCommand]
    private Task OpenNoteAsync(PortNote? note) => note is null ? Task.CompletedTask
        : _workspace.NavigateToAsync(Path.Combine(Result.ProjectDirectory, note.File), note.Line, 1);

    [RelayCommand]
    private Task SendNotesAsync() => _workspace.SendPortNotesAsync(this);

    [RelayCommand]
    private Task UndoAsync() => _workspace.UndoPortAsync(this);
}
