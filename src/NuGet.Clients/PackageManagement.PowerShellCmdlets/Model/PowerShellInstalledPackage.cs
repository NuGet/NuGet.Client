// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement.UI;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Versioning;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// Represent the view of packages installed to project(s)
    /// </summary>
    public class PowerShellInstalledPackage : PowerShellPackage
    {
        public string ProjectName { get; set; }

        /// <summary>
        /// Get the view of installed packages. Use for Get-Package command.
        /// </summary>
        internal static List<PowerShellInstalledPackage> GetPowerShellPackageView(Dictionary<NuGetProject, IEnumerable<Packaging.PackageReference>> dictionary,
                                                                                  ISolutionManager SolutionManager, Configuration.ISettings settings)
        {
            var views = new List<PowerShellInstalledPackage>();

            foreach (var entry in dictionary)
            {
                var nugetProject = entry.Key;

                string packageFolder = null;
                FolderNuGetProject packageFolderProject = null;

                if (nugetProject is BuildIntegratedNuGetProject)
                {
                    packageFolder = BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(SolutionManager.SolutionDirectory, settings);
                }
                else
                {
                    var project = nugetProject as MSBuildNuGetProject;

                    if (project != null)
                    {
                        packageFolderProject = project.FolderNuGetProject;
                    }
                }

                // entry.Value is an empty list if no packages are installed
                foreach (var package in entry.Value)
                {
                    string installPackagePath = null;
                    string licenseUrl = null;

                    if (packageFolder != null)
                    {
                        installPackagePath = BuildIntegratedProjectUtility.GetPackagePathFromGlobalSource(packageFolder, package.PackageIdentity);
                    }
                    else if (packageFolderProject != null)
                    {
                        installPackagePath = packageFolderProject.GetInstalledPackageFilePath(package.PackageIdentity);
                    }

                    using (var reader = GetPackageReader(installPackagePath, package.PackageIdentity))   
                    {
                        var nuspecReader = new NuspecReader(reader.GetNuspec());
                        licenseUrl = nuspecReader.GetLicenseUrl();
                    }


                    var view = new PowerShellInstalledPackage()
                    {
                        Id = package.PackageIdentity.Id,
                        AsyncLazyVersions = new AsyncLazy<IEnumerable<NuGetVersion>>(() => Task.FromResult<IEnumerable<NuGetVersion>>(new[] { package.PackageIdentity.Version }), NuGetUIThreadHelper.JoinableTaskFactory),
                        ProjectName = entry.Key.GetMetadata<string>(NuGetProjectMetadataKeys.Name),
                        LicenseUrl = licenseUrl
                    };

                    views.Add(view);
                }
            }

            return views;
        }

        private static PackageReaderBase GetPackageReader(string installPath, PackageIdentity package)
        {
            FileInfo nupkg = null;
            if (Directory.Exists(installPath))
            {
                nupkg = new FileInfo(
                        Path.Combine(installPath, package.Id + "." + package.Version + PackagingCoreConstants.NupkgExtension));
            }

            if (File.Exists(installPath))
            {
                nupkg = new FileInfo(installPath);
            }

            if (nupkg!= null && nupkg.Exists)
            {
                return new PackageArchiveReader(nupkg.OpenRead());
            }

            return null;
        }
    }
}
