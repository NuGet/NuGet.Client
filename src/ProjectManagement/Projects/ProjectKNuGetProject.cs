using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Microsoft.VisualStudio.ProjectSystem.Interop;
using NuGet.Packaging;
using NuGet.Versioning;
using System.Linq;
using NuGet.Frameworks;

namespace NuGet.ProjectManagement.Projects
{
    public class NuGetPackageMoniker : INuGetPackageMoniker
    {
        public string Id
        {
            get;
            set;
        }

        public string Version
        {
            get;
            set;
        }
    }

    public class ProjectKNuGetProject : NuGetProject
    {
        private INuGetPackageManager _project;

        public ProjectKNuGetProject(INuGetPackageManager project, string projectName)
        {
            _project = project;
            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, projectName);
        }

        private static bool IsCompatible(
            NuGetFramework projectFrameworkName,
            IEnumerable<NuGetFramework> packageSupportedFrameworks)
        {
            if (packageSupportedFrameworks.Any())
            {
                return packageSupportedFrameworks.Any(packageSupportedFramework =>
                    NuGet.Frameworks.DefaultCompatibilityProvider.Instance.IsCompatible(
                        projectFrameworkName,
                        packageSupportedFramework));
            }

            // No supported frameworks means that everything is supported.
            return true;
        }

        public override bool InstallPackage(PackagingCore.PackageIdentity packageIdentity, System.IO.Stream packageStream, INuGetProjectContext nuGetProjectContext)
        {
            if (!packageStream.CanSeek)
            {
                throw new ArgumentException(Strings.PackageStreamShouldBeSeekable);
            }

            packageStream.Seek(0, SeekOrigin.Begin);
            var zipArchive = new ZipArchive(packageStream);
            PackageReader packageReader = new PackageReader(zipArchive);
            var packageSupportedFrameworks = packageReader.GetSupportedFrameworks();
            var projectFrameworks = _project.GetSupportedFrameworksAsync(CancellationToken.None)
                .Result
                .Select(f => NuGetFramework.Parse(f.FullName));

            var args = new Dictionary<string, object>();
            args["Frameworks"] = projectFrameworks.Where(
                projectFramework =>
                    IsCompatible(projectFramework, packageSupportedFrameworks)).ToArray();            
            var task = _project.InstallPackageAsync(
                new NuGetPackageMoniker
                {
                    Id = packageIdentity.Id,
                    Version = packageIdentity.Version.ToNormalizedString()
                },
                args,
                logger: null,
                progress: null,
                cancellationToken: CancellationToken.None);
            task.Wait();
            return true;
        }

        public override bool UninstallPackage(PackagingCore.PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext)
        {
            var args = new Dictionary<string, object>();
            var task = _project.UninstallPackageAsync(
                new NuGetPackageMoniker
                {
                    Id = packageIdentity.Id,
                    Version = packageIdentity.Version.ToNormalizedString()
                },
                args,
                logger: null,
                progress: null,
                cancellationToken: CancellationToken.None);
            task.Wait();
            return true;
        }

        public override IEnumerable<Packaging.PackageReference> GetInstalledPackages()
        {
            var task = _project.GetInstalledPackagesAsync(CancellationToken.None);
            task.Wait();

            var result = new List<Packaging.PackageReference>();
            foreach (object item in task.Result)
            {
                PackagingCore.PackageIdentity identity = null;

                var moniker = item as INuGetPackageMoniker;
                if (moniker != null)
                {
                    identity = new PackagingCore.PackageIdentity(
                        moniker.Id,
                        NuGetVersion.Parse(moniker.Version));
                }
                else
                {
                    // otherwise, item is the file name of the nupkg file
                    var fileName = item as string;
                    using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                    {
                        var zipArchive = new ZipArchive(fileStream);
                        var packageReader = new PackageReader(zipArchive);
                        identity = packageReader.GetIdentity();
                    }
                }

                result.Add(new Packaging.PackageReference(
                        identity,
                        targetFramework: null));
            }

            return result;
        }
    }
}