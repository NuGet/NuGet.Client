// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    public class IconAndIconUrlUsageWarning : IPackageRule
    {
        public string MessageFormat => throw new NotImplementedException();

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var nuspecReader = builder?.NuspecReader;
            var icon = nuspecReader.GetIcon();
            var iconUrl = nuspecReader.GetIconUrl();

            if (icon != null && iconUrl != null)
            {
                yield return PackagingLogMessage.CreateWarning(
                    string.Format(CultureInfo.CurrentCulture, MessageFormat),
                    NuGetLogCode.NU5049);
            }
        }
    }
}
