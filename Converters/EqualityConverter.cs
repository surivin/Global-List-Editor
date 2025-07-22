using System;
using System.Globalization;
using System.Windows.Data;

namespace Global_List_Editor.Converters
{
    public class EqualityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return object.Equals(value, parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value) return parameter;
            return Binding.DoNothing;
        }
    }
}
