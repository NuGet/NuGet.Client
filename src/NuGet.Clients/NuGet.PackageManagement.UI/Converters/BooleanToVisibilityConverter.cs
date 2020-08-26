// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// This BooleanToVisibility converter allows us to override the converted value when
    /// the bound value is false.
    /// The built-in converter in WPF restricts us to always use Collapsed when the bound
    /// value is false.
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public bool Inverted { get; set; }
        public Visibility NonVisualState { get; set; } = Visibility.Collapsed;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var boolValue = (bool)value;
            if (Inverted)
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : NonVisualState;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // no op
            Debug.Fail("Not Implemented");
            return null;
        }
    }
}
