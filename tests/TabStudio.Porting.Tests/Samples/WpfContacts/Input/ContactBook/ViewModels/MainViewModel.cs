using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using ContactBook.Models;

namespace ContactBook.ViewModels
{
    /// <summary>The contact list, the search and the selected contact.</summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly ICollectionView _view;
        private Contact? _selectedContact;
        private string _searchText = string.Empty;

        public MainViewModel()
        {
            Contacts = new ObservableCollection<Contact>(SampleData.CreateContacts());
            _view = CollectionViewSource.GetDefaultView(Contacts);
            _view.Filter = item => Matches((Contact)item);
            AddCommand = new RelayCommand(_ => Add());
            DeleteCommand = new RelayCommand(item => Delete(item as Contact), item => item is Contact);
        }

        public ObservableCollection<Contact> Contacts { get; }

        public ICommand AddCommand { get; }

        public ICommand DeleteCommand { get; }

        public Contact? SelectedContact
        {
            get => _selectedContact;
            set
            {
                if (SetProperty(ref _selectedContact, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                }
            }
        }

        public bool HasSelection => SelectedContact is not null;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _view.Refresh();
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public bool IsEmpty => !Contacts.Any(Matches);

        public string StatusText => $"{Contacts.Count(Matches)} of {Contacts.Count} contacts";

        public void Replace(Contact original, Contact edited)
        {
            var index = Contacts.IndexOf(original);
            if (index >= 0)
            {
                Contacts[index] = edited;
                SelectedContact = edited;
            }
        }

        private bool Matches(Contact contact) =>
            SearchText.Length == 0
            || contact.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || contact.Company.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

        private void Add()
        {
            var contact = new Contact { Name = "New contact" };
            Contacts.Add(contact);
            SelectedContact = contact;
            OnPropertyChanged(nameof(StatusText));
        }

        private void Delete(Contact? contact)
        {
            if (contact is not null && Contacts.Remove(contact))
            {
                if (SelectedContact == contact)
                {
                    SelectedContact = null;
                }

                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }
}
