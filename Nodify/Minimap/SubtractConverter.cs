using System;
using System.Globalization;
using System.Windows.Data;

namespace Nodify
{
    internal class SubtractConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count == 2 && values[0] is double a && values[1] is double b)
                return a - b;
            return AvaloniaProperty.UnsetValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
