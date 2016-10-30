// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.References;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Versioning;
using NuGet.VisualStudio.Facade.ProjectSystem;
using EnvDTEProject = EnvDTE.Project;
using PackageReference = NuGet.Packaging.PackageReference;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Represents a project object associated with new VS "15" CPS project with package references.
    /// Key feature/difference is the project restore info is pushed by nomination API and stored in 
    /// a cache. Factory method retrieving the info from the cache should be provided.
    /// </summary>
    public class CpsPackageReferenceProject : BuildIntegratedNuGetProject
    {
        private const string TargetFrameworkCondition = "TargetFramework";

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
            UnconfiguredProject unconfiguredProject,
            string projectId)
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
            InternalMetadata.Add(NuGetProjectMetadataKeys.ProjectId, projectId);
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

        public override async Task<bool> ExecuteInitScriptAsync(
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

        public override Task<String> GetAssetsFilePathAsync()
        {
            return Task.FromResult(Path.Combine(
                _packageSpecFactory().RestoreMetadata.OutputPath,
                LockFileFormat.AssetsFileName));
        }

        #region IDependencyGraphProject
        

        public override string MSBuildProjectPath => _projectFullPath;
        
        public override string ProjectName => _projectName;

        public override Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context)
        {
            PackageSpec packageSpec = null;
            if (context == null || !context.PackageSpecCache.TryGetValue(MSBuildProjectPath, out packageSpec))
            {
                packageSpec = _packageSpecFactory();
                if (packageSpec == null)
                {
                    throw new InvalidOperationException(
                        string.Format(Strings.ProjectNotLoaded_RestoreFailed, ProjectName));
                }
                context?.PackageSpecCache.Add(MSBuildProjectPath, packageSpec);
            }

            return Task.FromResult<IReadOnlyList<PackageSpec>>(new[] { packageSpec });
        }

        #endregion

        #region NuGetProject

        public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
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

            return Task.FromResult<IEnumerable<PackageReference>>(installedPackages);
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

        public override async Task<bool> InstallPackageAsync(
            string packageId,
            VersionRange range,
            INuGetProjectContext nuGetProjectContext,
            BuildIntegratedInstallationContext installationContext,
            CancellationToken token)
        {
            // Right now, the UI only handles installation of specific versions, which is just the minimum version of
            // the provided version range.
            var formattedRange = range.MinVersion.ToNormalizedString();

            nuGetProjectContext.Log(MessageLevel.Info, Strings.InstallingPackage, $"{packageId} {formattedRange}");

            if (installationContext.SuccessfulFrameworks.Any() && installationContext.UnsuccessfulFrameworks.Any())
            {   
                // This is the "partial install" case. That is, install the package to only a subset of the frameworks
                // supported by this project.
                var conditionalService = _unconfiguredProject
                    .Services
                    .ExportProvider
                    .GetExportedValue<IConditionalPackageReferencesService>();

                if (conditionalService == null)
                {
                    throw new InvalidOperationException(string.Format(
                        Strings.UnableToGetCPSPackageInstallationService,
                        _projectFullPath));
                }

                foreach (var framework in installationContext.SuccessfulFrameworks)
                {
                    string originalFramework;
                    if (!installationContext.OriginalFrameworks.TryGetValue(framework, out originalFramework))
                    {
                        originalFramework = framework.GetShortFolderName();
                    }

                    await conditionalService.AddAsync(
                        packageId,
                        formattedRange,
                        TargetFrameworkCondition,
                        originalFramework);
                }
            }
            else
            {
                // Install the package to all frameworks.
                var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();

                var result = await configuredProject?
                    .Services
                    .PackageReferences
                    .AddAsync(packageId, formattedRange);

                // This is the update operation
                if (result != null && !result.Added)
                {
                    var existingReference = result.Reference;
                    await existingReference?.Metadata.SetPropertyValueAsync("Version", formattedRange);
                }
            }

            return true;
        }

        public override async Task<Boolean> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();
            await configuredProject?.Services.PackageReferences.RemoveAsync(packageIdentity.Id);
            return true;
        }

        public override async Task<INuGetLock> GetProjectLockAsync()
        {
            return await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var hierarchy = VsHierarchyUtility.ToVsHierarchy(_envDTEProject);

                return ProjectHelper.GetProjectLock(_envDTEProject, hierarchy, _projectFullPath);
            });
        }

        #endregion
    }
}
