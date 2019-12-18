//// Copyright (c) .NET Foundation. All rights reserved.
//// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

//using System;
//using System.IO;
//using Microsoft.Extensions.CommandLineUtils;
//using NuGet.Commands;
//using NuGet.Common;
//using NuGet.Configuration;

//namespace NuGet.CommandLine.XPlat
//{
//    internal static class SourcesCommand
//    {
//        public static void Register(CommandLineApplication app, SourcesAction action, Func<ILogger> getLogger, bool needsNoun = false)
//        {
//            string actionStr = action.ToString().ToLower();
//            string description = GetCommandDescription(actionStr);

//            app.Command(actionStr, sources =>
//            {
//                sources.Description = description;

//                // these options are set in the switch statement, so depending on the action, may or may not be set.
//                CommandOption name = null,
//                                  format = null,
//                                  source = null,
//                                  username = null,
//                                  password = null,
//                                  storePasswordInClearText = null,
//                                  validAuthenticationTypes = null,
//                                  configfile = null;
//                if (needsNoun)
//                {
//                    var nestedCommand =
//                        new CommandLineApplication(throwOnUnexpectedArg: true)
//                            { Name = "source" };

//                    // need to tell them to put "source" as noun
//                    sources.Commands.Add(nestedCommand);
//                }
//                else
//                {
//                    switch (action)
//                    {
//                        case SourcesAction.List:
//                            format = sources.Option(
//                                "-f|--format",
//                                Strings.SourcesCommandFormatDescription,
//                                CommandOptionType.SingleValue);
//                            break;
//                        case SourcesAction.Add:
//                        case SourcesAction.Update:
//                            name = sources.Option(
//                                "-n|--name <name>",
//                                Strings.SourcesCommandNameDescription,
//                                CommandOptionType.SingleValue);
//                            source = sources.Option(
//                                "-s|--source <source>",
//                                Strings.SourcesCommandSourceDescription,
//                                CommandOptionType.SingleValue);
//                            username = sources.Option(
//                                "-u|--username <username>",
//                                Strings.SourcesCommandUserNameDescription,
//                                CommandOptionType.SingleValue);
//                            password = sources.Option(
//                                "-p|--password <password>",
//                                Strings.SourcesCommandUserNameDescription,
//                                CommandOptionType.SingleValue);
//                            storePasswordInClearText = sources.Option(
//                                "--store-password-in-clear-text",
//                                Strings.SourcesCommandStorePasswordInClearTextDescription,
//                                CommandOptionType.NoValue);
//                            validAuthenticationTypes = sources.Option(
//                                "--valid-authentication-types",
//                                Strings.SourcesCommandValidAuthenticationTypesDescription,
//                                CommandOptionType.SingleValue);
//                            break;
//                        case SourcesAction.Remove:
//                        case SourcesAction.Enable:
//                        case SourcesAction.Disable:
//                            name = sources.Option(
//                                "-n|--name <name>",
//                                Strings.SourcesCommandNameDescription,
//                                CommandOptionType.SingleValue);
//                            break;
//                    }

//                    configfile = sources.Option(
//                        "-c|--configfile",
//                        Strings.Option_ConfigFile,
//                        CommandOptionType.SingleValue);
//                }

//                sources.HelpOption(XPlatUtility.HelpOption);

//                sources.OnExecute(() =>
//                {
//                    var sourcesArgs = new SourcesArgs()
//                    {
//                        Action = action,
//                        Logger = getLogger(),
//                        LogMinimalOverride = LogMinimalOverride,
//                        Name = name?.Value(),
//                        Source = source?.Value(),
//                        Username = username?.Value(),
//                        Password = password?.Value(),
//                        ValidAuthenticationTypes = validAuthenticationTypes?.Value(),
//                        IsQuiet = false, // verbosity quiet not implemented in `dotnet nuget sources` yet. Covered by #6374.
//                    };

//                    if (storePasswordInClearText != null)
//                    {
//                        sourcesArgs.StorePasswordInClearText = storePasswordInClearText.HasValue();
//                    }

//                    if (format != null)
//                    {
//                        SourcesListFormat formatValue;
//                        Enum.TryParse<SourcesListFormat>(format.Value(), ignoreCase: true, out formatValue);
//                        sourcesArgs.Format = formatValue;
//                    }

//                    sourcesArgs.Settings = GetSettings(configfile.Value(), Directory.GetCurrentDirectory());
//#pragma warning disable CS0618 // Type or member is obsolete
//                    var sourceProvider = new PackageSourceProvider(sourcesArgs.Settings, enablePackageSourcesChangedEvent: false);
//#pragma warning restore CS0618 // Type or member is obsolete
//                    sourcesArgs.SourceProvider = sourceProvider;

//                    SourcesRunner.Run(sourcesArgs);

//                    return 0;
//                });
//            });
//        }

//        private static string GetCommandDescription(string actionStr)
//        {
//            if (actionStr == null)
//            {
//                throw new ArgumentNullException("actionStr");
//            }

//            switch (actionStr)
//            {
//                case "source":
//                    return Strings.SourcesCommandDescription;
//                case "add":
//                    return Strings.AddSourceCommandDescription;
//                case "remove":
//                    return Strings.RemoveSourceCommandDescription;
//                case "enable":
//                    return Strings.EnableSourceCommandDescription;
//                case "disable":
//                    return Strings.DisableCommandDescription;
//                case "list":
//                    return Strings.ListSourceCommandDescription;
//                case "update":
//                    return Strings.UpdateSourceCommandDescription;
//            }

//            return null;
//        }

//        private static void LogMinimalOverride(string data)
//        {
//            // in dotnet sdk, we need to use Console.WriteLine instead of logger.LogMinimal to avoid "log: " prefix on each line
//            Console.WriteLine(data);
//        }

//        private static ISettings GetSettings(string configfile, string currentDirectory)
//        {
//            if (string.IsNullOrEmpty(configfile))
//            {
//                // Use settings based on probing given currentDirectory
//                return NuGet.Configuration.Settings.LoadDefaultSettings(currentDirectory,
//                    configFileName: null,
//                    machineWideSettings: new XPlatMachineWideSetting());
//            }
//            else
//            {
//                // Use ConfigFile only
//                var configFileFullPath = Path.GetFullPath(configfile);
//                var configDirectory = Path.GetDirectoryName(configFileFullPath);
//                var configFileName = Path.GetFileName(configFileFullPath);

//                return NuGet.Configuration.Settings.LoadSpecificSettings(configDirectory,
//                    configFileName: configFileName);
//            }
//        }
//    }
//}
