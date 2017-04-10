// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace NuGet.PackageManagement.UI 
{
    public class StringFormatConverter : IMultiValueConverter
    {
        // values[0] is the format string
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string formatString = values[0] as string;
            var formattedString = string.Format(formatString, values.Skip(1).ToArray());
            return formattedString;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // no op
            Debug.Fail("Not Implemented");
            return null;
        }
    }
}
