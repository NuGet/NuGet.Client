// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Credentials;

namespace NuGet.CommandLine
{
    public enum SourcesListFormat
    {
        Detailed,
        Short
    }

    [Command(typeof(NuGetCommand), "sources", "SourcesCommandDescription", UsageSummaryResourceName = "SourcesCommandUsageSummary",
        MinArgs = 0, MaxArgs = 1)]
    public class SourcesCommand : Command
    {
        [Option(typeof(NuGetCommand), "SourcesCommandNameDescription")]
        public string Name { get; set; }

        [Option(typeof(NuGetCommand), "SourcesCommandSourceDescription", AltName = "src")]
        public string Source { get; set; }

        [Option(typeof(NuGetCommand), "SourcesCommandUserNameDescription")]
        public string Username { get; set; }

        [Option(typeof(NuGetCommand), "SourcesCommandPasswordDescription")]
        public string Password { get; set; }

        [Option(typeof(NuGetCommand), "SourcesCommandStorePasswordInClearTextDescription")]
        public bool StorePasswordInClearText { get; set; }

        [Option(typeof(NuGetCommand), "SourcesCommandValidAuthenticationTypesDescription")]
        public string ValidAuthenticationTypes { get; set; }

        [Option(typeof(NuGetCommand), "SourcesCommandFormatDescription")]
        public SourcesListFormat Format { get; set; }

        public override void ExecuteCommand()
        {
            if (SourceProvider == null)
            {
                throw new InvalidOperationException(ResourceHelper.GetLocalizedString(typeof(string), "Error_SourceProviderIsNull"));
            }

            string action = Arguments.FirstOrDefault();
            var interactive = !NonInteractive;

            var sourcesArgs = new SourcesArgs(
                Settings,
                SourceProvider,
                action,
                Name,
                Source,
                Username,
                Password,
                StorePasswordInClearText,
                ValidAuthenticationTypes,
                Format.ToString()?.ToLower(),
                interactive,
                ConfigFile,
                Console.LogError,
                Console.LogInformation
                );

            SourcesRunner.Run(sourcesArgs);
        }
    }
}
