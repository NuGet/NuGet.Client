using System;
using System.Windows;
using System.Windows.Data;

namespace NuGet.PackageManagement.UI
{
    public class FontSizeConverter : IValueConverter
    {
        // Scaling percentage. E.g. 122 means 122%.
        public int Scale { get; set; }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double v = (double)value;
            return v * Scale / 100.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
