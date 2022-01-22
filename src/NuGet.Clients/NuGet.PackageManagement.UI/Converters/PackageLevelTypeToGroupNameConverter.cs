// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Windows.Data;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    internal class PackageLevelTypeToGroupNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PackageLevelType pkgLevelType)
            {
                if (pkgLevelType == PackageLevelType.TopLevel)
                {
                    return Resources.PackageLevelType_TopLevelPackageHeaderText;
                }
                else if (pkgLevelType == PackageLevelType.Transitive)
                {
                    return Resources.PackageLevelType_TransitivePackageHeaderText;
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string pkgLevelTypeString)
            {
                if (pkgLevelTypeString == Resources.PackageLevelType_TopLevelPackageHeaderText)
                {
                    return PackageLevelType.TopLevel;
                }
                else if (pkgLevelTypeString == Resources.PackageLevelType_TransitivePackageHeaderText)
                {
                    return PackageLevelType.Transitive;
                }
            }

            return null;
        }
    }
}
