// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "push", "PushCommandDescription;DefaultConfigDescription",
        MinArgs = 1, MaxArgs = 2, UsageDescriptionResourceName = "PushCommandUsageDescription",
        UsageSummaryResourceName = "PushCommandUsageSummary", UsageExampleResourceName = "PushCommandUsageExamples")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class PushCommand : Command
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        [Option(typeof(NuGetCommand), "PushCommandSourceDescription", AltName = "src")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Source { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "CommandApiKey")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string ApiKey { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PushCommandSymbolSourceDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string SymbolSource { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SymbolApiKey")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string SymbolApiKey { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PushCommandTimeoutDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public int Timeout { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PushCommandDisableBufferingDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool DisableBuffering { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PushCommandNoSymbolsDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool NoSymbols { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "CommandNoServiceEndpointDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool NoServiceEndpoint { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PushCommandSkipDuplicateDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool SkipDuplicate { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override async Task ExecuteCommandAsync()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            string packagePath = Arguments[0];
            string apiKeyValue = null;

            if (!string.IsNullOrEmpty(ApiKey))
            {
                apiKeyValue = ApiKey;
            }
            else if (Arguments.Count > 1 && !string.IsNullOrEmpty(Arguments[1]))
            {
                apiKeyValue = Arguments[1];
            }

            try
            {
                await PushRunner.Run(
                    Settings,
                    SourceProvider,
                    new[] { packagePath },
                    Source,
                    apiKeyValue,
                    SymbolSource,
                    SymbolApiKey,
                    Timeout,
                    DisableBuffering,
                    NoSymbols,
                    NoServiceEndpoint,
                    SkipDuplicate,
                    Console);
            }
            catch (TaskCanceledException ex)
            {
                string timeoutMessage = LocalizedResourceManager.GetString(nameof(NuGetResources.PushCommandTimeoutError));
                throw new AggregateException(ex, new Exception(timeoutMessage));
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException && ex.InnerException is WebException)
                {
                    throw ex.InnerException;
                }

                throw;
            }
        }
    }
}
