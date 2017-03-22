// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
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
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VsPathContextProvider : IVsPathContextProvider
    {
        private readonly Lazy<ISettings> _settings;
        private readonly Lazy<IVsSolutionManager> _solutionManager;
        private readonly Lazy<Common.ILogger> _logger;
        private readonly Func<BuildIntegratedNuGetProject, Task<LockFile>> _getLockFileOrNullAsync;

        private readonly AsyncLazy<EnvDTE.DTE> _dte;
        private readonly Lazy<INuGetProjectContext> _projectContext = new Lazy<INuGetProjectContext>(() => new VSAPIProjectContext());

        [ImportingConstructor]
        public VsPathContextProvider(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            Lazy<ISettings> settings,
            Lazy<IVsSolutionManager> solutionManager,
            [Import("VisualStudioActivityLogger")]
            Lazy<Common.ILogger> logger)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _settings = settings;
            _solutionManager = solutionManager;
            _logger = logger;
            _getLockFileOrNullAsync = BuildIntegratedProjectUtility.GetLockFileOrNull;

            _dte = new AsyncLazy<EnvDTE.DTE>(
                async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return serviceProvider.GetDTE();
                },
                NuGetUIThreadHelper.JoinableTaskFactory);
        }

        /// <summary>
        /// This constructor is just used for testing.
        /// </summary>
        public VsPathContextProvider(
            ISettings settings,
            IVsSolutionManager solutionManager,
            Common.ILogger logger,
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

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _settings = new Lazy<ISettings>(() => settings);
            _solutionManager = new Lazy<IVsSolutionManager>(() => solutionManager);
            _logger = new Lazy<Common.ILogger>(() => logger);
            _getLockFileOrNullAsync = getLockFileOrNullAsync ?? BuildIntegratedProjectUtility.GetLockFileOrNull;
        }

        public bool TryCreateContext(string projectUniqueName, out IVsPathContext outputPathContext)
        {
            if (projectUniqueName == null)
            {
                throw new ArgumentNullException(nameof(projectUniqueName));
            }

            // invoke async operation from within synchronous method
            outputPathContext = NuGetUIThreadHelper.JoinableTaskFactory.Run(
                async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var dte = await _dte.GetValueAsync();

                    EnvDTE.Project dteProject = null;
                    var lookup = await EnvDTESolutionUtility.GetPathToDTEProjectLookupAsync(dte);

                    if (lookup.ContainsKey(projectUniqueName))
                    {
                        dteProject = lookup[projectUniqueName];
                    }

                    if (dteProject == null)
                    {
                        return null;
                    }

                    var nuGetProject = await _solutionManager.Value.GetOrCreateProjectAsync(dteProject, _projectContext.Value);

                    // It's possible the project isn't a NuGet-compatible project at all.
                    if (nuGetProject == null)
                    {
                        return null;
                    }

                    await TaskScheduler.Default;

                    return await CreatePathContextAsync(nuGetProject, CancellationToken.None);
                });

            return outputPathContext != null;
        }

        public async Task<IVsPathContext> CreatePathContextAsync(NuGetProject nuGetProject, CancellationToken token)
        {
            IVsPathContext context;

            try
            {
                context = await GetPathContextFromAssetsFileAsync(
                    nuGetProject, token);

                context = context ?? await GetPathContextFromPackagesConfigAsync(
                    nuGetProject, token);

                // Fallback to reading the path context from the solution's settings. Note that project level settings in
                // VS are not currently supported.
                context = context ?? GetSolutionPathContext();
            }
            catch (Exception e) when (e is KeyNotFoundException || e is InvalidOperationException)
            {
                var projectUniqueName = NuGetProject.GetUniqueNameOrName(nuGetProject);
                _logger.Value.LogError($"Failed creating a path context for \"{projectUniqueName}\". Reason: {e.Message}.");
                throw new InvalidOperationException(projectUniqueName, e);
            }

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

            var lockFile = await _getLockFileOrNullAsync(buildIntegratedProject);

            if ((lockFile?.PackageFolders?.Count ?? 0) == 0)
            {
                throw new InvalidOperationException("The lock file doesn't exist or it's an older format that doesn't have the package folders property persisted.");
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
                if (string.IsNullOrEmpty(packageInstallPath))
                {
                    throw new KeyNotFoundException($"Package directory for \"{pid}\" is not found");
                }

                trie[packageInstallPath] = packageInstallPath;
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
            foreach (var pid in packageReferences.Select(pr => pr.PackageIdentity))
            {
                var packageInstallPath = msbuildNuGetProject.FolderNuGetProject.GetInstalledPath(pid);
                if (string.IsNullOrEmpty(packageInstallPath))
                {
                    throw new KeyNotFoundException($"Package directory for \"{pid}\" is not found");
                }

                trie[packageInstallPath] = packageInstallPath;
            }

            var pathContext = GetSolutionPathContext();

            return new VsIndexedPathContext(
                pathContext.UserPackageFolder,
                pathContext.FallbackPackageFolders.Cast<string>(),
                trie);
        }
    }
}
