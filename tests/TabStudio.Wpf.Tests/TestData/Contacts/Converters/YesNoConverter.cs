using System.Globalization;
using System.Windows.Data;

namespace Contacts.Converters
{
    /// <summary>Shows a flag as Yes or an empty cell.</summary>
    public sealed class YesNoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? "Yes" : string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is string text && text.Length > 0;
    }
}
