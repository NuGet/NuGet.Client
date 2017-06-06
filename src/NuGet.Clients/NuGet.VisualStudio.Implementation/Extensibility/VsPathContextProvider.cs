// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.VisualStudio.Implementation.Resources;

namespace NuGet.VisualStudio
{
    // Implementation of IVsPathContextProvider as a MEF-exported component.
    [Export(typeof(IVsPathContextProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VsPathContextProvider : IVsPathContextProvider
    {
        private readonly Lazy<ISettings> _settings;
        private readonly Lazy<IVsSolutionManager> _solutionManager;
        private readonly Lazy<NuGet.Common.ILogger> _logger;
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
            Lazy<NuGet.Common.ILogger> logger)
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
            NuGet.Common.ILogger logger,
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
            _logger = new Lazy<NuGet.Common.ILogger>(() => logger);
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
                    var dte = await _dte.GetValueAsync();
                    var lookup = await GetPathToDTEProjectLookupAsync(dte);

                    if (!lookup.TryGetValue(projectUniqueName, out var dteProject))
                    {
                        return null;
                    }

                    var nuGetProject = await _solutionManager.Value.GetOrCreateProjectAsync(dteProject, _projectContext.Value);

                    // It's possible the project isn't a NuGet-compatible project at all.
                    if (nuGetProject == null)
                    {
                        return null;
                    }

                    return await CreatePathContextAsync(nuGetProject, CancellationToken.None);
                });

            return outputPathContext != null;
        }

        private static async Task<Dictionary<string, EnvDTE.Project>> GetPathToDTEProjectLookupAsync(EnvDTE.DTE dte)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var pathToProject = new Dictionary<string, EnvDTE.Project>(StringComparer.OrdinalIgnoreCase);

            var supportedProjects = dte.Solution.Projects.Cast<EnvDTE.Project>();

            foreach (var solutionProject in supportedProjects)
            {
                var solutionProjectPath = EnvDTEProjectInfoUtility.GetFullProjectPath(solutionProject);

                if (!string.IsNullOrEmpty(solutionProjectPath) &&
                    !pathToProject.ContainsKey(solutionProjectPath))
                {
                    pathToProject.Add(solutionProjectPath, solutionProject);
                }
            }

            return pathToProject;
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
                var errorMessage = string.Format(CultureInfo.CurrentCulture, VsResources.PathContext_CreateContextError, projectUniqueName, e.Message);
                _logger.Value.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage, e);
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
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, VsResources.PathContext_LockFileError));
            }

            // switch to a background thread to process packages data
            await TaskScheduler.Default;

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
                    throw new KeyNotFoundException(string.Format(CultureInfo.CurrentCulture, VsResources.PathContext_PackageDirectoryNotFound, pid));
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

            var packageReferences = await msbuildNuGetProject.GetInstalledPackagesAsync(token);

            // switch to a background thread to process packages data
            await TaskScheduler.Default;

            var trie = new PathLookupTrie<string>();

            foreach (var pid in packageReferences.Select(pr => pr.PackageIdentity))
            {
                var packageInstallPath = msbuildNuGetProject.FolderNuGetProject.GetInstalledPath(pid);
                if (string.IsNullOrEmpty(packageInstallPath))
                {
                    throw new KeyNotFoundException(string.Format(CultureInfo.CurrentCulture, VsResources.PathContext_PackageDirectoryNotFound, pid));
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
