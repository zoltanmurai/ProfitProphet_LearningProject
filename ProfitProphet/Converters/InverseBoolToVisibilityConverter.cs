using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProfitProphet.Converters
{
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        // Convert: invert bool to Visibility (true -> Collapsed, false -> Visible)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If value is true -> Collapsed, otherwise Visible
            return (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
        }

        // ConvertBack: map Visibility back to inverted bool (Collapsed/Hidden -> true, Visible -> false)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Return true when visibility is not Visible
            return value is Visibility v && v != Visibility.Visible;
        }
    }
}
