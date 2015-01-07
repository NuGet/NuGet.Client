using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// If the value is an empty or null IEnumerable, returns Visibility.Collapsed.
    /// Otherwise, returns Visibility.Visible.
    /// 
    /// When Inverted is true, the returned values are reversed.
    /// </summary>
    public class EnumerableToVisibilityConverter : IValueConverter
    {
        public bool Inverted { get; set; }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (targetType == typeof(Visibility))
            {
                var list = value as IEnumerable;
                var isNullOrEmpty = IsNullOrEmpty(list);
                if (Inverted)
                {
                    isNullOrEmpty = !isNullOrEmpty;
                }

                return isNullOrEmpty ?
                    Visibility.Collapsed :
                    Visibility.Visible;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static bool IsNullOrEmpty(IEnumerable list)
        {
            if (list == null)
            {
                return true;
            }

            var enumerator = list.GetEnumerator();
            return enumerator.MoveNext() == false;
        }
    }
}
