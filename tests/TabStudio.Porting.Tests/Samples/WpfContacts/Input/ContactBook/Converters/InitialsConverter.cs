using System.Globalization;
using System.Windows.Data;

namespace ContactBook.Converters
{
    /// <summary>Turns "Ada Lovelace" into "AL", for the avatar.</summary>
    [ValueConversion(typeof(string), typeof(string))]
    public class InitialsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var words = (value as string ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(words.Take(2).Select(w => char.ToUpper(w[0], culture)));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
