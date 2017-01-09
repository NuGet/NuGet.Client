﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Common;

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
            string packagePath,
            string source,
            string apiKey,
            string symbolSource,
            string symbolApiKey,
            int timeoutSeconds,
            bool disableBuffering,
            bool noSymbols,
            ILogger logger)
        {
            source = CommandRunnerUtility.ResolveSource(sourceProvider, source);
            symbolSource = CommandRunnerUtility.ResolveSymbolSource(sourceProvider, symbolSource);

            if (timeoutSeconds == 0)
            {
                timeoutSeconds = 5 * 60;
            }

            var packageUpdateResource = await CommandRunnerUtility.GetPackageUpdateResource(sourceProvider, source);

            // only push to SymbolSource when the actual package is being pushed to the official NuGet.org
            var sourceUri = packageUpdateResource.SourceUri;
            if (string.IsNullOrEmpty(symbolSource)
                && !noSymbols
                && !sourceUri.IsFile
                && sourceUri.IsAbsoluteUri)
            {
                if (sourceUri.Host.Equals(NuGetConstants.NuGetHostName, StringComparison.OrdinalIgnoreCase) // e.g. nuget.org
                    || sourceUri.Host.EndsWith("." + NuGetConstants.NuGetHostName, StringComparison.OrdinalIgnoreCase)) // *.nuget.org, e.g. www.nuget.org
                {
                    symbolSource = NuGetConstants.DefaultSymbolServerUrl;

                    if (string.IsNullOrEmpty(symbolApiKey))
                    {
                        // Use the nuget.org API key if it was given
                        symbolApiKey = apiKey;
                    }
                }
            }

            await packageUpdateResource.Push(
                packagePath,
                symbolSource,
                timeoutSeconds,
                disableBuffering,
                endpoint => apiKey ?? CommandRunnerUtility.GetApiKey(settings, endpoint, source, defaultApiKey: null, isSymbolApiKey: false),
                symbolsEndpoint => symbolApiKey ?? CommandRunnerUtility.GetApiKey(settings, symbolsEndpoint, symbolSource, apiKey, isSymbolApiKey: true),
                logger);
        }
    }
}
