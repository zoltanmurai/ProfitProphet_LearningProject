using System;
using System.Globalization;
using System.Windows.Data;

namespace ProfitProphet.Converters 
{
    // InverseBoolConverter: A simple WPF Value Converter to invert boolean values for UI bindings
    public class InverseBoolConverter : IValueConverter
    {
        // Convert: invert incoming boolean value for bindings
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If input is a boolean, return its negation
            if (value is bool booleanValue)
                return !booleanValue;

            // Non-boolean input -> default to false
            return false;
        }

        // ConvertBack: not supported (one-way converter)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Intentionally not implemented; throw to indicate unsupported operation
            throw new NotImplementedException();
        }
    }
}
