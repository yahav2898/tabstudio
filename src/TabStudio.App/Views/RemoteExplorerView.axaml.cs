using Avalonia.Controls;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels;
namespace TabStudio.App.Views;
public sealed partial class RemoteExplorerView : UserControl
{
    public RemoteExplorerView() => InitializeComponent();
    private async void CopyPublicKey(object? sender, RoutedEventArgs args)
    {
        if (DataContext is RemoteExplorerViewModel { PublicKey.Length: > 0 } model && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(model.PublicKey);
            model.Status = "Public key copied. Add it to the server's ~/.ssh/authorized_keys.";
        }
    }
}
