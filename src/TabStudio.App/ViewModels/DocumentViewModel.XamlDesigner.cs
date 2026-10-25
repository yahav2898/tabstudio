namespace TabStudio.App.ViewModels;

/// <summary>The XAML designer of an Avalonia XAML file.</summary>
public sealed partial class DocumentViewModel
{
    /// <summary>
    /// The designer that previews this file, when it is the XAML of an Avalonia app; its tab then shows the
    /// preview beside the code.
    /// </summary>
    public XamlDesignerViewModel? Designer { get; internal set; }

    /// <summary>Called by the designer when the code is shown or hidden, which shows or hides the editing actions.</summary>
    internal void OnDesignerLayoutChanged() => OnPropertyChanged(nameof(ShowsEditorActions));
}
