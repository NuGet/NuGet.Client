// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.Packaging.Rules
{
    internal class InvalidPrereleaseDependencyRule : IPackageRule
    {
        public string MessageFormat { get; }

        public InvalidPrereleaseDependencyRule(string messageFormat)
        {
            MessageFormat = messageFormat;
        }
        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var nuspecReader = builder?.NuspecReader;
            if (nuspecReader.GetDependencyGroups() == null)
            {
                // We have independent validation for null-versions.
                yield break;
            }

            // If the package is stable, and has a prerelease dependency.
            if (!nuspecReader.GetVersion().IsPrerelease)
            {
                // If we are creating a production package, do not allow any of the dependencies to be a prerelease version.
                var prereleaseDependency = nuspecReader.GetDependencyGroups().SelectMany(set => set.Packages).FirstOrDefault(IsPrereleaseDependency);
                if (prereleaseDependency != null)
                {
                    yield return CreatePackageIssueForPrereleaseDependency(prereleaseDependency.ToString());
                }
            }
        }

        private bool IsPrereleaseDependency(PackageDependency dependency)
        {
            return dependency.VersionRange.MinVersion?.IsPrerelease == true ||
                   dependency.VersionRange.MaxVersion?.IsPrerelease == true;
        }

        private PackagingLogMessage CreatePackageIssueForPrereleaseDependency(string dependency)
        {
            return PackagingLogMessage.CreateWarning(
                String.Format(CultureInfo.CurrentCulture, MessageFormat, dependency),
                NuGetLogCode.NU5104);
        }
    }
}