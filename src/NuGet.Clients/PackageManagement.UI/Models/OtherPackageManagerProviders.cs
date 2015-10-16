// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Represents other package manager providers that are available for a package in a project.
    /// </summary>
    public class OtherPackageManagerProviders
    {
        public OtherPackageManagerProviders(
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders,
            string packageId,
            string projectName)
        {
            PackageManagerProviders = packageManagerProviders;
            PackageId = packageId;
            ProjectName = projectName;
        }

        public IEnumerable<IVsPackageManagerProvider> PackageManagerProviders
        {
            get;
            private set;
        }

        public string PackageId
        {
            get;
            private set;
        }

        public string ProjectName
        {
            get;
            private set;
        }

        public static async Task<OtherPackageManagerProviders> LoadProvidersInBackground(
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
                return new OtherPackageManagerProviders(
                    otherProviders,
                    packageId,
                    projectName);
            }
        }
    }
}