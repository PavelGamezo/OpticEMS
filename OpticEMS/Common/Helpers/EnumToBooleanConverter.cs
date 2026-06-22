using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpticEMS.Common.Helpers
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return DependencyProperty.UnsetValue;

            string checkValue = value.ToString();
            string targetValue = parameter.ToString();

            return string.Equals(checkValue, targetValue, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return DependencyProperty.UnsetValue;

            bool isChecked = (bool)value;
            if (!isChecked)
                return Binding.DoNothing; 

            string targetValue = parameter.ToString();

            try
            {
                return Enum.Parse(targetType, targetValue, true);
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }
}
