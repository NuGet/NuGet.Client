// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Threading;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using Tasks = System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Represents a project object associated with new VS "15" CPS project with package references.
    /// Key feature/difference is the project restore info is pushed by nomination API and stored in 
    /// a cache. Factory method retrieving the info from the cache should be provided.
    /// </summary>
    public class CpsPackageReferenceProject : BuildIntegratedNuGetProject
    {
        private readonly string _projectName;
        private readonly string _projectUniqueName;
        private readonly string _projectFullPath;

        private readonly Func<PackageSpec> _packageSpecFactory;

        public CpsPackageReferenceProject(
            string projectName,
            string projectUniqueName,
            string projectFullPath,
            Func<PackageSpec> packageSpecFactory)
        {
            if (projectFullPath == null)
            {
                throw new ArgumentNullException(nameof(projectFullPath));
            }

            if (packageSpecFactory == null)
            {
                throw new ArgumentNullException(nameof(packageSpecFactory));
            }

            _projectName = projectName;
            _projectUniqueName = projectUniqueName;
            _projectFullPath = projectFullPath;

            _packageSpecFactory = packageSpecFactory;

            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, _projectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, _projectUniqueName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.FullPath, _projectFullPath);
        }

        #region IDependencyGraphProject

        public override string AssetsFilePath { get; }

        /// <summary>
        /// Making this timestamp as the current time means that a restore with this project in the graph
        /// will never no-op. We do this to keep this work-around implementation simple.
        /// </summary>
        public override DateTimeOffset LastModified => DateTimeOffset.Now;

        public override string MSBuildProjectPath => _projectFullPath;



        public override Tasks.Task<PackageSpec> GetPackageSpecAsync(DependencyGraphCacheContext context)
        {
            throw new NotImplementedException();
        }

        public override Tasks.Task<DependencyGraphSpec> GetDependencyGraphSpecAsync(DependencyGraphCacheContext context)
        {
            throw new NotImplementedException();
        }

        public override Tasks.Task<IReadOnlyList<IDependencyGraphProject>> GetDirectProjectReferencesAsync(DependencyGraphCacheContext context)
        {
            throw new NotImplementedException();
        }

        //public IReadOnlyList<PackageSpec> GetPackageSpecsForRestore(
        //    DependencyGraphCacheContext context)
        //{
        //    var packageSpec = _packageSpecFactory();
        //    if (packageSpec != null)
        //    {
        //        return new[] { packageSpec };
        //    }

        //    return new PackageSpec[0];
        //}

        //public async Tasks.Task<IReadOnlyList<ExternalProjectReference>> GetProjectReferenceClosureAsync(
        //    DependencyGraphCacheContext context)
        //{
        //    await Tasks.TaskScheduler.Default;

        //    var externalProjectReferences = new HashSet<ExternalProjectReference>();

        //    var packageSpec = _packageSpecFactory();
        //    if (packageSpec != null)
        //    {
        //        var projectReferences = GetProjectReferences(packageSpec);

        //        var reference = new ExternalProjectReference(
        //            packageSpec.RestoreMetadata.ProjectPath,
        //            packageSpec,
        //            packageSpec.RestoreMetadata.ProjectPath,
        //            projectReferences);

        //        externalProjectReferences.Add(reference);
        //    }

        //    return DependencyGraphProjectCacheUtility
        //        .GetExternalClosure(_projectFullPath, externalProjectReferences)
        //        .ToList();
        //}

        private static string[] GetProjectReferences(PackageSpec packageSpec)
        {
            return packageSpec
                .TargetFrameworks
                .SelectMany(f => GetProjectReferences(f.Dependencies, f.FrameworkName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IEnumerable<string> GetProjectReferences(IEnumerable<LibraryDependency> libraries, NuGetFramework targetFramework)
        {
            return libraries
                .Where(l => l.LibraryRange.TypeConstraint == LibraryDependencyTarget.ExternalProject)
                .Select(l => l.Name);
        }

        public override Tasks.Task<bool> IsRestoreRequired(IEnumerable<VersionFolderPathResolver> pathResolvers, ISet<PackageIdentity> packagesChecked, DependencyGraphCacheContext context)
        {
            // TODO: when the real implementation of NuGetProject for CPS PackageReference is completed, more
            // sophisticated restore no-op detection logic is required. Always returning true means that every build
            // will result in a restore.

            var packageSpec = _packageSpecFactory();
            return Tasks.Task.FromResult<bool>(packageSpec != null);
        }

        public override Tasks.Task<bool> ExecuteInitScriptAsync(PackageIdentity identity, string packageInstallPath, INuGetProjectContext projectContext,
            bool throwOnFailure)
        {
            throw new NotImplementedException();
        }

        public override string ProjectName { get; }

        #endregion

        #region NuGetProject

        public override Tasks.Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            PackageReference[] installedPackages;

            var packageSpec = _packageSpecFactory();
            if (packageSpec != null)
            {
                installedPackages = GetPackageReferences(packageSpec);
            }
            else
            {
                installedPackages = new PackageReference[0];
            }

            return Tasks.Task.FromResult<IEnumerable<PackageReference>>(installedPackages);
        }

        private static PackageReference[] GetPackageReferences(PackageSpec packageSpec)
        {
            var frameworkSorter = new NuGetFrameworkSorter();

            return packageSpec
                .TargetFrameworks
                .SelectMany(f => GetPackageReferences(f.Dependencies, f.FrameworkName))
                .GroupBy(p => p.PackageIdentity)
                .Select(g => g.OrderBy(p => p.TargetFramework, frameworkSorter).First())
                .ToArray();
        }

        private static IEnumerable<PackageReference> GetPackageReferences(IEnumerable<LibraryDependency> libraries, NuGetFramework targetFramework)
        {
            return libraries
                .Where(l => l.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package)
                .Select(l => ToPackageReference(l, targetFramework));
        }

        private static PackageReference ToPackageReference(LibraryDependency library, NuGetFramework targetFramework)
        {
            var identity = new PackageIdentity(
                library.LibraryRange.Name,
                library.LibraryRange.VersionRange.MinVersion);

            return new PackageReference(identity, targetFramework);
        }

        public override Tasks.Task<bool> InstallPackageAsync(PackageIdentity packageIdentity, DownloadResourceResult downloadResourceResult, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Tasks.Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
