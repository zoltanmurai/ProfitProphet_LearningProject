using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProfitProphet.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        // Convert bool to Visibility (true -> Visible, false -> Collapsed)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If value is a boolean, map true -> Visible, false -> Collapsed.
            if (value is bool b)
                return b ? Visibility.Visible : Visibility.Collapsed;

            // Non-boolean inputs default to Collapsed to avoid showing UI unexpectedly.
            return Visibility.Collapsed;
        }

        // Convert Visibility back to bool (Visible -> true; otherwise false)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Treat only Visible as true; Collapsed/Hidden or non-Visibility inputs -> false.
            return value is Visibility v && v == Visibility.Visible;
        }
    }
}