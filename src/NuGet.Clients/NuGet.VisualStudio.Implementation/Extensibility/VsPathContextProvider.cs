// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.LibraryModel;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;

namespace NuGet.VisualStudio
{
    // Implementation of IVsPathContextProvider as a MEF-exported component.
    [Export(typeof(IVsPathContextProvider))]
    public sealed class VsPathContextProvider : IVsPathContextProvider
    {
        private readonly Lazy<ISettings> _settings;
        private readonly Lazy<IVsSolutionManager> _solutionManager;
        private readonly Func<BuildIntegratedNuGetProject, Task<LockFile>> _getLockFileOrNullAsync;

        [ImportingConstructor]
        public VsPathContextProvider(
            Lazy<ISettings> settings,
            Lazy<IVsSolutionManager> solutionManager)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            _settings = settings;
            _solutionManager = solutionManager;
            _getLockFileOrNullAsync = BuildIntegratedProjectUtility.GetLockFileOrNull;
        }

        /// <summary>
        /// This constructor is just used for testing.
        /// </summary>
        public VsPathContextProvider(
            ISettings settings,
            IVsSolutionManager solutionManager,
            Func<BuildIntegratedNuGetProject, Task<LockFile>> getLockFileOrNullAsync)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            _settings = new Lazy<ISettings>(() => settings);
            _solutionManager = new Lazy<IVsSolutionManager>(() => solutionManager);
            _getLockFileOrNullAsync = getLockFileOrNullAsync ?? BuildIntegratedProjectUtility.GetLockFileOrNull;
        }

        public bool TryCreateContext(string projectUniqueName, out IVsPathContext outputPathContext)
        {
            if (projectUniqueName == null)
            {
                throw new ArgumentNullException(nameof(projectUniqueName));
            }

            var nuGetProject = _solutionManager.Value.GetNuGetProject(projectUniqueName);

            // It's possible the project isn't a NuGet-compatible project at all.
            if (nuGetProject == null)
            {
                outputPathContext = null;
                return false;
            }

            // invoke async operation from within synchronous method
            outputPathContext = NuGetUIThreadHelper.JoinableTaskFactory.Run(
                () => CreatePathContextAsync(nuGetProject));

            return outputPathContext != null;
        }

        public async Task<IVsPathContext> CreatePathContextAsync(NuGetProject nuGetProject)
        {
            var context = await GetPathContextFromAssetsFileAsync(
                nuGetProject, CancellationToken.None);

            context = context ?? await GetPathContextFromPackagesConfigAsync(
                nuGetProject, CancellationToken.None);

            // Fallback to reading the path context from the solution's settings. Note that project level settings in
            // VS are not currently supported.
            context = context ?? GetSolutionPathContext();

            return context;
        }

        public IVsPathContext GetSolutionPathContext()
        {
            return new VsPathContext(NuGetPathContext.Create(_settings.Value));
        }

        private async Task<IVsPathContext> GetPathContextFromAssetsFileAsync(
            NuGetProject nuGetProject, CancellationToken token)
        {
            // It's possible that this project isn't a build integrated NuGet project at all. That is, this project may
            // be a packages.config project.
            var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;
            if (buildIntegratedProject == null)
            {
                return null;
            }

            // It's possible the lock file doesn't exist or it's an older format that doesn't have the package folders
            // property persisted.
            var lockFile = await _getLockFileOrNullAsync(buildIntegratedProject);

            if (lockFile == null ||
                lockFile.PackageFolders == null ||
                lockFile.PackageFolders.Count == 0)
            {
                return null;
            }

            // The user packages folder is always the first package folder. Subsequent package folders are always
            // fallback package folders.
            var packageFolders = lockFile
                .PackageFolders
                .Select(lockFileItem => lockFileItem.Path)
                .ToList();

            var userPackageFolder = packageFolders[0];
            var fallbackPackageFolders = packageFolders.Skip(1);

            if (lockFile.Libraries == null ||
                lockFile.Libraries.Count == 0)
            {
                return new VsPathContext(userPackageFolder, fallbackPackageFolders);
            }

            var fppr = new FallbackPackagePathResolver(userPackageFolder, fallbackPackageFolders);

            var trie = new PathLookupTrie<string>();

            foreach (var pid in lockFile
                .Libraries
                .Where(l => l.Type == LibraryType.Package)
                .Select(l => new PackageIdentity(l.Name, l.Version)))
            {
                var packageInstallPath = fppr.GetPackageDirectory(pid.Id, pid.Version);
                if (packageInstallPath != null)
                {
                    trie[packageInstallPath] = packageInstallPath;
                }
            }

            return new VsIndexedPathContext(
                userPackageFolder,
                fallbackPackageFolders,
                trie);
        }

        private async Task<IVsPathContext> GetPathContextFromPackagesConfigAsync(
            NuGetProject nuGetProject, CancellationToken token)
        {
            var msbuildNuGetProject = nuGetProject as MSBuildNuGetProject;
            if (msbuildNuGetProject == null)
            {
                return null;
            }

            var trie = new PathLookupTrie<string>();

            var packageReferences = await msbuildNuGetProject.GetInstalledPackagesAsync(token);
            foreach (var pr in packageReferences)
            {
                var packageInstallPath = msbuildNuGetProject.FolderNuGetProject.GetInstalledPath(
                    pr.PackageIdentity);
                if (packageInstallPath != null)
                {
                    trie[packageInstallPath] = packageInstallPath;
                }
            }

            var pathContext = GetSolutionPathContext();

            return new VsIndexedPathContext(
                pathContext.UserPackageFolder,
                pathContext.FallbackPackageFolders.Cast<string>(),
                trie);
        }
    }
}
