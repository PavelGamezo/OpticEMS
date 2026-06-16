using System.Globalization;
using System.Windows.Data;

namespace OpticEMS.Common.Helpers
{
    public class DoubleToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && d > 0)
            {
                return d.ToString("F2");
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && double.TryParse(str, out double result))
            {
                return result;
            }

            return 0.0;
        }
    }
}
