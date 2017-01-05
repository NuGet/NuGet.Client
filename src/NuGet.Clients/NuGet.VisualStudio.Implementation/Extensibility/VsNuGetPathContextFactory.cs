// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsNuGetPathContextFactory))]
    public class VsNuGetPathContextFactory : IVsNuGetPathContextFactory
    {
        private readonly ISettings _settings;
        private readonly IVsSolutionManager _solutionManager;

        [ImportingConstructor]
        public VsNuGetPathContextFactory(ISettings settings, IVsSolutionManager solutionManager)
        {
            _settings = settings;
            _solutionManager = solutionManager;
        }

        /// <summary>
        /// This property is used only for testing.
        /// </summary>
        public Func<BuildIntegratedNuGetProject, Task<LockFile>> GetLockFileOrNullAsync { get; set; }

        public async Task<IVsNuGetPathContext> CreateAsync(Project project, CancellationToken token)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            // Try to read the path context from the assets file.
            var outputPathContext = await GetPathContextFromAssetsFile(project);

            // Fallback to reading the path context from the solution's settings. Note that project level settings in
            // VS are not currently supported.
            if (outputPathContext == null)
            {
                var internalPathContext = NuGetPathContext.Create(_settings);

                outputPathContext = new VsNuGetPathContext(
                    internalPathContext.UserPackageFolder,
                    internalPathContext.FallbackPackageFolders);
            }

            return outputPathContext;
        }

        private async Task<IVsNuGetPathContext> GetPathContextFromAssetsFile(Project project)
        {
            var nuGetProject = await _solutionManager.GetOrCreateProjectAsync(
                project,
                new VSAPIProjectContext());

            // It's possible the project isn't a NuGet-compatible project at all.
            if (nuGetProject == null)
            {
                return null;
            }

            // It's possible that this project isn't a build integrated NuGet project at all. That is, this project may
            // be a packages.config project.
            var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;
            if (buildIntegratedProject == null)
            {
                return null;
            }

            // It's possible the lock file doesn't exist or it's an older format that doesn't have the package folders
            // property persisted.
            var getLockFileOrNullAsync = GetLockFileOrNullAsync ?? BuildIntegratedProjectUtility.GetLockFileOrNull;
            var lockFile = await getLockFileOrNullAsync(buildIntegratedProject);
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

            return new VsNuGetPathContext(userPackageFolder, fallbackPackageFolders);
        }
    }
}
