// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using static Lucene.Net.Documents.Field;

namespace NuGet.PackageManagement.UI
{
    public class FormatedStringPartConverter : IValueConverter
    {
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

            int index2 = formattedString.IndexOf("{0}", StringComparison.Ordinal);
            if (mode == 0)
            {
                return formattedString.Substring(0, index2);
            }

            if (mode == 2)
            {
                return formattedString.Substring(index2 + "{0}".Length);
            }

            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
