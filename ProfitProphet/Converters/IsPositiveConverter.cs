using System;
using System.Globalization;
using System.Windows.Data;

namespace ProfitProphet.Converters
{
    public class IsPositiveConverter : IValueConverter
    {
        // Convert: return true when a numeric value is positive
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If the number is positive, it returns True.
            // handle double
            if (value is double d) return d > 0;
            // handle decimal
            if (value is decimal dec) return dec > 0;
            // handle int
            if (value is int i) return i > 0;
            // Non-numeric input -> false
            return false;
        }

        // ConvertBack not supported (one-way converter)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Throw to indicate this path is intentionally not implemented
            throw new NotImplementedException();
        }
    }
}