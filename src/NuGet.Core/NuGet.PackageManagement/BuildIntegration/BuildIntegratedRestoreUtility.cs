// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Helper class for calling the RestoreCommand
    /// </summary>
    public static class BuildIntegratedRestoreUtility
    {
        public static async Task ExecuteInitPs1ScriptsAsync(
            BuildIntegratedNuGetProject project,
            IEnumerable<PackageIdentity> packages,
            FallbackPackagePathResolver pathResolver,
            INuGetProjectContext projectContext)
        {
            // Find all dependencies in sorted order
            var sortedPackages = await BuildIntegratedProjectUtility.GetOrderedProjectPackageDependencies(project);

            // Keep track of the packages that need to be executed.
            var packagesToExecute = new HashSet<PackageIdentity>(packages, PackageIdentity.Comparer);

            // Use the ordered packages to run init.ps1 for the specified packages.
            foreach (var package in sortedPackages)
            {
                if (packagesToExecute.Remove(package))
                {
                    var packageInstallPath = pathResolver.GetPackageDirectory(package.Id, package.Version);
                    
                    if (packageInstallPath == null)
                    {
                        continue;
                    }

                    await project.ExecuteInitScriptAsync(
                        package,
                        packageInstallPath,
                        projectContext,
                        throwOnFailure: false);
                }
            }
        }

        /// <summary>
        /// Determine what packages need to have their init.ps1 scripts executed based on the provided restore result.
        /// When a restore happens, new packages can be introduced (because the project.json was updated since the last
        /// restore) or existing packages can be installed (because the global packages folder was cleared or a restore
        /// has never been run on an existing project.json). In both of these cases, the init.ps1 scripts should be
        /// executed. Also, the init.ps1 scripts should be executed in dependency order, however it is the
        /// resposibility of <see cref="ExecuteInitPs1ScriptsAsync(BuildIntegratedNuGetProject, IEnumerable{PackageIdentity}, FallbackPackagePathResolver, INuGetProjectContext)"/>
        /// to do this.
        /// </summary>
        /// <param name="restoreResult">The restore result to examine.</param>
        /// <returns>The packages to execute init.ps1 scripts.</returns>
        public static IReadOnlyList<PackageIdentity> GetPackagesToExecuteInitPs1(RestoreResult restoreResult)
        {
            Debug.Assert(restoreResult.Success, "We should not be executing init.ps1 scripts after a failed restore.");

            // Packages added from the previous restore.
            var addedPackages = GetAddedPackages(restoreResult.PreviousLockFile, restoreResult.LockFile);
            
            // Packages that were not installed before.
            var installedPackages = restoreResult
                .GetAllInstalled()
                .Where(library => library.Type == LibraryType.Package)
                .Select(library => new PackageIdentity(library.Name, library.Version));

            // Get unique package identities.
            var newPackages = new HashSet<PackageIdentity>(addedPackages.Concat(installedPackages));
            
            return newPackages.ToList();
        }

        /// <summary>
        /// Find all packages added to <paramref name="updatedLockFile"/>.
        /// </summary>
        public static IReadOnlyList<PackageIdentity> GetAddedPackages(
            LockFile originalLockFile,
            LockFile updatedLockFile)
        {
            IEnumerable<PackageIdentity> updatedPackages;
            if (updatedLockFile != null)
            {
                updatedPackages = updatedLockFile
                    .Targets
                    .SelectMany(target => target.Libraries)
                    .Where(library => library.Type == LibraryType.Package)
                    .Select(library => new PackageIdentity(library.Name, library.Version));
            }
            else
            {
                updatedPackages = Enumerable.Empty<PackageIdentity>();
            }

            IEnumerable<PackageIdentity> originalPackages;
            if (originalLockFile != null)
            {
                originalPackages = originalLockFile
                    .Targets
                    .SelectMany(target => target.Libraries)
                    .Where(library => library.Type == LibraryType.Package)
                    .Select(library => new PackageIdentity(library.Name, library.Version));
            }
            else
            {
                originalPackages = Enumerable.Empty<PackageIdentity>();
            }

            var results = updatedPackages
                .Except(originalPackages, PackageIdentity.Comparer)
                .ToList();

            return results;
        }

        /// <summary>
        /// Find the list of parent projects which directly or indirectly reference the child project.
        /// </summary>
        public static IReadOnlyList<BuildIntegratedNuGetProject> GetParentProjectsInClosure(
            IReadOnlyList<BuildIntegratedNuGetProject> projects,
            BuildIntegratedNuGetProject target,
            DependencyGraphSpec cache)
        {
            if (projects == null)
            {
                throw new ArgumentNullException(nameof(projects));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            var listOfParents = cache.GetParents(target.MSBuildProjectPath);

            var parentNuGetprojects = new HashSet<BuildIntegratedNuGetProject>();

            foreach (var parent in listOfParents)
            {
                // do not count the target as a parent
                var nugetProject = projects.FirstOrDefault(r => r.MSBuildProjectPath == parent);
                if (nugetProject != null && !nugetProject.Equals(target))
                {
                    parentNuGetprojects.Add(nugetProject);
                }
            }

            // sort parents by path to make this more deterministic during restores
            return parentNuGetprojects
                .OrderBy(parent => parent.MSBuildProjectPath, StringComparer.Ordinal)
                .ToList();
        }
    }
}
