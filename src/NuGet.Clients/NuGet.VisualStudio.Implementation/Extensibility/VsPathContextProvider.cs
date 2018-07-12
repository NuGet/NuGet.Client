// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.LibraryModel;
using NuGet.PackageManagement;
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
    [Export(typeof(IVsPathContextProvider2))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VsPathContextProvider : IVsPathContextProvider2
    {
        private const string ProjectAssetsFile = "ProjectAssetsFile";
        private readonly Lazy<ISettings> _settings;
        private readonly Lazy<IVsSolutionManager> _solutionManager;
        private readonly Lazy<NuGet.Common.ILogger> _logger;
        private readonly Lazy<IVsProjectAdapterProvider> _vsProjectAdapterProvider;
        private readonly Func<string, LockFile> _getLockFileOrNull;

        private readonly AsyncLazy<EnvDTE.DTE> _dte;
        private readonly Lazy<INuGetProjectContext> _projectContext = new Lazy<INuGetProjectContext>(() => new VSAPIProjectContext());

        [ImportingConstructor]
        public VsPathContextProvider(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            Lazy<ISettings> settings,
            Lazy<IVsSolutionManager> solutionManager,
            [Import("VisualStudioActivityLogger")]
            Lazy<NuGet.Common.ILogger> logger,
            Lazy<IVsProjectAdapterProvider> vsProjectAdapterProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _vsProjectAdapterProvider = vsProjectAdapterProvider ?? throw new ArgumentNullException(nameof(vsProjectAdapterProvider));
            _getLockFileOrNull = BuildIntegratedProjectUtility.GetLockFileOrNull;

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
            IVsProjectAdapterProvider vsProjectAdapterProvider,
            Func<string, LockFile> getLockFileOrNull)
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

            if (vsProjectAdapterProvider == null)
            {
                throw new ArgumentNullException(nameof(vsProjectAdapterProvider));
            }

            _settings = new Lazy<ISettings>(() => settings);
            _solutionManager = new Lazy<IVsSolutionManager>(() => solutionManager);
            _logger = new Lazy<NuGet.Common.ILogger>(() => logger);
            _vsProjectAdapterProvider = new Lazy<IVsProjectAdapterProvider>(() => vsProjectAdapterProvider);
            _getLockFileOrNull = getLockFileOrNull ?? BuildIntegratedProjectUtility.GetLockFileOrNull;
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

                    return await CreatePathContextAsync(dteProject, projectUniqueName, CancellationToken.None);
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

        public async Task<IVsPathContext> CreatePathContextAsync(
            EnvDTE.Project dteProject,
            string projectUniqueName,
            CancellationToken token)
        {
            IVsPathContext context = null;

            try
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // get the VSProjectAdapter instance which will be used to retrieve MSBuild properties
                var projectAdapter = await _vsProjectAdapterProvider.Value.CreateAdapterForFullyLoadedProjectAsync(dteProject);

                // First check for project.assets.json file and generate VsPathContext from there.
                context = await GetPathContextForPackageRefAsync(projectAdapter, CancellationToken.None);

                // if no project.assets.json file, then check for project.lock.json file.
                context = context ?? await GetPathContextForProjectJsonAsync(projectAdapter, CancellationToken.None);

                // if no project.lock.json file, then look for packages.config file.
                context = context ?? await GetPathContextForPackagesConfigAsync(projectAdapter, CancellationToken.None);

                // Fallback to reading the path context from the solution's settings. Note that project level settings in
                // VS are not currently supported.
                context = context ?? GetSolutionPathContext();
            }
            catch (Exception e) when (e is KeyNotFoundException || e is InvalidOperationException)
            {
                var errorMessage = string.Format(CultureInfo.CurrentCulture, VsResources.PathContext_CreateContextError, projectUniqueName, e.Message);
                _logger.Value.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage, e);
            }

            return context;
        }

        private async Task<IVsPathContext> GetPathContextForPackageRefAsync(
            IVsProjectAdapter vsProjectAdapter, CancellationToken token)
        {
            // read ProjectAssetsFile property to get assets file full path
            var projectAssetsFile = await vsProjectAdapter.BuildProperties.GetPropertyValueAsync(ProjectAssetsFile);

            if (!string.IsNullOrEmpty(projectAssetsFile))
            {
                return await GetPathContextFromProjectLockFileAsync(projectAssetsFile, CancellationToken.None);
            }

            return null;
        }

        private async Task<IVsPathContext> GetPathContextForProjectJsonAsync(
            IVsProjectAdapter vsProjectAdapter, CancellationToken token)
        {
            // generate project.lock.json file path from project file
            var projectFilePath = vsProjectAdapter.FullProjectPath;

            if (!string.IsNullOrEmpty(projectFilePath))
            {
                var msbuildProjectFile = new FileInfo(projectFilePath);
                var projectNameFromMSBuildPath = Path.GetFileNameWithoutExtension(msbuildProjectFile.Name);

                string projectJsonPath = null;
                if (string.IsNullOrEmpty(projectNameFromMSBuildPath))
                {
                    projectJsonPath = Path.Combine(msbuildProjectFile.DirectoryName,
                        Common.ProjectJsonPathUtilities.ProjectConfigFileName);
                }
                else
                {
                    projectJsonPath = Common.ProjectJsonPathUtilities.GetProjectConfigPath(
                        msbuildProjectFile.DirectoryName,
                        projectNameFromMSBuildPath);
                }

                if (File.Exists(projectJsonPath))
                {
                    var lockFilePath = Common.ProjectJsonPathUtilities.GetLockFilePath(projectJsonPath);
                    return await GetPathContextFromProjectLockFileAsync(lockFilePath, CancellationToken.None);
                }
            }

            return null;
        }

        private async Task<IVsPathContext> GetPathContextFromProjectLockFileAsync(
            string lockFilePath, CancellationToken token)
        {
            var lockFile = _getLockFileOrNull(lockFilePath);
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

        private async Task<IVsPathContext> GetPathContextForPackagesConfigAsync(
            IVsProjectAdapter vsProjectAdapter, CancellationToken token)
        {
            var props = new Dictionary<string, object>();
            props.Add(NuGetProjectMetadataKeys.Name, Path.GetFileNameWithoutExtension(vsProjectAdapter.FullProjectPath));
            props.Add(NuGetProjectMetadataKeys.TargetFramework, await vsProjectAdapter.GetTargetFrameworkAsync());

            var packagesProject = new PackagesConfigNuGetProject(vsProjectAdapter.ProjectDirectory, props);

            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(_solutionManager.Value, _settings.Value);
            var folderProject = new FolderNuGetProject(packagesFolderPath);

            var packageReferences = await packagesProject.GetInstalledPackagesAsync(token);

            // switch to a background thread to process packages data
            await TaskScheduler.Default;

            var trie = new PathLookupTrie<string>();

            foreach (var pid in packageReferences.Select(pr => pr.PackageIdentity))
            {
                var packageInstallPath = folderProject.GetInstalledPath(pid);
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

        public IVsPathContext GetSolutionPathContext()
        {
            return new VsPathContext(NuGetPathContext.Create(_settings.Value));
        }
    }
}
