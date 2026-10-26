namespace TabStudio.Porting;

/// <summary>The UI framework of a project that can be ported to Avalonia.</summary>
public enum PortSourceKind
{
    /// <summary>Windows Presentation Foundation: XAML windows and their code-behind.</summary>
    Wpf,

    /// <summary>Windows Forms: forms laid out by the designer in <c>InitializeComponent</c>.</summary>
    WindowsForms,
}
