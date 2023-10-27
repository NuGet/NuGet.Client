// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchCommand
    {
        internal delegate Task SetupSettingsAndRunSearchAsyncDelegate(PackageSearchArgs packageSearchArgs);

        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            Register(app, getLogger, SetupSettingsAndRunSearchAsync);
        }

        public static void Register(CommandLineApplication app, Func<ILogger> getLogger, SetupSettingsAndRunSearchAsyncDelegate setupSettingsAndRunSearchAsync)
        {
            app.Command("search", pkgSearch =>
            {
                pkgSearch.Description = Strings.pkgSearch_Description;
                CommandOption help = pkgSearch.HelpOption(XPlatUtility.HelpOption);
                CommandArgument searchTerm = pkgSearch.Argument(
                    "<Search Term>",
                    Strings.pkgSearch_termDescription);
                CommandOption sources = pkgSearch.Option(
                    "--source",
                    Strings.pkgSearch_SourceDescription,
                    CommandOptionType.MultipleValue);
                CommandOption exactMatch = pkgSearch.Option(
                    "--exact-match",
                    Strings.pkgSearch_ExactMatchDescription,
                    CommandOptionType.NoValue);
                CommandOption prerelease = pkgSearch.Option(
                    "--prerelease",
                    Strings.pkgSearch_PrereleaseDescription,
                    CommandOptionType.NoValue);
                CommandOption interactive = pkgSearch.Option(
                    "--interactive",
                    Strings.pkgSearch_InteractiveDescription,
                    CommandOptionType.NoValue);
                CommandOption take = pkgSearch.Option(
                    "--take",
                    Strings.pkgSearch_TakeDescription,
                    CommandOptionType.SingleValue);
                CommandOption skip = pkgSearch.Option(
                    "--skip",
                    Strings.pkgSearch_SkipDescription,
                    CommandOptionType.SingleValue);
                CommandOption format = pkgSearch.Option(
                    "--format",
                    Strings.pkgSearch_FormatDescription,
                    CommandOptionType.SingleValue);

                pkgSearch.OnExecute(async () =>
                {
                    // default values
                    PackageSearchArgs packageSearchArgs = new PackageSearchArgs(skip.Value(), take.Value(), format.Value())
                    {
                        Sources = sources.Values,
                        SearchTerm = searchTerm.Value,
                        ExactMatch = exactMatch.HasValue(),
                        Interactive = interactive.HasValue(),
                        Prerelease = prerelease.HasValue(),
                        Logger = getLogger(),
                    };

                    await setupSettingsAndRunSearchAsync(packageSearchArgs);

                    return 0;
                });

            });
        }

        public static async Task SetupSettingsAndRunSearchAsync(PackageSearchArgs packageSearchArgs)
        {
            DefaultCredentialServiceUtility.SetupDefaultCredentialService(packageSearchArgs.Logger, !packageSearchArgs.Interactive);

            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);

            await PackageSearchRunner.RunAsync(
                sourceProvider,
                packageSearchArgs,
                System.Threading.CancellationToken.None);
        }
    }
}
