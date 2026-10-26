using System.Windows;
using Contacts.ViewModels;

namespace Contacts
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("A sample of WPF data binding, commands and the DataGrid.", "About Contacts");
        }
    }
}
