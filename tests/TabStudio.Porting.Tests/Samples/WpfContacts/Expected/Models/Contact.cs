using ContactBook.ViewModels;

namespace ContactBook.Models
{
    /// <summary>A person in the contact book.</summary>
    public class Contact : ViewModelBase
    {
        private bool _isFavorite;
        private int _rating;

        public string Name { get; set; } = string.Empty;

        public string Company { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public bool IsFavorite
        {
            get => _isFavorite;
            set => SetProperty(ref _isFavorite, value);
        }

        public int Rating
        {
            get => _rating;
            set => SetProperty(ref _rating, value);
        }

        public Contact Clone() => (Contact)MemberwiseClone();
    }
}
