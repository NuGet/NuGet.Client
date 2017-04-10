// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.Commands.Rules
{
    internal class InvalidPrereleaseDependencyRule : IPackageRule
    {
        public IEnumerable<PackageIssue> Validate(PackageBuilder builder)
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

        private static PackageIssue CreatePackageIssueForPrereleaseDependency(string dependency)
        {
            return new PackageIssue(
                AnalysisResources.InvalidPrereleaseDependency_Title,
                AnalysisResources.InvalidPrereleaseDependency_Description,
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.InvalidPrereleaseDependency_Solution, dependency)
                
            );
        }
    }
}