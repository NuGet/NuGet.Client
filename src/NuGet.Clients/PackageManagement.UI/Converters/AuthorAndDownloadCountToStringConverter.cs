// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Diagnostics;
using System.Collections.Generic;

namespace NuGet.PackageManagement.UI
{
    public class AuthorAndDownloadCountToStringConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string author = values[0] as string;
            int? downloadCount = values[1] as int?;

            List<string> strings = new List<string>();
            if (!string.IsNullOrEmpty(author))
            {
                var authorText = string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.Text_ByAuthor,
                    author);
                strings.Add(authorText);
            }

            if (downloadCount.HasValue && downloadCount.Value > 0)
            {
                var s = NumberToString(downloadCount.Value);
                string downloadCountText = String.Format(
                    CultureInfo.CurrentCulture,
                    Resources.Text_Downloads,
                    s);
                strings.Add(downloadCountText);
            }

            return string.Join(", ", strings); ;
        }

        private static readonly string[] _scalingFactor = new string[] {
            "",
            "K", // kilo
            "M", // mega, million
            "G", // giga, billion
            "T"  // tera, trillion
        };

        // Convert numbers into strings like "1.2K", "33.4M" etc.
        // Precondition: number > 0.
        public static string NumberToString(int number)
        {
            double v = (double)number;
            int exp = 0;

            while (v >= 1000)
            {
                v /= 1000;
                ++exp;
            }

            var s = string.Format(
                CultureInfo.CurrentCulture,
                "{0:G3}{1}",
                v,
                _scalingFactor[exp]);
            return s;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // no op
            Debug.Fail("Not Implemented");
            return null;
        }
    }
}
