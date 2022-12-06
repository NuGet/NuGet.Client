// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows.Data;

namespace NuGet.PackageManagement.UI
{
    internal class NotEqualConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            IComparable v = value as IComparable;
            IComparable p = parameter as IComparable;

            if (v == null)
            {
                throw new ArgumentException("Value should not be null and should inherit from IComparable");
            }
            if (p == null)
            {
                throw new ArgumentException("Parameter should not be null and should inherit from IComparable");
            }

            return v.CompareTo(p) != 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
