// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;

namespace NuGet.CommandLine.XPlat
{
    internal static class SourcesCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("sources", sources =>
            {
                sources.Description = Strings.SourcesCommandDescription;
                sources.HelpOption(XPlatUtility.HelpOption);

                var arguments = sources.Argument(
                    "Action",
                    Strings.Sources_Action,
                    multipleValues: false);

                var name = sources.Option(
                    "-n|--name <name>",
                    Strings.SourcesCommandNameDescription,
                    CommandOptionType.SingleValue);

                var source = sources.Option(
                    "-s|--source <source>",
                    Strings.SourcesCommandSourceDescription,
                    CommandOptionType.SingleValue);

                var username = sources.Option(
                    "-u|--username <username>",
                    Strings.SourcesCommandUserNameDescription,
                    CommandOptionType.SingleValue);

                var password = sources.Option(
                    "-p|--password <password>",
                    Strings.SourcesCommandUserNameDescription,
                    CommandOptionType.SingleValue);

                var storePasswordInClearText = sources.Option(
                    "--store-password-in-clear-text",
                    Strings.SourcesCommandStorePasswordInClearTextDescription,
                    CommandOptionType.NoValue);

                var validAuthenticationTypes = sources.Option(
                    "--valid-authentication-types",
                    Strings.SourcesCommandValidAuthenticationTypesDescription,
                    CommandOptionType.SingleValue);

                var format = sources.Option(
                    "-f|--format",
                    Strings.SourcesCommandFormatDescription,
                    CommandOptionType.SingleValue);

                var interactive = sources.Option(
                    "--interactive",
                    Strings.NuGetXplatCommand_Interactive,
                    CommandOptionType.NoValue);

                var configfile = sources.Option(
                    "-c|--configfile",
                    Strings.Option_ConfigFile,
                    CommandOptionType.SingleValue);

                sources.OnExecute(() =>
                {
                    SourcesAction action = SourcesAction.None;

                    string actionArg = null;
                    if (arguments.Values.Count == 0)
                    {
                        action = SourcesAction.List;
                    }
                    else
                    {
                        actionArg = arguments.Values[0];
                        if (!Enum.TryParse<SourcesAction>(actionArg, ignoreCase: true, out action))
                        {
                            Console.WriteLine(string.Format(CultureInfo.CurrentCulture,
                                Strings.SourcesCommandUsageSummary));
                            return 1;
                        }
                    }

                    SourcesListFormat formatValue;
                    Enum.TryParse<SourcesListFormat>(format.Value(), ignoreCase: true, out formatValue);

                    var settings = GetSettings(configfile.Value(), Directory.GetCurrentDirectory());

#pragma warning disable CS0618 // Type or member is obsolete
                    var sourceProvider = new PackageSourceProvider(settings, enablePackageSourcesChangedEvent: false);
#pragma warning restore CS0618 // Type or member is obsolete

                    var sourcesArgs = new SourcesArgs(
                            settings,
                            sourceProvider,
                            action,
                            name.Value(),
                            source.Value(),
                            username.Value(),
                            password.Value(),
                            storePasswordInClearText.HasValue(),
                            validAuthenticationTypes.Value(),
                            formatValue,
                            interactive.HasValue(),
                            configfile.Value(),
                            isQuiet: false,    //TODO: Verbosity == Verbosity.Quiet
                            getLogger(),
                            logMinimalOverride: LogMinimalOverride
                            );

                    DefaultCredentialServiceUtility.SetupDefaultCredentialService(getLogger(), !interactive.HasValue());

                    SourcesRunner.Run(sourcesArgs);

                    // TODO: ensure errors are returning non-zero
                    return 0;
                });
            });
        }

        private static void LogMinimalOverride(string data)
        {
            // in dotnet sdk, we need to use Console.WriteLine instead of logger.LogMinimal to avoid "log: " prefix on each line
            Console.WriteLine(data);
        }

        private static ISettings GetSettings(string configfile, string currentDirectory)
        {
            if (string.IsNullOrEmpty(configfile))
            {
                return NuGet.Configuration.Settings.LoadDefaultSettings(currentDirectory,
                    configFileName: null,
                    machineWideSettings: new XPlatMachineWideSetting());
            }
            else
            {
                var configFileFullPath = Path.GetFullPath(configfile);
                var directory = Path.GetDirectoryName(configFileFullPath);
                var configFileName = Path.GetFileName(configFileFullPath);

                return NuGet.Configuration.Settings.LoadSpecificSettings(currentDirectory,
                    configFileName: configFileName);
            }
        }
    }
}
