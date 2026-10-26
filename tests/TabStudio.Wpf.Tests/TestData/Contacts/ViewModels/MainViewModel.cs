using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Contacts.Models;

namespace Contacts.ViewModels
{
    /// <summary>The contacts, the one selected, and the form that adds new ones.</summary>
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private string _newName = "New contact";
        private string _newEmail = "new@example.com";
        private Contact? _selected;

        public MainViewModel()
        {
            AddCommand = new RelayCommand(Add, () => !string.IsNullOrWhiteSpace(NewName));
            DeleteCommand = new RelayCommand(Delete, () => Selected is not null);
            Contacts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<Contact> Contacts { get; } =
        [
            new("Ada Lovelace", "ada@example.com", true),
            new("Grace Hopper", "grace@example.com", false),
            new("Alan Turing", "alan@example.com", false),
        ];

        public string NewName
        {
            get => _newName;
            set
            {
                _newName = value;
                OnPropertyChanged();
                AddCommand.RaiseCanExecuteChanged();
            }
        }

        public string NewEmail
        {
            get => _newEmail;
            set
            {
                _newEmail = value;
                OnPropertyChanged();
            }
        }

        public Contact? Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                OnPropertyChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }

        public string Summary => Contacts.Count == 1 ? "1 contact" : $"{Contacts.Count} contacts";

        public RelayCommand AddCommand { get; }

        public RelayCommand DeleteCommand { get; }

        private void Add()
        {
            var contact = new Contact(NewName.Trim(), NewEmail.Trim(), false);
            Contacts.Add(contact);
            Selected = contact;
        }

        private void Delete()
        {
            if (Selected is { } contact
                && MessageBox.Show($"Delete {contact.Name}?", "Contacts", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                Contacts.Remove(contact);
                Selected = null;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? property = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
