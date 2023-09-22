using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace NuGet.PackageManagement.UI
{
    internal class GroupTypeToExpanderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            CollectionViewGroup grp = (CollectionViewGroup)value;
            if (grp.Name.ToString().Equals("TopLevel", StringComparison.OrdinalIgnoreCase) || grp.Name.ToString().Equals("Transitive", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // no op
            Debug.Fail("Not Implemented");
            return null;
        }
    }
}
