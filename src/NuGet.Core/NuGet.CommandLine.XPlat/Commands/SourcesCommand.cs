// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
                    Strings.SourcesCommandConfigfileDescription,
                    CommandOptionType.SingleValue);

                sources.OnExecute(() =>
                {
                    string action = "";
                    if (arguments.Values.Count > 0)
                    {
                        action = arguments.Values[0].ToLowerInvariant();
                    }

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
                            format.Value()?.ToLower(),
                            interactive.HasValue(),
                            configfile.Value(),
                            getLogger().LogError,
                            getLogger().LogInformation
                            );

                    DefaultCredentialServiceUtility.SetupDefaultCredentialService(getLogger(), !interactive.HasValue());

                    SourcesRunner.Run(sourcesArgs);

                    return 0;
                });
            });
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
