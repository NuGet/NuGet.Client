// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    internal class PackageLoadContext
    {
        private readonly Task<PackageCollection> _installedPackagesTask;

        public IEnumerable<SourceRepository> SourceRepositories { get; private set; }

        public NuGetPackageManager PackageManager { get; private set; }

        public IProjectContextInfo[] Projects { get; private set; }

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
            Projects = (uiContext.Projects ?? Enumerable.Empty<IProjectContextInfo>()).ToArray();
            PackageManagerProviders = uiContext.PackageManagerProviders;
            SolutionManager = uiContext.SolutionManager;

            _installedPackagesTask = PackageCollection.FromProjectsAsync(Projects, CancellationToken.None);
        }

        public Task<PackageCollection> GetInstalledPackagesAsync() =>_installedPackagesTask;

        // Returns the list of frameworks that we need to pass to the server during search
        public async Task<IEnumerable<string>> GetSupportedFrameworksAsync()
        {
            var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in Projects)
            {
                (bool targetFrameworkSuccess, NuGetFramework framework) = await project.TryGetMetadataAsync<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework, CancellationToken.None);
                if (targetFrameworkSuccess)
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
                    (bool supportedFrameworksSuccess, IEnumerable<NuGetFramework> supportedFrameworksValue) = await project.TryGetMetadataAsync<IEnumerable<NuGetFramework>>(NuGetProjectMetadataKeys.SupportedFrameworks, CancellationToken.None);
                    if (supportedFrameworksSuccess)
                    {
                        var supportedFrameworks = supportedFrameworksValue;
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
