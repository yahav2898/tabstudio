using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Services;

namespace TabStudio.App.ViewModels.DesktopMode;

/// <summary>A size the IDE can be drawn at on the external display, in the companion's menu.</summary>
public sealed partial class DesktopScaleOption : ObservableObject
{
    /// <summary>Creates the option.</summary>
    /// <param name="scale">The size.</param>
    /// <param name="label">The menu text.</param>
    /// <param name="select">Picks a size; its parameter is <paramref name="scale"/>.</param>
    public DesktopScaleOption(DesktopScale scale, string label, ICommand select)
    {
        Scale = scale;
        Label = label;
        Select = select;
    }

    /// <summary>The size.</summary>
    public DesktopScale Scale { get; }

    /// <summary>The menu text, such as "125%".</summary>
    public string Label { get; }

    /// <summary>Picks this size.</summary>
    public ICommand Select { get; }

    /// <summary>Whether this is the size in use.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
