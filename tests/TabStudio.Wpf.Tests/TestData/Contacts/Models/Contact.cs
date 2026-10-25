using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Contacts.Models
{
    /// <summary>A person in the list.</summary>
    public sealed class Contact(string name, string email, bool isFavorite) : INotifyPropertyChanged
    {
        private bool _isFavorite = isFavorite;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name { get; } = name;

        public string Email { get; } = email;

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? property = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
