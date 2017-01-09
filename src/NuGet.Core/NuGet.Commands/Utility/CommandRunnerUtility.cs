﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    /// <summary>
    /// Helper functions for shared command runners (push, delete, etc)
    /// </summary>
    internal static class CommandRunnerUtility
    {
        public static string ResolveSource(IPackageSourceProvider sourceProvider, string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                source = sourceProvider.DefaultPushSource;
            }

            if (!string.IsNullOrEmpty(source))
            {
                source = sourceProvider.ResolveAndValidateSource(source);
            }

            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentException(Strings.Error_MissingSourceParameter);
            }

            return source;
        }

        public static string ResolveSymbolSource(IPackageSourceProvider sourceProvider, string symbolSource)
        {
            if (!string.IsNullOrEmpty(symbolSource))
            {
                symbolSource = sourceProvider.ResolveAndValidateSource(symbolSource);
            }

            return symbolSource;
        }

        public static string GetApiKey(ISettings settings, string endpoint, string source, string defaultApiKey, bool isSymbolApiKey)
        {
            // try searching API key by endpoint first
            // needed to support config key mappings like 'https://www.nuget.org/api/v2/package'
            var apiKey = SettingsUtility.GetDecryptedValue(settings, ConfigurationConstants.ApiKeys, endpoint);

            // if not found try finding it by source url
            apiKey = apiKey ?? SettingsUtility.GetDecryptedValue(settings, ConfigurationConstants.ApiKeys, source);

            // fallback for a case of nuget.org source
            // try to retrieve an api key mapped to a default "gallery" url
            if (apiKey == null
                && source.IndexOf(NuGetConstants.NuGetHostName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var defaultConfigKey = isSymbolApiKey ? NuGetConstants.DefaultSymbolServerUrl : NuGetConstants.DefaultGalleryServerUrl;
                apiKey = SettingsUtility.GetDecryptedValue(settings, ConfigurationConstants.ApiKeys, defaultConfigKey);
            }

            // return an API key when found or the default one
            return apiKey ?? defaultApiKey;
        }

        public static async Task<PackageUpdateResource> GetPackageUpdateResource(IPackageSourceProvider sourceProvider, string source)
        {
            // Use a loaded PackageSource if possible since it contains credential info
            PackageSource packageSource = null;
            foreach (var loadedPackageSource in sourceProvider.LoadPackageSources())
            {
                if (loadedPackageSource.IsEnabled && source == loadedPackageSource.Source)
                {
                    packageSource = loadedPackageSource;
                    break;
                }
            }

            if (packageSource == null)
            {
                packageSource = new PackageSource(source);
            }

            var sourceRepositoryProvider = new CachingSourceProvider(sourceProvider);
            var sourceRepository = sourceRepositoryProvider.CreateRepository(packageSource);

            return await sourceRepository.GetResourceAsync<PackageUpdateResource>();
        }
    }
}
