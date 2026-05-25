using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OpticEMS.Common.Helpers
{
    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isValid)
            {
                string color = parameter?.ToString() == "GreenRed"
                    ? (isValid ? "LimeGreen" : "Red")
                    : (isValid ? "White" : "Red");

                return (Brush)Application.Current.Resources[color]
                       ?? new SolidColorBrush(isValid ? Colors.LimeGreen : Colors.Red);
            }

            return Brushes.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
