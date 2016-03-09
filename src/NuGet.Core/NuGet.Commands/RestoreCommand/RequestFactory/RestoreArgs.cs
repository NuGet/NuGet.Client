// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
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

        public HashSet<string> FallbackRuntimes { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        public List<string> Inputs { get; set; } = new List<string>();

        public SourceCacheContext CacheContext { get; set; }

        public ILogger Log { get; set; }

        public List<string> Sources { get; set; } = new List<string>();

        public List<string> FallbackSources { get; set; } = new List<string>();

        public CachingSourceProvider CachingSourceProvider { get; set; }

        public List<IRestoreRequestProvider> RequestProviders { get; set; } = new List<IRestoreRequestProvider>();

        public PackageSaveMode PackageSaveMode { get; set; } = PackageSaveMode.Defaultv3;

        public int? LockFileVersion { get; set; }

        // Cache directory -> ISettings
        private ConcurrentDictionary<string, ISettings> _settingsCache
            = new ConcurrentDictionary<string, ISettings>(StringComparer.Ordinal);

        // ISettings.Root -> SourceRepositories
        private ConcurrentDictionary<string, List<SourceRepository>> _sourcesCache
            = new ConcurrentDictionary<string, List<SourceRepository>>(StringComparer.Ordinal);

        public ISettings GetSettings(string projectDirectory)
        {
            return _settingsCache.GetOrAdd(projectDirectory, (dir) =>
            {
                return Settings.LoadDefaultSettings(dir,
                    ConfigFileName,
                    MachineWideSettings);
            });
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
        public List<SourceRepository> GetEffectiveSources(ISettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return _sourcesCache.GetOrAdd(settings.Root, (root) => GetEffectiveSourcesCore(settings));
        }

        private List<SourceRepository> GetEffectiveSourcesCore(ISettings settings)
        {
            // Take the passed in sources
            var packageSources = new HashSet<string>(Sources, StringComparer.Ordinal);

            var packageSourceProvider = new Lazy<PackageSourceProvider>(() 
                => new PackageSourceProvider(settings));

            // If no sources were passed in use the NuGet.Config sources
            if (packageSources.Count < 1)
            {
                // Add enabled sources
                var enabledSources = packageSourceProvider.Value
                        .LoadPackageSources()
                        .Where(source => source.IsEnabled)
                        .Select(source => source.Source)
                        .Distinct(StringComparer.Ordinal)
                        .ToList();

                packageSources.UnionWith(enabledSources);
            }

            // Always add fallback sources
            packageSources.UnionWith(FallbackSources);

            if (CachingSourceProvider == null)
            {
                // Create a shared caching provider if one does not exist already
                CachingSourceProvider = new CachingSourceProvider(packageSourceProvider.Value);
            }

            return packageSources.Select(source => CachingSourceProvider.CreateRepository(source))
                .ToList();
        }

        public void ApplyStandardProperties(RestoreRequest request)
        {
            request.PackageSaveMode = PackageSaveMode;

            var lockFilePath = ProjectJsonPathUtilities.GetLockFilePath(request.Project.FilePath);
            request.LockFilePath = lockFilePath;

            request.MaxDegreeOfConcurrency =
                DisableParallel ? 1 : RestoreRequest.DefaultDegreeOfConcurrency;

            request.RequestedRuntimes.UnionWith(Runtimes);
            request.FallbackRuntimes.UnionWith(FallbackRuntimes);

            if (LockFileVersion.HasValue && LockFileVersion.Value > 0)
            {
                request.LockFileVersion = LockFileVersion.Value;
            }
        }
    }
}
