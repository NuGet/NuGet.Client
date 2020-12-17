// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using NuGet.Frameworks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    internal class PackageLoadContext
    {
        private readonly Task<PackageCollection> _installedPackagesTask;

        public PackageLoadContext(bool isSolution, INuGetUIContext uiContext)
        {
            IsSolution = isSolution;
            PackageManager = uiContext.PackageManager;
            Projects = (uiContext.Projects ?? Enumerable.Empty<IProjectContextInfo>()).ToArray();
            PackageManagerProviders = uiContext.PackageManagerProviders;
            SolutionManager = uiContext.SolutionManagerService;
            ServiceBroker = uiContext.ServiceBroker;

            _installedPackagesTask = PackageCollection.FromProjectsAsync(
                ServiceBroker,
                Projects,
                CancellationToken.None);
        }

        public NuGetPackageManager PackageManager { get; }

        public IProjectContextInfo[] Projects { get; }

        // Indicates whether the loader is created by solution package manager.
        public bool IsSolution { get; }

        public IEnumerable<IVsPackageManagerProvider> PackageManagerProviders { get; }

        public PackageSearchMetadataCache CachedPackages { get; set; }

        public INuGetSolutionManagerService SolutionManager { get; }

        internal IServiceBroker ServiceBroker { get; }

        public Task<PackageCollection> GetInstalledPackagesAsync() => _installedPackagesTask;

        // Returns the list of frameworks that we need to pass to the server during search
        public async Task<IEnumerable<string>> GetSupportedFrameworksAsync()
        {
            var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (IProjectContextInfo project in Projects)
            {
                IProjectMetadataContextInfo projectMetadata = await project.GetMetadataAsync(
                    ServiceBroker,
                    CancellationToken.None);
                NuGetFramework framework = projectMetadata.TargetFramework;

                if (framework != null)
                {
                    if (framework.IsAny)
                    {
                        // One of the project's target framework is AnyFramework. In this case,
                        // we don't need to pass the framework filter to the server.
                        return Enumerable.Empty<string>();
                    }

                    if (framework.IsSpecificFramework)
                    {
                        frameworks.Add(framework.DotNetFrameworkName);
                    }
                }
                else
                {
                    // we also need to process SupportedFrameworks
                    IReadOnlyCollection<NuGetFramework> supportedFrameworks = projectMetadata.SupportedFrameworks;

                    if (supportedFrameworks != null && supportedFrameworks.Count > 0)
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
