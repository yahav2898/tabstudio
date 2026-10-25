using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels.WindowsBridge;

namespace TabStudio.App.Views.WindowsBridge;

/// <summary>
/// The Windows Bridge pane: draws each new picture of the streamed window as it arrives and tells the bridge, which
/// paces the stream; passes the pane's size and pixel density to the view model; and moves clipboard text between the
/// tablet and the PC.
/// </summary>
public sealed partial class BridgePaneView : UserControl
{
    private BridgePaneViewModel? _viewModel;

    /// <summary>Creates the view.</summary>
    public BridgePaneView()
    {
        InitializeComponent();
        Frame.SizeChanged += (_, e) => UpdatePaneSize(e.NewSize);
        CopyButton.Click += OnCopyFromPc;
        PasteButton.Click += OnPasteToPc;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.FrameReady -= OnFrameReady;
            _viewModel.KeyboardRequested -= OnKeyboardRequested;
        }

        _viewModel = DataContext as BridgePaneViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FrameReady += OnFrameReady;
            _viewModel.KeyboardRequested += OnKeyboardRequested;
            UpdatePaneSize(Frame.Bounds.Size);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdatePaneSize(Frame.Bounds.Size);
    }

    private void OnFrameReady(object? sender, EventArgs e)
    {
        if (Frame.Refresh())
        {
            _viewModel?.OnFrameDrawn();
        }
    }

    private void OnKeyboardRequested(object? sender, EventArgs e) => Frame.ShowKeyboard();

    private void UpdatePaneSize(Size size)
    {
        if (_viewModel is null || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        _viewModel.PaneSize = (size.Width, size.Height);
        _viewModel.RenderScale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
    }

    private async void OnCopyFromPc(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        try
        {
            if (await _viewModel.ReadPcClipboardAsync() is { } text)
            {
                await clipboard.SetTextAsync(text);
            }
        }
        catch (Exception ex) when (ex is TabStudio.Link.LinkRemoteException or TabStudio.Link.LinkConnectionLostException)
        {
            // The pane's status says the PC is gone.
        }
    }

    private async void OnPasteToPc(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        try
        {
            await _viewModel.WritePcClipboardAsync(await clipboard.GetTextAsync());
        }
        catch (Exception ex) when (ex is TabStudio.Link.LinkRemoteException or TabStudio.Link.LinkConnectionLostException)
        {
            // The pane's status says the PC is gone.
        }
    }
}
