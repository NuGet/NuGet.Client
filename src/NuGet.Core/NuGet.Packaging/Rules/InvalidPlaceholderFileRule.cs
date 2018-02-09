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
        public IEnumerable<PackLogMessage> Validate(PackageBuilder builder)
        {
            foreach (IPackageFile file in builder.Files)
            {
                string path = file.Path;
                if (Path.GetFileName(path).Equals(PackagingCoreConstants.EmptyFolder, StringComparison.Ordinal))
                {
                    string directory = PathUtility.EnsureTrailingSlash(Path.GetDirectoryName(path));
                    if (builder.Files.Count(f => PathUtility.EnsureTrailingSlash(Path.GetDirectoryName(f.Path)).StartsWith(directory, StringComparison.OrdinalIgnoreCase)) > 1)
                    {
                        yield return CreatePackageIssueForPlaceholderFile(path);
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