// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Commands
{
    public class RestoreArgs
    {
        public string ConfigFileName { get; set; }

        public IMachineWideSettings MachineWideSettings { get; set; }

        public string GlobalPackagesFolder { get; set; }

        public bool DisableParallel { get; set; }

        public HashSet<string> Runtimes { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        public HashSet<string> FallbackRuntimes { get; set; }

        public List<string> Inputs { get; set; } = new List<string>();

        public SourceCacheContext CacheContext { get; set; }

        public ILogger Log { get; set; }

        public List<string> Sources { get; set; } = new List<string>();

        public List<string> FallbackSources { get; set; } = new List<string>();

        public CachingSourceProvider CachingSourceProvider { get; set; }

        public List<IRestoreRequestProvider> RequestProviders { get; set; } = new List<IRestoreRequestProvider>();

        public PackageSaveMode PackageSaveMode { get; set; } = PackageSaveMode.Defaultv3;

        public ISettings GetSettings(string projectDirectory)
        {
            return Settings.LoadDefaultSettings(projectDirectory,
                ConfigFileName,
                MachineWideSettings);
        }

        public string GetEffectiveGlobalPackagesFolder(string rootDirectory, ISettings settings)
        {
            string globalPath = null;

            if (!string.IsNullOrEmpty(GlobalPackagesFolder))
            {
                globalPath = GlobalPackagesFolder;
            }
            else
            {
                globalPath = SettingsUtility.GetGlobalPackagesFolder(settings);
            }

            // Resolve relative paths
            return Path.GetFullPath(Path.Combine(rootDirectory, globalPath));
        }

        /// <summary>
        /// Uses either Sources or Settings, and then adds Fallback sources.
        /// </summary>
        public List<SourceRepository> GetEffectiveSources(
            ISettings settings)
        {
            var packageSourceProvider = new PackageSourceProvider(settings);

            // Take the passed in sources
            var packageSources = Sources.Select(s => new PackageSource(s));

            // If no sources were passed in use the NuGet.Config sources
            if (!packageSources.Any())
            {
                // Add enabled sources
                packageSources = packageSourceProvider.LoadPackageSources().Where(source => source.IsEnabled);
            }

            packageSources = packageSources.Concat(
                FallbackSources.Select(s => new PackageSource(s)));

            var cachingProvider = CachingSourceProvider ?? new CachingSourceProvider(packageSourceProvider);

            return packageSources.Select(source => cachingProvider.CreateRepository(source))
                .Distinct()
                .ToList();
        }

        public void ApplyStandardProperties(RestoreRequest request)
        {
            request.PackageSaveMode = PackageSaveMode;

            // Read the existing lock file, this is needed to support IsLocked=true
            var lockFilePath = ProjectJsonPathUtilities.GetLockFilePath(request.Project.FilePath);
            request.LockFilePath = lockFilePath;
            request.ExistingLockFile = LockFileUtilities.GetLockFile(lockFilePath, request.Log);

            request.MaxDegreeOfConcurrency =
                DisableParallel ? 1 : RestoreRequest.DefaultDegreeOfConcurrency;
        }
    }
}
