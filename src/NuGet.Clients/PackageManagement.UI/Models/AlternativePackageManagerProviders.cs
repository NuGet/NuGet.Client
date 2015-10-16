// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Represents alternative package manager providers that are available for a package in a project.
    /// E.g. Bower for package jQuery.
    /// </summary>
    public class AlternativePackageManagerProviders
    {
        public AlternativePackageManagerProviders(
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders,
            string packageId,
            string projectName)
        {
            if (packageManagerProviders == null)
            {
                throw new ArgumentNullException(nameof(packageManagerProviders));
            }

            PackageManagerProviders = packageManagerProviders;
            PackageId = packageId;
            ProjectName = projectName;
        }

        public IEnumerable<IVsPackageManagerProvider> PackageManagerProviders
        {
            get;
        }

        public string PackageId
        {
            get;
        }

        public string ProjectName
        {
            get;
        }

        public static async Task<AlternativePackageManagerProviders> CalculateAlternativePackageManagersAsync(
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders,
            string packageId,
            NuGetProject project)
        {
            var otherProviders = new List<IVsPackageManagerProvider>();
            var projectName = NuGetProject.GetUniqueNameOrName(project);

            foreach (var provider in packageManagerProviders)
            {
                bool applicable = await provider.CheckForPackageAsync(
                    packageId,
                    projectName,
                    CancellationToken.None);

                if (applicable)
                {
                    otherProviders.Add(provider);
                }
            }

            if (otherProviders.Count == 0)
            {
                return null;
            }
            else
            {
                return new AlternativePackageManagerProviders(
                    otherProviders,
                    packageId,
                    projectName);
            }
        }
    }
}