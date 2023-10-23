// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands.CommandRunners;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("search", pkgSearch =>
            {
                pkgSearch.Description = Strings.pkgSearch_Description;
                CommandOption help = pkgSearch.HelpOption(XPlatUtility.HelpOption);
                CommandArgument searchTern = pkgSearch.Argument(
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

                pkgSearch.OnExecute(async () =>
                {
                    // default values
                    int takeValue = 20;
                    int skipValue = 0;

                    if (take.HasValue() && int.TryParse(take.Value(), out int takeVal))
                    {
                        takeValue = takeVal;
                    }

                    if (skip.HasValue() && int.TryParse(skip.Value(), out int skipVal))
                    {
                        skipValue = skipVal;
                    }

                    ILogger logger = getLogger();
                    DefaultCredentialServiceUtility.SetupDefaultCredentialService(logger, !interactive.HasValue());
                    ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory(),
                    configFileName: null,
                    machineWideSettings: new XPlatMachineWideSetting());
                    PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
                    await PackageSearchRunner.RunAsync(
                        sourceProvider, sources.Values, searchTern.Value, skipValue, takeValue, prerelease.HasValue(), exactMatch.HasValue(), logger, System.Threading.CancellationToken.None);
                    return 0;
                });

            });
        }
    }
}
