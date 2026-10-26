using System.Windows;
using ContactBook.Models;

namespace ContactBook
{
    /// <summary>
    /// Edits a copy of a contact; the caller keeps it when the dialog returns true.
    /// </summary>
    public partial class ContactDialog : Window
    {
        public ContactDialog(Contact contact)
        {
            InitializeComponent();
            Contact = contact;
            DataContext = contact;
        }

        public Contact Contact { get; }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Contact.Name))
            {
                MessageBox.Show("Please enter a name.", "Edit Contact", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameBox.Focus();
                return;
            }

            DialogResult = true;
        }
    }
}
