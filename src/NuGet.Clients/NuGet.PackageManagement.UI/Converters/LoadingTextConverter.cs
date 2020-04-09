using System;
using System.Globalization;
using System.Windows.Data;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    public class LoadingTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var x = value as LoadingStatus?;
            string sal = "xx";

            if (x != null)
                sal = x.ToString();

            return sal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
