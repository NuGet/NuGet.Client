// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using NuGet.CommandLine.XPlat.Commands;
using NuGet.CommandLine.XPlat.Commands.PackageDownload;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageDownloadCommand
    {
        public static void Register(Command rootCommand, Func<ILoggerWithColor> getLogger)
        {
            var downloadCommand = new DocumentedCommand(
                "download",
                Strings.pkgDownload_descritpion,
                "https://aka.ms/dotnet/package/download");

            // Arguments
            var packageId = new Argument<string>("PackageId")
            {
                Description = Strings.pkgDownload_packageIdDescription,
                Arity = ArgumentArity.ExactlyOne,
            };

            // Options
            var allowInsecureConnections = new Option<bool>("--allow-insecure-connections")
            {
                Description = Strings.pkgDownload_AllowInsecureConnectionsDescritption,
                Arity = ArgumentArity.Zero
            };

            var configFile = new Option<string>("--configfile")
            {
                Description = Strings.pkgDownload_configFileDesciption,
                Arity = ArgumentArity.ExactlyOne
            };

            var downloadOnly = new Option<bool>("--download-only")
            {
                Description = Strings.pkgDownload_downloadOnlyDeciption,
                Arity = ArgumentArity.Zero
            };

            var help = new HelpOption()
            {
                Arity = ArgumentArity.Zero
            };

            var interactive = new Option<bool>("--interactive")
            {
                Description = Strings.pkgDownload_interactiveDecription,
                Arity = ArgumentArity.Zero
            };

            var outputDirectory = new Option<string>("--output-directory")
            {
                Description = Strings.pkgDownload_OutputDirectoryDescription,
                Arity = ArgumentArity.ExactlyOne
            };

            var prerelease = new Option<bool>("--prerelease")
            {
                Description = Strings.pkgDownload_prereleaseDescription,
                Arity = ArgumentArity.Zero
            };

            var sources = new Option<List<string>>("--source")
            {
                Description = Strings.pkgDownload_sourcesDescription,
                Arity = ArgumentArity.OneOrMore
            };

            var verbosity = new Option<string>("--verbosity")
            {
                Description = Strings.pkgDownload_verbosityDescription,
                Arity = ArgumentArity.ExactlyOne
            };

            var version = new Option<string>("--version")
            {
                Description = Strings.pkgDownload_versionDescription,
                Arity = ArgumentArity.ExactlyOne
            };


            downloadCommand.Arguments.Add(packageId);
            downloadCommand.Options.Add(allowInsecureConnections);
            downloadCommand.Options.Add(configFile);
            downloadCommand.Options.Add(downloadOnly);
            downloadCommand.Options.Add(help);
            downloadCommand.Options.Add(interactive);
            downloadCommand.Options.Add(outputDirectory);
            downloadCommand.Options.Add(prerelease);
            downloadCommand.Options.Add(sources);
            downloadCommand.Options.Add(verbosity);
            downloadCommand.Options.Add(version);

            downloadCommand.SetAction(async (parserResult, cancellationToken) =>
            {
                ILoggerWithColor logger = getLogger();

                try
                {
                    var args = new PackageDownloadArgs(parserResult.GetValue(packageId), parserResult.GetValue(sources), parserResult.GetValue(outputDirectory), logger)
                    {
                        Version = parserResult.GetValue(version),
                        IncludePrerelease = parserResult.GetValue(prerelease),
                        DownloadOnly = parserResult.GetValue(downloadOnly),
                        AllowInsecureConnections = parserResult.GetValue(allowInsecureConnections),
                        Interactive = parserResult.GetValue(interactive),
                        ConfigFile = parserResult.GetValue(configFile)
                    };

                    args.SetVerbosity(parserResult.GetValue(verbosity));

                    return await PackageDownloadRunner.RunAsync(args, cancellationToken);
                }
                catch (ArgumentException ex)
                {
                    logger.LogError(ex.Message);
                    return ExitCodes.InvalidArguments;
                }
            });

            rootCommand.Subcommands.Add(downloadCommand);
        }
    }
}
