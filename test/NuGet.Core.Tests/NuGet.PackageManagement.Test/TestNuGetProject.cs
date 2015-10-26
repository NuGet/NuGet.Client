using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

namespace NuGet.Test
{
    internal class TestNuGetProject : NuGetProject
    {
        IList<NuGet.Packaging.PackageReference> _installedPackages;

        public TestNuGetProject(IList<NuGet.Packaging.PackageReference> installedPackages)
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

        public override Task<IEnumerable<NuGet.Packaging.PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            return Task.FromResult<IEnumerable<NuGet.Packaging.PackageReference>>(_installedPackages);
        }

        public override Task<bool> InstallPackageAsync(PackageIdentity packageIdentity, DownloadResourceResult downloadResourceResult, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
