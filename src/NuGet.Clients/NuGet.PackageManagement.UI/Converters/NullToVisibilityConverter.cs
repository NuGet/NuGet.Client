// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Diagnostics;

namespace NuGet.PackageManagement.UI
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(Visibility))
            {
                if (value == null)
                {
                    return Visibility.Collapsed;
                }

                if (CollapseEmptyString && value.GetType() == typeof(string))
                {
                    return (string)value == string.Empty ? Visibility.Collapsed : Visibility.Visible;
                }

                return Visibility.Visible;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // no op
            Debug.Fail("Not Implemented");
            return null;
        }

        /// <summary>
        /// If a string is null or empty, collapse it. By default, it will only collapse null values (including for strings)
        /// </summary>
        public bool CollapseEmptyString { get; set; }
    }
}
