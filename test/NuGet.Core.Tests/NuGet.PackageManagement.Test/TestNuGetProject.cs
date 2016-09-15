// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

namespace NuGet.Test
{
    internal class TestNuGetProject : NuGetProject
    {
        IList<Packaging.PackageReference> _installedPackages;

        public TestNuGetProject(IList<Packaging.PackageReference> installedPackages)
            : base(CreateMetadata())
        {
            _installedPackages = installedPackages;
        }

        private static Dictionary<string, object> CreateMetadata()
        {
            return new Dictionary<string, object>
            {
                { NuGetProjectMetadataKeys.Name, nameof(TestNuGetProject) },
                { NuGetProjectMetadataKeys.TargetFramework, NuGetFramework.Parse("net45") },
            };
        }

        public override Task<IEnumerable<Packaging.PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            return Task.FromResult<IEnumerable<Packaging.PackageReference>>(_installedPackages);
        }

        public override Task<bool> InstallPackageAsync(
            PackageIdentity packageIdentity,
            DownloadResourceResult downloadResourceResult,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            return Task.FromResult(true);
        }

        public override Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
