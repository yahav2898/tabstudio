using System.Globalization;
using Avalonia.Data.Converters;

namespace ContactBook.Converters
{
    public class InitialsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var words = (value as string ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(words.Take(2).Select(w => char.ToUpper(w[0], culture)));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
