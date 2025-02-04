// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CommandLine.XPlat.Commands;
using NuGet.Configuration;
using NuGet.Credentials;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchCommand
    {
        public static void Register(Command rootCommand, Func<ILoggerWithColor> getLogger)
        {
            Register(rootCommand, getLogger, SetupSettingsAndRunSearchAsync);
        }

        public static void Register(Command rootCommand, Func<ILoggerWithColor> getLogger, Func<PackageSearchArgs, string, CancellationToken, Task<int>> setupSettingsAndRunSearchAsync)
        {
            DocumentedCommand searchCommand = new("search", Strings.pkgSearch_Description, "https://aka.ms/dotnet/package/search");

            ArgumentOfString searchTerm = new("Search Term")
            {
                Description = Strings.pkgSearch_termDescription,
                Arity = ArgumentArity.ZeroOrOne,
            };

            OptionOfListOfStrings sources = new("--source")
            {
                Description = Strings.pkgSearch_SourceDescription,
                Arity = ArgumentArity.OneOrMore
            };

            OptionOfBoolean exactMatch = new("--exact-match")
            {
                Description = Strings.pkgSearch_ExactMatchDescription,
                Arity = ArgumentArity.Zero
            };

            OptionOfBoolean prerelease = new("--prerelease")
            {
                Description = Strings.pkgSearch_PrereleaseDescription,
                Arity = ArgumentArity.Zero
            };

            OptionOfBoolean interactive = new("--interactive")
            {
                Description = Strings.pkgSearch_InteractiveDescription,
                Arity = ArgumentArity.Zero
            };

            OptionOfString take = new("--take")
            {
                Description = Strings.pkgSearch_TakeDescription,
                Arity = ArgumentArity.ExactlyOne
            };

            OptionOfString skip = new("--skip")
            {
                Description = Strings.pkgSearch_SkipDescription,
                Arity = ArgumentArity.ExactlyOne
            };

            OptionOfString format = new("--format")
            {
                Description = Strings.pkgSearch_FormatDescription,
                Arity = ArgumentArity.ExactlyOne
            };

            OptionOfString verbosity = new("--verbosity")
            {
                Description = Strings.pkgSearch_VerbosityDescription,
                Arity = ArgumentArity.ExactlyOne
            };

            OptionOfString configFile = new("--configfile")
            {
                Description = Strings.Option_ConfigFile,
                Arity = ArgumentArity.ExactlyOne
            };

            HelpOption help = new()
            {
                Arity = ArgumentArity.Zero
            };

            searchCommand.Arguments.Add(searchTerm);
            searchCommand.Options.Add(sources);
            searchCommand.Options.Add(exactMatch);
            searchCommand.Options.Add(prerelease);
            searchCommand.Options.Add(interactive);
            searchCommand.Options.Add(take);
            searchCommand.Options.Add(skip);
            searchCommand.Options.Add(format);
            searchCommand.Options.Add(verbosity);
            searchCommand.Options.Add(configFile);
            searchCommand.Options.Add(help);

            searchCommand.SetAction(async (parserResult, cancelationToken) =>
            {
                ILoggerWithColor logger = getLogger();

                try
                {
                    var packageSearchArgs = new PackageSearchArgs(parserResult.GetValue(skip), parserResult.GetValue(take), parserResult.GetValue(format), parserResult.GetValue(verbosity))
                    {
                        Sources = parserResult.GetValue(sources),
                        SearchTerm = parserResult.GetValue(searchTerm),
                        ExactMatch = parserResult.GetValue(exactMatch),
                        Interactive = parserResult.GetValue(interactive),
                        Prerelease = parserResult.GetValue(prerelease),
                        Logger = logger,
                    };

                    return await setupSettingsAndRunSearchAsync(packageSearchArgs, parserResult.GetValue(configFile), cancelationToken);
                }
                catch (ArgumentException ex)
                {
                    logger.LogError(ex.Message);
                    return ExitCodes.InvalidArguments;
                }
            });

            rootCommand.Subcommands.Add(searchCommand);
        }

        public static async Task<int> SetupSettingsAndRunSearchAsync(PackageSearchArgs packageSearchArgs, string configFile, CancellationToken cancellationToken)
        {
            DefaultCredentialServiceUtility.SetupDefaultCredentialService(packageSearchArgs.Logger, !packageSearchArgs.Interactive);

            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: configFile,
                machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);

            return await PackageSearchRunner.RunAsync(
                sourceProvider,
                packageSearchArgs,
                cancellationToken);
        }
    }
}
