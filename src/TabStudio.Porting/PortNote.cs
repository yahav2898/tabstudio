namespace TabStudio.Porting;

/// <summary>Where in the new project a port note is.</summary>
public enum PortNoteArea
{
    /// <summary>In a XAML file.</summary>
    Xaml,

    /// <summary>In C# code.</summary>
    Code,

    /// <summary>In the project file or the project as a whole.</summary>
    Project,
}

/// <summary>
/// A spot the converter could not port exactly, for a person or Claude to finish. The converted file keeps a
/// <c>TODO(port)</c> comment at the spot, so the project still builds.
/// </summary>
/// <param name="File">The file in the new project, relative to its folder, with <c>/</c>, such as <c>MainWindow.axaml</c>.</param>
/// <param name="Line">The 1-based line of the spot in that file.</param>
/// <param name="What">What is there, such as <c>DataTrigger on DataGridRow</c>.</param>
/// <param name="Why">Why it was not converted, and what would finish it.</param>
/// <param name="Area">Whether the spot is in XAML, code or the project.</param>
public sealed record PortNote(string File, int Line, string What, string Why, PortNoteArea Area)
{
    /// <summary>The note as one line: file, line, what and why.</summary>
    public override string ToString() => $"{File}:{Line}: {What}. {Why}";
}
