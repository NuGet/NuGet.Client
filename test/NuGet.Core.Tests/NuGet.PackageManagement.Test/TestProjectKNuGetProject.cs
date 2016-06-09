using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;

namespace NuGet.Test
{
    internal class TestProjectKNuGetProject : ProjectKNuGetProjectBase
    {
        public TestProjectKNuGetProject()
        {
            InternalMetadata.AddRange(CreateMetadata());
        }
        
        private static Dictionary<string, object> CreateMetadata()
        {
            return new Dictionary<string, object>
            {
                { NuGetProjectMetadataKeys.Name, nameof(TestProjectKNuGetProject) },
                { NuGetProjectMetadataKeys.TargetFramework, NuGetFramework.Parse("net45") },
            };
        }

        public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            return Task.FromResult<IEnumerable<PackageReference>>(new PackageReference[0]);
        }

        public override Task<bool> InstallPackageAsync(
            PackageIdentity packageIdentity,
            DownloadResourceResult downloadResourceResult,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            return Task.FromResult(true);
        }

        public override Task<bool> UninstallPackageAsync(
            PackageIdentity packageIdentity,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
