using AvalonDock.Layout;
using System.Globalization;
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
                return layoutContent.Content ?? value;
            }

            // Если пришёл ContentPresenter или другой контрол
            if (value is System.Windows.FrameworkElement fe)
            {
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(fe);
                while (parent != null)
                {
                    if (parent is LayoutContent lc)
                        return lc.Content ?? value;

                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
            }

            return value;
        }
    }
}
