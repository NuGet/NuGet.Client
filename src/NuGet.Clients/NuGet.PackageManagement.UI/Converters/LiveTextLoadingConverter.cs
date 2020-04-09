using System;
using System.Globalization;
using System.Windows.Data;

namespace NuGet.PackageManagement.UI
{
    internal class LiveTextLoadingConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {   
            if (values == null || values.Length < 2)
            {
                return null;
            }

            var enabled = values[0] as bool?;
            var text = values[1] as string;

            if (enabled ?? false)
            {
                return text;
            }

            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
