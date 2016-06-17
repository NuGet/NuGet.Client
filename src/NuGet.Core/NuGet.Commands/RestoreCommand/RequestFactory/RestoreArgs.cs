﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    public class RestoreArgs
    {
        public string ConfigFile { get; set; }

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
            if (string.IsNullOrEmpty(ConfigFile))
            {
                return _settingsCache.GetOrAdd(projectDirectory, (dir) =>
                {
                    return Settings.LoadDefaultSettings(dir,
                        configFileName : null,
                        machineWideSettings: MachineWideSettings);
                });
            }
            else
            {
                var configFileFullPath = Path.GetFullPath(ConfigFile);
                var directory = Path.GetDirectoryName(configFileFullPath);
                var configFileName = Path.GetFileName(configFileFullPath);

                return _settingsCache.GetOrAdd(directory, (dir) =>
                {
                    return Settings.LoadSpecificSettings(dir,
                        configFileName: configFileName);
                });
            }
        }

        public string GetEffectiveGlobalPackagesFolder(string rootDirectory, ISettings settings)
        {
            if (!string.IsNullOrEmpty(GlobalPackagesFolder))
            {
                // Resolve as relative to the CWD
                return Path.GetFullPath(GlobalPackagesFolder);
            }

            // Load from environment, nuget.config or default location, and resolve relative paths
            // to the project root.
            string globalPath = SettingsUtility.GetGlobalPackagesFolder(settings);
            return Path.GetFullPath(Path.Combine(rootDirectory, globalPath));
        }

        public IReadOnlyList<string> GetEffectiveFallbackPackageFolders(ISettings settings)
        {
            return SettingsUtility.GetFallbackPackageFolders(settings);
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
            var sourceObjects = new Dictionary<string, PackageSource>(StringComparer.Ordinal);
            var packageSourceProvider = new PackageSourceProvider(settings);
            var packageSourcesFromProvider = packageSourceProvider.LoadPackageSources();
            var useNugetConfigSources = (Sources.Count == 0);

            // Always use passed-in sources and fallback sources
            foreach (var sourceUri in Enumerable.Concat(Sources, FallbackSources))
            {
                sourceObjects[sourceUri] = new PackageSource(sourceUri);
            }

            // Use PackageSource objects from the provider when possible (since those will have credentials from nuget.config)
            foreach (var source in packageSourcesFromProvider)
            {
                if (source.IsEnabled && (useNugetConfigSources || sourceObjects.ContainsKey(source.Source)))
                {
                    sourceObjects[source.Source] = source;
                }
            }

            if (CachingSourceProvider == null)
            {
                // Create a shared caching provider if one does not exist already
                CachingSourceProvider = new CachingSourceProvider(packageSourceProvider);
            }

            return sourceObjects.Select(entry => CachingSourceProvider.CreateRepository(entry.Value)).ToList();
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
