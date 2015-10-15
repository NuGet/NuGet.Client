// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class InstalledVersionsToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var installedVersions = value as ICollection<NuGetVersion>;
            if (installedVersions == null || installedVersions.IsEmpty())
            {
                return Resources.Text_NotInstalled;
            }

            if (installedVersions.Count == 1)
            {
                var versionForDisplay = new VersionForDisplay(
                    installedVersions.First(),
                    string.Empty);
                return versionForDisplay.ToString();
            }

            // multiple versions installed
            return Resources.Text_MultipleVersionsInstalled;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // no op
            Debug.Fail("Not Implemented");
            return null;
        }
    }
}