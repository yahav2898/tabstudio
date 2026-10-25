using System.ComponentModel;
using Avalonia.Controls;
using TabStudio.App.ViewModels;
using TabStudio.Forms.Hosting;

namespace TabStudio.App.Views;

/// <summary>
/// The app preview. A control can only have one parent, so the app's view moves between the full pane and the
/// device frame when the frame changes.
/// </summary>
public sealed partial class AppPreviewView : UserControl
{
    private AppPreviewViewModel? _preview;

    /// <summary>Creates the view.</summary>
    public AppPreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_preview is not null)
        {
            _preview.PropertyChanged -= OnPreviewPropertyChanged;
        }

        _preview = DataContext as AppPreviewViewModel;
        if (_preview is not null)
        {
            _preview.PropertyChanged += OnPreviewPropertyChanged;
        }

        PlaceView();
    }

    private void OnPreviewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppPreviewViewModel.View) or nameof(AppPreviewViewModel.Frame))
        {
            PlaceView();
        }
    }

    /// <summary>Puts the app's view in the full pane or in the device frame, taking it out of the other first.</summary>
    private void PlaceView()
    {
        FillContent.Content = null;
        FramedContent.Content = null;
        if (_preview?.View is { } view)
        {
            if (view is FormsDesktop desktop && !desktop.IsSet(FormsDesktop.WallpaperProperty))
            {
                // A Windows Forms app's desktop follows the IDE's theme, as Windows 11's wallpaper follows dark mode.
                desktop.Bind(FormsDesktop.WallpaperProperty, this.GetResourceObservable("Brush.FormsDesktop"));
            }

            if (_preview.IsFill)
            {
                FillContent.Content = view;
            }
            else
            {
                FramedContent.Content = view;
            }
        }
    }
}
