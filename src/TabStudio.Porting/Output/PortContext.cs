namespace TabStudio.Porting.Output;

/// <summary>The files the port adds to the new project so the app runs the same on Avalonia.</summary>
[Flags]
internal enum SupportNeeds
{
    None = 0,

    /// <summary>Windows that are screens on phones: WindowView and ScreenHost.</summary>
    Windows = 1,

    /// <summary>WPF windows: AppWindow.</summary>
    WpfWindow = 2,

    /// <summary>Windows Forms forms: AppForm.</summary>
    Form = 4,

    /// <summary>WPF's message box.</summary>
    WpfMessageBox = 8,

    /// <summary>Windows Forms' message box.</summary>
    FormsMessageBox = 16,

    /// <summary>WPF's CommandManager.RequerySuggested.</summary>
    CommandManager = 32,

    /// <summary>The rows of a DataGridView filled in code.</summary>
    GridRows = 64,

    /// <summary>The look of group boxes, status bars and toolbars.</summary>
    Styles = 128,
}

/// <summary>What the converter knows and collects while it ports one project.</summary>
internal sealed class PortContext
{
    private readonly Dictionary<string, FileReport> _reports = new(StringComparer.Ordinal);

    /// <summary>Starts the port of a project.</summary>
    public PortContext(PortSource source, PortOptions options)
    {
        Source = source;
        Options = options;
    }

    /// <summary>The project being ported.</summary>
    public PortSource Source { get; }

    /// <summary>Where the new project goes.</summary>
    public PortOptions Options { get; }

    /// <summary>The namespace of the app's code, which the new project keeps.</summary>
    public string RootNamespace => Source.RootNamespace;

    /// <summary>The namespace of the files the port adds.</summary>
    public string SupportNamespace => RootNamespace + ".Porting";

    /// <summary>Which support files the new project needs.</summary>
    public SupportNeeds Support { get; set; }

    /// <summary>Whether the new project shows a data grid, which is a package of its own in Avalonia.</summary>
    public bool UsesDataGrid { get; set; }

    /// <summary>Whether code reads the app's settings with System.Configuration, which is a package on Avalonia.</summary>
    public bool UsesConfiguration { get; set; }

    /// <summary>The reports of the new project's files.</summary>
    public IEnumerable<FileReport> Reports => _reports.Values;

    /// <summary>The report of a file of the new project, created on first use.</summary>
    /// <param name="path">The file, relative with <c>/</c>.</param>
    public FileReport Report(string path)
    {
        if (!_reports.TryGetValue(path, out var report))
        {
            report = new FileReport(path);
            _reports[path] = report;
        }

        return report;
    }

    /// <summary>Records that the new project needs a support file, and the files it builds on.</summary>
    public void Needs(SupportNeeds needs)
    {
        if ((needs & SupportNeeds.FormsMessageBox) != 0)
        {
            needs |= SupportNeeds.Form;
        }

        if ((needs & (SupportNeeds.WpfWindow | SupportNeeds.Form | SupportNeeds.WpfMessageBox)) != 0)
        {
            needs |= SupportNeeds.Windows;
        }

        Support |= needs;
    }
}
