using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class VersionToVersionForDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            NuGetVersion version = value as NuGetVersion;
            return new VersionForDisplay(version, String.Empty);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
