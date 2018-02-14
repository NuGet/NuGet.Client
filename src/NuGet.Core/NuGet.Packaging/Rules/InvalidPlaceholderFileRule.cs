// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.Packaging.Rules
{
    internal class InvalidPlaceholderFileRule : IPackageRule
    {
        public IEnumerable<PackLogMessage> Validate(PackageArchiveReader builder)
        {
            foreach (var file in builder.GetFiles())
            {
                if (Path.GetFileName(file).Equals(PackagingCoreConstants.EmptyFolder, StringComparison.Ordinal))
                {
                    var directory = PathUtility.EnsureTrailingSlash(Path.GetDirectoryName(file));
                    if (builder.GetFiles().Count(f => PathUtility.EnsureTrailingSlash(Path.GetDirectoryName(f)).StartsWith(directory, StringComparison.OrdinalIgnoreCase)) > 1)
                    {
                        yield return CreatePackageIssueForPlaceholderFile(file);
                    }
                }
            }
        }

        private static PackLogMessage CreatePackageIssueForPlaceholderFile(string target)
        {
            return PackLogMessage.CreateWarning(
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.PlaceholderFileInPackageWarning, target),
                NuGetLogCode.NU5109);
        }
    }
}