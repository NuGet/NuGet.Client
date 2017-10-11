// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.Commands.Rules
{
    internal class InvalidPlaceholderFileRule : IPackageRule
    {
        public IEnumerable<PackageIssue> Validate(PackageBuilder builder)
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

        private static PackageIssue CreatePackageIssueForPlaceholderFile(string target)
        {
            return new PackageIssue(
                AnalysisResources.PlaceholderFileInPackageTitle,
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.PlaceholderFileInPackageDescription, target),
                AnalysisResources.PlaceholderFileInPackageSolution
            );
        }
    }
}