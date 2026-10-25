using System.Windows;
using System.Windows.Input;
using ContactBook.ViewModels;

namespace ContactBook
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => SearchBox.Focus();
        }

        private MainViewModel ViewModel => (MainViewModel)DataContext;

        private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

        private void ContactsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

        private void EditSelected()
        {
            if (ViewModel.SelectedContact is not { } contact)
            {
                return;
            }

            var dialog = new ContactDialog(contact.Clone()) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                ViewModel.Replace(contact, dialog.Contact);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, $"Contact Book {AppInfo.Version}\nA small WPF sample.", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
