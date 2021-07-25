// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Windows.Data;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Extracts from a formated string with one '{0}' placeholder to either left or right side of the placeholder
    /// </summary>
    public class FormatedStringPartConverter : IValueConverter
    {
        /// <summary>
        /// Extracts either left or right side of a string with one placeholder '{0}'
        /// </summary>
        /// <param name="value">string with placeholder</param>
        /// <param name="targetType">Not used</param>
        /// <param name="parameter">0 for left side, 2 for right side</param>
        /// <param name="culture">Not used</param>
        /// <returns><c>null </c> if invalid value or parameter, otherwise a string with either left or right side</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string formattedString = value as string;

            if (formattedString == null)
            {
                return null;
            }

            int? mode = parameter as int?;

            if (mode == null)
            {
                return null;
            }

            int placeholderIndex = formattedString.IndexOf("{0}", StringComparison.Ordinal);

            if (placeholderIndex < 0)
            {
                return null;
            }

            if (mode == 0)
            {
                return formattedString.Substring(0, placeholderIndex);
            }

            if (mode == 2)
            {
                return formattedString.Substring(placeholderIndex + "{0}".Length);
            }

            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
