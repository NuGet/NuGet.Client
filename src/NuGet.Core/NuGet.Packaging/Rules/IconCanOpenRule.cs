// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{

    /// <summary>
    /// Validates that the icon specified in the .nuspec can be reachable
    /// </summary>
    public class IconCanOpenRule : IPackageRule
    {
        public string MessageFormat { get; }

        public IconCanOpenRule(string messageFormat)
        {
            MessageFormat = messageFormat ?? throw new ArgumentNullException(nameof(messageFormat));
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            // Try open icon file
            NuspecReader nureader = builder?.NuspecReader;

            string iconPath = nureader?.GetIcon();

            if (!string.IsNullOrEmpty(iconPath))
            {
                var stream = builder.GetStream(iconPath);

                if (stream == null)
                {
                    yield return PackagingLogMessage.CreateError(
                        string.Format(CultureInfo.CurrentCulture, MessageFormat, iconPath),
                        NuGetLogCode.NU5036);
                }
                
            }
        }
    }
}
