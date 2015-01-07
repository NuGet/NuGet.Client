using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace NuGet.PackageManagement.UI
{
    public class StyleKeyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter == null)
                return null;

            if (parameter == Brushes.UIText)
            {
                return SystemColors.ActiveCaptionTextBrush;
            }

            if (parameter == Brushes.DetailPaneBackground)
            {
                return SystemColors.WindowFrameColor;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
