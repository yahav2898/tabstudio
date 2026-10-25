using Avalonia.Interactivity;
using ContactBook.Models;
using ContactBook.Porting;

namespace ContactBook
{
    /// <summary>
    /// Edits a copy of a contact; the caller keeps it when the dialog returns true.
    /// </summary>
    public partial class ContactDialog : AppWindow
    {
        public ContactDialog(Contact contact)
        {
            InitializeComponent();
            Contact = contact;
            DataContext = contact;
        }

        public Contact Contact { get; }

        private async void Ok_Click(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Contact.Name))
            {
                await MessageBox.ShowAsync("Please enter a name.", "Edit Contact", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameBox.Focus();
                return;
            }

            DialogResult = true;
        }
    }
}
