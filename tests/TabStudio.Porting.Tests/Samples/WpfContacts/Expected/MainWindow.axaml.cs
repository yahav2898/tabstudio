using Avalonia.Input;
using Avalonia.Interactivity;
using ContactBook.Porting;
using ContactBook.ViewModels;

namespace ContactBook
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AppWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => SearchBox.Focus();
        }

        private MainViewModel ViewModel => (MainViewModel)DataContext;

        private void Edit_Click(object? sender, RoutedEventArgs e) => EditSelected();

        private void ContactsGrid_MouseDoubleClick(object? sender, TappedEventArgs e) => EditSelected();

        private async void EditSelected()
        {
            if (ViewModel.SelectedContact is not { } contact)
            {
                return;
            }

            var dialog = new ContactDialog(contact.Clone()) { Owner = this };
            if (await dialog.ShowDialogAsync() == true)
            {
                ViewModel.Replace(contact, dialog.Contact);
            }
        }

        private async void About_Click(object? sender, RoutedEventArgs e)
        {
            await MessageBox.ShowAsync(this, $"Contact Book {AppInfo.Version}\nA small WPF sample.", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
