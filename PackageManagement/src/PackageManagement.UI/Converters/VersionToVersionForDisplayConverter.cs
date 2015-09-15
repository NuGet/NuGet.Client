// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Windows.Data;
using NuGet.Versioning;
using System.Diagnostics;

namespace NuGet.PackageManagement.UI
{
    public class VersionToVersionForDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var version = value as NuGetVersion;
            return new VersionForDisplay(version, string.Empty);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // no op
            Debug.Assert(false, "Not Implemented");
            return null;
        }
    }
}
