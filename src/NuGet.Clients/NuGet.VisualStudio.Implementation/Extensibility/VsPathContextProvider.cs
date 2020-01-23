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
using NuGet.Common;
using NuGet.Configuration;
using NuGet.LibraryModel;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.VisualStudio.Implementation.Resources;

namespace NuGet.VisualStudio
{
    // Implementation of IVsPathContextProvider as a MEF-exported component.
    [Export(typeof(IVsPathContextProvider))]
    [Export(typeof(IVsPathContextProvider2))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VsPathContextProvider : IVsPathContextProvider2
    {
        private readonly IAsyncServiceProvider _asyncServiceprovider;
        private readonly Lazy<ISettings> _settings;
        private readonly Lazy<IVsSolutionManager> _solutionManager;
        private readonly Lazy<ILogger> _logger;
        private readonly Func<BuildIntegratedNuGetProject, Task<LockFile>> _getLockFileOrNullAsync;

        private readonly Lazy<INuGetProjectContext> _projectContext;
        

        [ImportingConstructor]
        public VsPathContextProvider(
            Lazy<ISettings> settings,
            Lazy<IVsSolutionManager> solutionManager,
            [Import("VisualStudioActivityLogger")]
            Lazy<ILogger> logger)
            : this(AsyncServiceProvider.GlobalProvider,
                  settings,
                  solutionManager,
                  logger)
        { }

        public VsPathContextProvider(
            IAsyncServiceProvider asyncServiceProvider,
            Lazy<ISettings> settings,
            Lazy<IVsSolutionManager> solutionManager,
            Lazy<ILogger> logger)
        {
            _asyncServiceprovider = asyncServiceProvider ?? throw new ArgumentNullException(nameof(asyncServiceProvider));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _getLockFileOrNullAsync = BuildIntegratedProjectUtility.GetLockFileOrNull;

            _projectContext = new Lazy<INuGetProjectContext>(() => new VSAPIProjectContext
            {
                PackageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Defaultv2,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        ClientPolicyContext.GetClientPolicy(_settings.Value, NullLogger.Instance),
                        NullLogger.Instance)
            });
        }

        /// <summary>
        /// This constructor is just used for testing.
        /// </summary>
        public VsPathContextProvider(
            ISettings settings,
            IVsSolutionManager solutionManager,
            ILogger logger,
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
            _logger = new Lazy<ILogger>(() => logger);
            _getLockFileOrNullAsync = getLockFileOrNullAsync ?? BuildIntegratedProjectUtility.GetLockFileOrNull;

            _projectContext = new Lazy<INuGetProjectContext>(() => {
                return new VSAPIProjectContext
                {
                    PackageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Defaultv2,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        ClientPolicyContext.GetClientPolicy(_settings.Value, NullLogger.Instance),
                        NullLogger.Instance)
                };
            });
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
                    var nuGetProject = await CreateNuGetProjectAsync(projectUniqueName);

                    // It's possible the project isn't a NuGet-compatible project at all.
                    if (nuGetProject == null)
                    {
                        return null;
                    }

                    return await CreatePathContextAsync(nuGetProject, CancellationToken.None);
                });

            return outputPathContext != null;
        }

        public bool TryCreateSolutionContext(out IVsPathContext2 outputPathContext)
        {
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(_solutionManager.Value, _settings.Value);

            outputPathContext = new VsPathContext(NuGetPathContext.Create(_settings.Value), packagesFolderPath);

            return outputPathContext != null;
        }

        public bool TryCreateSolutionContext(string solutionDirectory, out IVsPathContext2 outputPathContext)
        {
            if (solutionDirectory == null)
            {
                throw new ArgumentNullException(nameof(solutionDirectory));
            }

            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(solutionDirectory, _settings.Value);

            outputPathContext = new VsPathContext(NuGetPathContext.Create(_settings.Value), packagesFolderPath);

            return outputPathContext != null;
        }

        private async Task<NuGetProject> CreateNuGetProjectAsync(string projectUniqueName)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await _asyncServiceprovider.GetDTEAsync();
            var supportedProjects = dte.Solution.Projects.Cast<EnvDTE.Project>();

            foreach (var solutionProject in supportedProjects)
            {
                var solutionProjectPath = EnvDTEProjectInfoUtility.GetFullProjectPath(solutionProject);

                if (!string.IsNullOrEmpty(solutionProjectPath) &&
                    PathUtility.GetStringComparerBasedOnOS().Equals(solutionProjectPath, projectUniqueName))
                {
                    return await _solutionManager.Value.GetOrCreateProjectAsync(solutionProject, _projectContext.Value);
                }
            }

            return null;
        }

        public async Task<IVsPathContext> CreatePathContextAsync(NuGetProject nuGetProject, CancellationToken token)
        {
            IVsPathContext context;

            try
            {
                var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;

                if (buildIntegratedProject != null)
                {
                    // if project is build integrated, then read it from assets file.
                    context = await GetPathContextFromAssetsFileAsync(
                        buildIntegratedProject, token);
                }
                else
                {
                    var msbuildNuGetProject = nuGetProject as MSBuildNuGetProject;
                    if (msbuildNuGetProject != null)
                    {
                        // when a msbuild project, then read it from packages.config file.
                        context = await GetPathContextFromPackagesConfigAsync(
                            msbuildNuGetProject, token);
                    }
                    else
                    {
                        // Fallback to reading the path context from the solution's settings. Note that project level settings in
                        // VS are not currently supported.
                        context = GetSolutionPathContext();
                    }
                }
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
            BuildIntegratedNuGetProject buildIntegratedProject, CancellationToken token)
        {
            var lockFile = await _getLockFileOrNullAsync(buildIntegratedProject);

            if ((lockFile?.PackageFolders?.Count ?? 0) == 0)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, VsResources.PathContext_LockFileError));
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
            MSBuildNuGetProject msbuildNuGetProject, CancellationToken token)
        {
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
