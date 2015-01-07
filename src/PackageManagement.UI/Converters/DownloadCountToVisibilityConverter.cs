using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NuGet.PackageManagement.UI
{
    public class DownloadCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(Visibility))
            {
                if (value is int && (int)value >= 0)
                {
                    return Visibility.Visible;
                }
                
                return Visibility.Collapsed;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}