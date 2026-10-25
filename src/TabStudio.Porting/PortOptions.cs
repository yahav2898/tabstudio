namespace TabStudio.Porting;

/// <summary>Where the new Avalonia project goes and what it is built with.</summary>
/// <param name="ProjectName">The new project's name, such as <c>WpfApp1.Avalonia</c>.</param>
/// <param name="ProjectDirectory">The folder of the new project; it must not exist yet, or be empty.</param>
/// <param name="AvaloniaVersion">The version of Avalonia's packages, such as <c>11.3.22</c>.</param>
public sealed record PortOptions(string ProjectName, string ProjectDirectory, string AvaloniaVersion)
{
    /// <summary>The version of <c>Avalonia.Controls.DataGrid</c>, for projects with a data grid; it ships apart from Avalonia.</summary>
    public string DataGridVersion { get; init; } = "11.3.13";
}
