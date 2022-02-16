// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Windows.Data;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    internal class PackageLevelToGroupNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PackageLevel pkgLevel)
            {
                if (pkgLevel == PackageLevel.TopLevel)
                {
                    return Resources.PackageLevel_TopLevelPackageHeaderText;
                }
                else if (pkgLevel == PackageLevel.Transitive)
                {
                    return Resources.PackageLevel_TransitivePackageHeaderText;
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
