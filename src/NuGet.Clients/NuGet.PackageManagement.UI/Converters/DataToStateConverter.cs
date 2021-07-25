// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.VisualStudio.Internal.Contracts;
using System.Windows.Data;
using System.Globalization;

namespace NuGet.PackageManagement.UI
{
    public class DataToStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var deprecation = value as PackageDeprecationMetadataContextInfo;

            if (deprecation == null)
            {
                return MyControlState.Invisible;
            }

            if (deprecation.AlternatePackage != null && !string.IsNullOrEmpty(deprecation.AlternatePackage.PackageId))
            {
                return MyControlState.WithAlternative;
            }

            return MyControlState.Deprecation;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
