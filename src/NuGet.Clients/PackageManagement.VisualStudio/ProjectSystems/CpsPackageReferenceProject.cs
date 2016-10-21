// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.ProjectSystem;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using Tasks = System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;

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
        private readonly EnvDTEProject _envDTEProject;
        private readonly UnconfiguredProject _unconfiguredProject;
        private IScriptExecutor _scriptExecutor;

        public CpsPackageReferenceProject(
            string projectName,
            string projectUniqueName,
            string projectFullPath,
            Func<PackageSpec> packageSpecFactory,
            EnvDTEProject envDTEProject,
            UnconfiguredProject unconfiguredProject
            )
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
            _envDTEProject = envDTEProject;
            _unconfiguredProject = unconfiguredProject;

            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, _projectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, _projectUniqueName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.FullPath, _projectFullPath);
        }

        private IScriptExecutor ScriptExecutor
        {
            get
            {
                if (_scriptExecutor == null)
                {
                    _scriptExecutor = ServiceLocator.GetInstanceSafe<IScriptExecutor>();
                }

                return _scriptExecutor;
            }
        }

        public override async Tasks.Task<bool> ExecuteInitScriptAsync(
            PackageIdentity identity,
            string packageInstallPath,
            INuGetProjectContext projectContext,
            bool throwOnFailure)
        {
            return
                await
                    ScriptExecutorUtil.ExecuteScriptAsync(identity, packageInstallPath, projectContext, ScriptExecutor,
                        _envDTEProject, throwOnFailure);
        }

        public override string AssetsFilePath { get; }

        #region IDependencyGraphProject

        /// <summary>
        /// Making this timestamp as the current time means that a restore with this project in the graph
        /// will never no-op. We do this to keep this work-around implementation simple.
        /// </summary>
        public override DateTimeOffset LastModified => DateTimeOffset.Now;

        public override string MSBuildProjectPath => _projectFullPath;



        public override Tasks.Task<PackageSpec> GetPackageSpecAsync(DependencyGraphCacheContext context)
        {
            PackageSpec packageSpec = null;
            if (context == null || !context.PackageSpecCache.TryGetValue(MSBuildProjectPath, out packageSpec))
            {
                packageSpec = _packageSpecFactory();
                context?.PackageSpecCache.Add(MSBuildProjectPath, packageSpec);
            }

            return Tasks.Task.FromResult<PackageSpec>(packageSpec);
        }

        public override Tasks.Task<DependencyGraphSpec> GetDependencyGraphSpecAsync(DependencyGraphCacheContext context)
        {
            throw new NotImplementedException();
        }

        public override Tasks.Task<IReadOnlyList<IDependencyGraphProject>> GetDirectProjectReferencesAsync(DependencyGraphCacheContext context)
        {
            IReadOnlyList<IDependencyGraphProject> references = null;
            if(context == null || !context.DirectReferenceCache.TryGetValue(MSBuildProjectPath, out references))
            {
                var solutionManager = (VSSolutionManager)ServiceLocator.GetInstance<ISolutionManager>();
                var list = new List<IDependencyGraphProject>();
                if (solutionManager != null)
                {
                    var projReferences = GetProjectReferences(_packageSpecFactory());
                    if (projReferences != null && projReferences.Any())
                    {
                        IList<NuGetProject>  nugetProjReferences = projReferences.Select(r => solutionManager.GetNuGetProject(r)).ToList();
                        references = nugetProjReferences.OfType<IDependencyGraphProject>().ToList().AsReadOnly();
                    }
                }

                context?.DirectReferenceCache.Add(MSBuildProjectPath, references);
            }

            return Tasks.Task.FromResult<IReadOnlyList<IDependencyGraphProject>>(references);
        }

        private static string[] GetProjectReferences(PackageSpec packageSpec)
        {
            return packageSpec?
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

        public override async Tasks.Task<Boolean> InstallPackageAsync(PackageIdentity packageIdentity, DownloadResourceResult downloadResourceResult, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {

            nuGetProjectContext.Log(MessageLevel.Info, Strings.InstallingPackage, packageIdentity);

            var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();
            var result = await
                configuredProject.Services.PackageReferences.AddAsync
                (packageIdentity.Id, packageIdentity.Version.ToString());
            var existingReference = result.Reference;
            if (!result.Added)
            {
                await existingReference.Metadata.SetPropertyValueAsync("Version", packageIdentity.Version.ToFullString());
            }

            return true;
        }

        public override async Tasks.Task<Boolean> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();
            await configuredProject.Services.PackageReferences.RemoveAsync(packageIdentity.Id);
            return true;
        }

        #endregion
    }
}
