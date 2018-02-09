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
        public IEnumerable<PackLogMessage> Validate(PackageBuilder builder)
        {
            if (builder?.DependencyGroups == null)
            {
                // We have independent validation for null-versions.
                yield break;
            }

            if (!builder.Version.IsPrerelease)
            {
                // If we are creating a production package, do not allow any of the dependencies to be a prerelease version.
                var prereleaseDependency = builder.DependencyGroups.SelectMany(set => set.Packages).FirstOrDefault(IsPrereleaseDependency);
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

        private static PackLogMessage CreatePackageIssueForPrereleaseDependency(string dependency)
        {
            return PackLogMessage.CreateWarning(
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.InvalidPrereleaseDependencyWarning, dependency),
                NuGetLogCode.NU5104);
        }
    }
}