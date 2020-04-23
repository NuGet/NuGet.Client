// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    internal class PackageLoadContext
    {
        private readonly Task<PackageCollection> _installedPackagesTask;

        public IEnumerable<SourceRepository> SourceRepositories { get; private set; }

        public NuGetPackageManager PackageManager { get; private set; }

        public NuGetProject[] Projects { get; private set; }

        // Indicates whether the loader is created by solution package manager.
        public bool IsSolution { get; private set; }

        public IEnumerable<IVsPackageManagerProvider> PackageManagerProviders { get; private set; }

        public PackageSearchMetadataCache CachedPackages { get; set; }

        public IVsSolutionManager SolutionManager { get; private set; }

        public PackageLoadContext(
            IEnumerable<SourceRepository> sourceRepositories,
            bool isSolution,
            INuGetUIContext uiContext)
        {
            SourceRepositories = sourceRepositories;
            IsSolution = isSolution;
            PackageManager = uiContext.PackageManager;
            Projects = (uiContext.Projects ?? Enumerable.Empty<NuGetProject>()).ToArray();
            PackageManagerProviders = uiContext.PackageManagerProviders;
            SolutionManager = uiContext.SolutionManager;

            _installedPackagesTask = PackageCollection.FromProjectsAsync(Projects, CancellationToken.None);
        }

        public Task<PackageCollection> GetInstalledPackagesAsync() =>_installedPackagesTask;

        // get lock file for projects
        private async Task<List<LockFile>> GetLockFiles(IEnumerable<NuGetProject> nuGetProjects)
        {
            var nuGetProjectList = nuGetProjects.ToList();
            var buildIntegratedProjects = nuGetProjectList.OfType<BuildIntegratedNuGetProject>().ToList();

            List<LockFile> lockFiles = new List<LockFile>();
            var lockFileFormat = new LockFileFormat();

            foreach (var project in buildIntegratedProjects)
            {
                var lockFilePath = await project.GetAssetsFilePathAsync();
                if (File.Exists(lockFilePath))
                {
                    lockFiles = lockFiles.Append(lockFileFormat.Read(lockFilePath)).ToList();
                }
            }
            return lockFiles;
        }

        public async Task<Dictionary<string, VersionRange>> GetDependentPackagesAsync()
        {
            // get lock files from projects. This will add all dependency packages for all projects in the list
            List<LockFile> lockFiles = await GetLockFiles(Projects.ToList());
            Dictionary<string, VersionRange> dependentPackages = new Dictionary<string, VersionRange>();

            foreach (LockFile lockFile in lockFiles)
            {
                foreach (LockFileTarget target in lockFile.Targets)
                {
                    foreach (LockFileTargetLibrary lib in target.Libraries)
                    {
                        foreach (PackageDependency dep in lib.Dependencies)
                        {
                            dependentPackages[dep.Id] = dep.VersionRange;
                        }
                    }
                }
            }
            return dependentPackages;
        }

        public async Task<List<string>> GetTargetFrameworksAsync()
        { 
            var frameworks = new List<string>();
            var buildIntegratedProjects = Projects.OfType<BuildIntegratedNuGetProject>().ToList();
            foreach (var project in buildIntegratedProjects)
            {
                // get the target frameworks for PackagesConfig style projects
                NuGetFramework framework;
                if (project.TryGetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework, out framework)
                    && !frameworks.Contains(framework.ToString()))
                {
                    frameworks.Add(framework.ToString());
                }

                // get the target frameworks for PackageReference style projects
                var dgcContext = new DependencyGraphCacheContext();
                var packageSpecs = await project.GetPackageSpecsAsync(dgcContext);
                foreach (var packageSpec in packageSpecs)
                {
                    foreach (var targetFramework in packageSpec.TargetFrameworks)
                    {
                        if (!frameworks.Contains(targetFramework.FrameworkName.ToString()))
                        {
                            frameworks.Add(targetFramework.FrameworkName.ToString());
                        }
                    }
                }
            }
            return frameworks;
        }

        // Returns the list of frameworks that we need to pass to the server during search
        public IEnumerable<string> GetSupportedFrameworks()
        {
            var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in Projects)
            {
                NuGetFramework framework;
                if (project.TryGetMetadata(NuGetProjectMetadataKeys.TargetFramework,
                    out framework))
                {
                    if (framework != null
                        && framework.IsAny)
                    {
                        // One of the project's target framework is AnyFramework. In this case,
                        // we don't need to pass the framework filter to the server.
                        return Enumerable.Empty<string>();
                    }

                    if (framework != null
                        && framework.IsSpecificFramework)
                    {
                        frameworks.Add(framework.DotNetFrameworkName);
                    }
                }
                else
                {
                    // we also need to process SupportedFrameworks
                    IEnumerable<NuGetFramework> supportedFrameworks;
                    if (project.TryGetMetadata(
                        NuGetProjectMetadataKeys.SupportedFrameworks,
                        out supportedFrameworks))
                    {
                        foreach (var f in supportedFrameworks)
                        {
                            if (f.IsAny)
                            {
                                return Enumerable.Empty<string>();
                            }

                            frameworks.Add(f.DotNetFrameworkName);
                        }
                    }
                }
            }

            return frameworks;
        }
    }
}
