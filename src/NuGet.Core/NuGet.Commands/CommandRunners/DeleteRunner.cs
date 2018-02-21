// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    /// <summary>
    /// Shared code to run the "delete" command from the command line projects
    /// </summary>
    public static class DeleteRunner
    {
        public static async Task Run(
            ISettings settings,
            IPackageSourceProvider sourceProvider,
            string packageId,
            string packageVersion,
            string source,
            string apiKey,
            bool nonInteractive,
            bool noServiceEndpoint,
            Func<string, bool> confirmFunc,
            ILogger logger)
        {
            source = CommandRunnerUtility.ResolveSource(sourceProvider, source);

            var packageUpdateResource = await CommandRunnerUtility.GetPackageUpdateResource(sourceProvider, source);

            await packageUpdateResource.Delete(
                packageId,
                packageVersion,
                endpoint => apiKey ?? CommandRunnerUtility.GetApiKey(settings, endpoint, source, defaultApiKey: null, isSymbolApiKey: false),
                desc => nonInteractive || confirmFunc(desc),
                noServiceEndpoint,
                logger);
        }
    }
}
