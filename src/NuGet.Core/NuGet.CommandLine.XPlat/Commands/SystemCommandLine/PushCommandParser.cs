// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Credentials;

namespace NuGet.CommandLine.XPlat.Commands
{
    internal static partial class PushCommandParser
    {
        internal static async Task PushHandlerAsync(
            bool forceEnglishOutput,
            string source,
            string symbolSource,
            int timeout,
            string apiKey,
            string symbolApiKey,
            bool disableBuffering,
            bool noSymbols,
            string[] packagePaths,
            bool noServiceEndpoint,
            bool interactive,
            bool skipDuplicate)
        {
            IList<string> packagePathsValue = new List<string>(packagePaths);

#pragma warning disable CS0618 // Type or member is obsolete
            var sourceProvider = new PackageSourceProvider(XPlatUtility.GetSettingsForCurrentWorkingDirectory(), enablePackageSourcesChangedEvent: false);
#pragma warning restore CS0618 // Type or member is obsolete

            try
            {
                DefaultCredentialServiceUtility.SetupDefaultCredentialService(GetLoggerFunction(), !interactive);
                await PushRunner.Run(
                    sourceProvider.Settings,
                    sourceProvider,
                    packagePathsValue,
                    source,
                    apiKey,
                    symbolSource,
                    symbolApiKey,
                    timeout,
                    disableBuffering,
                    noSymbols,
                    noServiceEndpoint,
                    skipDuplicate,
                    GetLoggerFunction());
            }
            catch (TaskCanceledException ex)
            {
                throw new AggregateException(ex, new Exception(Strings.Push_Timeout_Error));
            }
        }
    }
}
