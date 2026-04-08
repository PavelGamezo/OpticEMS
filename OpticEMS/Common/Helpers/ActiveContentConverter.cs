using AvalonDock.Layout;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace OpticEMS.Common.Helpers
{
    public class ActiveContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LayoutContent layoutContent)
            {
                if (layoutContent.Content != null)
                    return layoutContent.Content;

                if (layoutContent is LayoutDocument doc && doc.Content != null)
                    return doc.Content;
            }

            return value;
        }
    }
}
