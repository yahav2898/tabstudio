using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>A compile error or warning listed under a code cell. Tapping it moves the caret to it.</summary>
public sealed partial class CellDiagnosticViewModel
{
    private readonly NotebookCellViewModel _cell;

    /// <summary>Creates the row.</summary>
    /// <param name="diagnostic">The diagnostic, with a location relative to the cell.</param>
    /// <param name="cell">The cell it belongs to.</param>
    public CellDiagnosticViewModel(BuildDiagnostic diagnostic, NotebookCellViewModel cell)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(cell);
        Diagnostic = diagnostic;
        _cell = cell;
    }

    /// <summary>The diagnostic.</summary>
    public BuildDiagnostic Diagnostic { get; }

    /// <summary>The diagnostic id, such as <c>CS0103</c>.</summary>
    public string Code => Diagnostic.Id;

    /// <summary>What is wrong.</summary>
    public string Message => Diagnostic.Message;

    /// <summary>Whether this is an error rather than a warning.</summary>
    public bool IsError => Diagnostic.Severity == DiagnosticSeverityLevel.Error;

    /// <summary>Whether this is a warning.</summary>
    public bool IsWarning => Diagnostic.Severity == DiagnosticSeverityLevel.Warning;

    /// <summary>Where in the cell, such as "Ln 2, Col 5", or empty when the cell as a whole is meant.</summary>
    public string LocationText => Diagnostic.Location is { } location
        ? string.Create(CultureInfo.CurrentCulture, $"Ln {location.Line}, Col {location.Column}")
        : string.Empty;

    [RelayCommand]
    private void Navigate()
    {
        if (Diagnostic.Location is { } location)
        {
            _cell.NavigateTo(location.Line, location.Column);
        }
    }
}
