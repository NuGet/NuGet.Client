// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat
{
    internal static class ApiKeyCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("apikey", apiKey =>
            {
                apiKey.Description = Strings.ApiKeyCommand_Description;
                apiKey.HelpOption(XPlatUtility.HelpOption);

                RegisterSetCommand(apiKey, getLogger);
                RegisterUnsetCommand(apiKey, getLogger);

                apiKey.OnExecute(() =>
                {
                    apiKey.ShowHelp();
                    return ExitCodes.InvalidArguments;
                });
            });
        }

        private static void RegisterSetCommand(CommandLineApplication apiKey, Func<ILogger> getLogger)
        {
            apiKey.Command("set", set =>
            {
                set.Description = Strings.ApiKeySetCommand_Description;
                set.HelpOption(XPlatUtility.HelpOption);

                CommandOption source = set.Option(
                    "-s|--source <source>",
                    Strings.ApiKeyCommand_SourceDescription,
                    CommandOptionType.SingleValue);

                CommandOption configurationFile = set.Option(
                    "--configfile",
                    Strings.Option_ConfigFile,
                    CommandOptionType.SingleValue);

                CommandArgument apiKeyArgument = set.Argument(
                    "[apiKey]",
                    Strings.ApiKeySetCommand_ApiKeyDescription);

                set.OnExecute(() =>
                {
                    var args = new ApiKeySetArgs
                    {
                        ApiKey = apiKeyArgument.Value,
                        Source = source.Value(),
                        ConfigFile = configurationFile.Value()
                    };

                    return ApiKeySetRunner.Run(args, getLogger);
                });
            });
        }

        private static void RegisterUnsetCommand(CommandLineApplication apiKey, Func<ILogger> getLogger)
        {
            apiKey.Command("unset", unset =>
            {
                unset.Description = Strings.ApiKeyUnsetCommand_Description;
                unset.HelpOption(XPlatUtility.HelpOption);

                CommandOption source = unset.Option(
                    "-s|--source <source>",
                    Strings.ApiKeyCommand_SourceDescription,
                    CommandOptionType.SingleValue);

                CommandOption configurationFile = unset.Option(
                    "--configfile",
                    Strings.Option_ConfigFile,
                    CommandOptionType.SingleValue);

                unset.OnExecute(() =>
                {
                    var args = new ApiKeyUnsetArgs
                    {
                        Source = source.Value(),
                        ConfigFile = configurationFile.Value()
                    };

                    return ApiKeyUnsetRunner.Run(args, getLogger);
                });
            });
        }
    }

    internal sealed class ApiKeySetArgs
    {
        public string? ApiKey { get; set; }

        public string? Source { get; set; }

        public string? ConfigFile { get; set; }
    }

    internal sealed class ApiKeyUnsetArgs
    {
        public string? Source { get; set; }

        public string? ConfigFile { get; set; }
    }

    internal static class ApiKeySetRunner
    {
        public static int Run(ApiKeySetArgs args, Func<ILogger> getLogger)
        {
            RunnerHelper.EnsureArgumentsNotNull(args, getLogger);

            if (string.IsNullOrEmpty(args.ApiKey))
            {
                getLogger().LogError(Strings.ApiKeySetCommand_MissingApiKey);
                return ExitCodes.InvalidArguments;
            }

            PackageSourceProvider sourceProvider = ApiKeyRunnerUtility.GetPackageSourceProvider(args.ConfigFile);
            string source = ApiKeyRunnerUtility.ResolveSource(sourceProvider, args.Source);
            ISettings settings = ApiKeyRunnerUtility.GetSettingsForWriting(args.ConfigFile);

            SettingsUtility.SetEncryptedValueForAddItem(settings, ConfigurationConstants.ApiKeys, source, args.ApiKey);

            getLogger().LogMinimal(string.Format(
                CultureInfo.CurrentCulture,
                Strings.ApiKeySetCommand_ApiKeySaved,
                source));

            return ExitCodes.Success;
        }
    }

    internal static class ApiKeyUnsetRunner
    {
        public static int Run(ApiKeyUnsetArgs args, Func<ILogger> getLogger)
        {
            RunnerHelper.EnsureArgumentsNotNull(args, getLogger);

            PackageSourceProvider sourceProvider = ApiKeyRunnerUtility.GetPackageSourceProvider(args.ConfigFile);
            string source = ApiKeyRunnerUtility.ResolveSource(sourceProvider, args.Source);
            ISettings settings = ApiKeyRunnerUtility.GetSettingsForWriting(args.ConfigFile);

            if (SettingsUtility.DeleteValue(settings, ConfigurationConstants.ApiKeys, ConfigurationConstants.KeyAttribute, source))
            {
                getLogger().LogMinimal(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ApiKeyUnsetCommand_ApiKeyRemoved,
                    source));
            }
            else
            {
                getLogger().LogMinimal(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ApiKeyUnsetCommand_ApiKeyNotFound,
                    source));
            }

            return ExitCodes.Success;
        }
    }

    internal static class ApiKeyRunnerUtility
    {
        public static PackageSourceProvider GetPackageSourceProvider(string? configFile)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new PackageSourceProvider(GetSettingsForReading(configFile), enablePackageSourcesChangedEvent: false);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public static ISettings GetSettingsForWriting(string? configFile)
        {
            if (!string.IsNullOrEmpty(configFile))
            {
                return XPlatUtility.ProcessConfigFile(configFile);
            }

            return Settings.LoadDefaultSettings(
                root: null,
                configFileName: null,
                machineWideSettings: null);
        }

        public static string ResolveSource(PackageSourceProvider sourceProvider, string? source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return NuGetConstants.DefaultGalleryServerUrl;
            }

            return sourceProvider.ResolveAndValidateSource(source);
        }

        private static ISettings GetSettingsForReading(string? configFile)
        {
            if (!string.IsNullOrEmpty(configFile))
            {
                return XPlatUtility.ProcessConfigFile(configFile);
            }

            return Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());
        }
    }
}
