// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Globalization;
using System.Windows.Data;

namespace NuGet.PackageManagement.UI
{
    public class AcceleratorKeyConverter : IValueConverter
    {
        /// <summary>
        /// Returns char next to the first underscore found
        /// </summary>
        /// <param name="value">A string resource</param>
        /// <param name="targetType">No use</param>
        /// <param name="parameter">No use</param>
        /// <param name="culture">No use</param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return string.Empty;
            }

            string input = value as string;
            bool captureNext = false;
            string captured = string.Empty;

            foreach (var x in input) // foreach takes care of surrogate pairs
            {
                if (x == '_')
                {
                    captureNext = true;
                }
                else if (captureNext)
                {
                    captured = new string(new char[] { x });
                    break;
                }
            }

            return captured;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
