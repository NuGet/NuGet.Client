// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    /// <summary>
    /// Shared code to run the "push" command from the command line projects
    /// </summary>
    public static class PushRunner
    {
        public static async Task Run(
            ISettings settings,
            IPackageSourceProvider sourceProvider,
            IList<string> packagePaths,
            string source,
            string apiKey,
            string symbolSource,
            string symbolApiKey,
            int timeoutSeconds,
            bool disableBuffering,
            bool noSymbols,
            bool noServiceEndpoint,
            bool skipDuplicate,
            ILogger logger)
        {
            source = CommandRunnerUtility.ResolveSource(sourceProvider, source);
            symbolSource = CommandRunnerUtility.ResolveSymbolSource(sourceProvider, symbolSource);

            if (timeoutSeconds == 0)
            {
                timeoutSeconds = 5 * 60;
            }
            PackageSource packageSource = CommandRunnerUtility.GetOrCreatePackageSource(sourceProvider, source);
            var packageUpdateResource = await CommandRunnerUtility.GetPackageUpdateResource(sourceProvider, packageSource);

            if (packageSource.IsHttp && !packageSource.IsHttps)
            {
                logger.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Push_Warning_HTTPSourceUsage, packageSource.Source));
            }

            var packageUpdateResourceScheme = packageUpdateResource.SourceUri.Scheme;
            if (packageUpdateResourceScheme.Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Push_Warning_HTTPSourceUsage, source));
            }

            packageUpdateResource.Settings = settings;
            SymbolPackageUpdateResourceV3 symbolPackageUpdateResource = null;

            // figure out from index.json if pushing snupkg is supported
            var sourceUri = packageUpdateResource.SourceUri;
            if (string.IsNullOrEmpty(symbolSource)
                && !noSymbols
                && !sourceUri.IsFile
                && sourceUri.IsAbsoluteUri)
            {
                symbolPackageUpdateResource = await CommandRunnerUtility.GetSymbolPackageUpdateResource(sourceProvider, source);
                if (symbolPackageUpdateResource != null)
                {
                    symbolSource = symbolPackageUpdateResource.SourceUri.AbsoluteUri;
                    symbolApiKey = apiKey;

                    var symbolsUpdateResourceScheme = symbolPackageUpdateResource.SourceUri.Scheme;
                    if (symbolsUpdateResourceScheme.Equals("http", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Push_Warning_HTTPSourceUsage, source));
                    }
                }
            }

            await packageUpdateResource.Push(
                packagePaths,
                symbolSource,
                timeoutSeconds,
                disableBuffering,
                endpoint => apiKey ?? CommandRunnerUtility.GetApiKey(settings, endpoint, source, defaultApiKey: null, isSymbolApiKey: false),
                symbolsEndpoint => symbolApiKey ?? CommandRunnerUtility.GetApiKey(settings, symbolsEndpoint, symbolSource, apiKey, isSymbolApiKey: true),
                noServiceEndpoint,
                skipDuplicate,
                symbolPackageUpdateResource,
                logger);
        }

        [Obsolete("Use Run method which takes multiple package paths.")]
        public static Task Run(
            ISettings settings,
            IPackageSourceProvider sourceProvider,
            string packagePath,
            string source,
            string apiKey,
            string symbolSource,
            string symbolApiKey,
            int timeoutSeconds,
            bool disableBuffering,
            bool noSymbols,
            bool noServiceEndpoint,
            bool skipDuplicate,
            ILogger logger)
        {
            return Run(settings: settings,
                sourceProvider: sourceProvider,
                packagePaths: new[] { packagePath },
                source: source,
                apiKey: apiKey,
                symbolSource: symbolSource,
                symbolApiKey: symbolApiKey,
                timeoutSeconds: timeoutSeconds,
                disableBuffering: disableBuffering,
                noSymbols: noSymbols,
                noServiceEndpoint: noServiceEndpoint,
                skipDuplicate: skipDuplicate,
                logger: logger);
        }
    }
}
