// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Signing;
using static NuGet.Commands.TrustedSignersArgs;

namespace NuGet.CommandLine.XPlat
{
    internal static class TrustedSignersCommand
    {
        internal static void Register(CommandLineApplication app,
                      Func<ILogger> getLogger,
                      Action<LogLevel> setLogLevel)
        {
            app.Command("trust", trustedSignersCmd =>
            {
                // sub-commands
                trustedSignersCmd.Command("list", (listCommand) =>
                {
                    listCommand.Description = Strings.TrustListCommandDescription;
                    CommandOption configFile = listCommand.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);

                    listCommand.HelpOption(XPlatUtility.HelpOption);

                    CommandOption verbosity = listCommand.Option(
                        "-v|--verbosity",
                        Strings.Verbosity_Description,
                        CommandOptionType.SingleValue);

                    listCommand.OnExecute(async () =>
                    {
                        return await ExecuteCommand(TrustCommand.List, algorithm : null, allowUntrustedRootOption: false, owners: null, verbosity, configFile, getLogger, setLogLevel);
                    });
                });

                trustedSignersCmd.Command("sync", (syncCommand) =>
                {
                    syncCommand.Description = Strings.TrustSyncCommandDescription;
                    CommandOption configFile = syncCommand.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);

                    syncCommand.HelpOption(XPlatUtility.HelpOption);

                    CommandOption verbosity = syncCommand.Option(
                        "-v|--verbosity",
                        Strings.Verbosity_Description,
                        CommandOptionType.SingleValue);

                    CommandArgument name = syncCommand.Argument("<NAME>",
                                               "The name of the existing trusted signer to sync.");

                    syncCommand.OnExecute(async () =>
                    {
                        return await ExecuteCommand(TrustCommand.Sync, algorithm: null, allowUntrustedRootOption: false, owners: null, verbosity, configFile, getLogger, setLogLevel, name: name.Value);
                    });
                });

                trustedSignersCmd.Command("remove", (syncCommand) =>
                {
                    syncCommand.Description = Strings.TrustRemoveCommandDescription;
                    CommandOption configFile = syncCommand.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);

                    syncCommand.HelpOption(XPlatUtility.HelpOption);

                    CommandOption verbosity = syncCommand.Option(
                        "-v|--verbosity",
                        Strings.Verbosity_Description,
                        CommandOptionType.SingleValue);

                    CommandArgument name = syncCommand.Argument("<NAME>",
                                               "The name of the existing trusted signer to remove.");

                    syncCommand.OnExecute(async () =>
                    {
                        return await ExecuteCommand(TrustCommand.Remove, algorithm: null, allowUntrustedRootOption: false, owners: null, verbosity, configFile, getLogger, setLogLevel, name: name.Value);
                    });
                });

                trustedSignersCmd.Command("author", (authorCommand) =>
                {
                    authorCommand.Description = Strings.TrustAuthorCommandDescription;

                    CommandOption allowUntrustedRootOption = authorCommand.Option(
                                                "--allow-untrusted-root",
                                                Strings.TrustCommandAllowUntrustedRoot,
                                                CommandOptionType.NoValue);

                    CommandOption configFile = authorCommand.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);

                    authorCommand.HelpOption(XPlatUtility.HelpOption);

                    CommandOption verbosity = authorCommand.Option(
                        "-v|--verbosity",
                        Strings.Verbosity_Description,
                        CommandOptionType.SingleValue);

                    CommandArgument name = authorCommand.Argument("<NAME>",
                                               "The name of the trusted signer to add. If NAME already exists in the configuration, the signature is appended.");
                    CommandArgument package = authorCommand.Argument("<PACKAGE>",
                                               "The given PACKAGE should be a local path to the signed .nupkg file.");

                    authorCommand.OnExecute(async () =>
                    {
                        return await ExecuteCommand(TrustCommand.Author, algorithm: null, allowUntrustedRootOption.HasValue(), owners: null, verbosity, configFile, getLogger, setLogLevel, name: name.Value, sourceUrl: null, packagePath: package.Value);
                    });
                });

                trustedSignersCmd.Command("repository", (repositoryCommand) =>
                {
                    repositoryCommand.Description = Strings.TrustRepositoryCommandDescription;

                    CommandOption allowUntrustedRootOption = repositoryCommand.Option(
                                                "--allow-untrusted-root",
                                                Strings.TrustCommandAllowUntrustedRoot,
                                                CommandOptionType.NoValue);

                    CommandOption configFile = repositoryCommand.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);

                    repositoryCommand.HelpOption(XPlatUtility.HelpOption);

                    CommandOption verbosity = repositoryCommand.Option(
                        "-v|--verbosity",
                        Strings.Verbosity_Description,
                        CommandOptionType.SingleValue);

                    CommandArgument name = repositoryCommand.Argument("<NAME>",
                                               "The name of the trusted signer to add. If NAME already exists in the configuration, the signature is appended.");
                    CommandArgument package = repositoryCommand.Argument("<PACKAGE>",
                                               "The given PACKAGE should be a local path to the signed .nupkg file.");

                    repositoryCommand.OnExecute(async () =>
                    {
                        return await ExecuteCommand(TrustCommand.Repository, algorithm: null, allowUntrustedRootOption.HasValue(), owners: null, verbosity, configFile, getLogger, setLogLevel, name: name.Value, sourceUrl: null, packagePath: package.Value);
                    });
                });

                trustedSignersCmd.Command("certificate", (certificateCommand) =>
                {
                    certificateCommand.Description = Strings.TrustRepositoryCommandDescription;

                    CommandOption algorithm = certificateCommand.Option(
                        "--algorithm",
                        Strings.TrustCommandAlgorithm,
                        CommandOptionType.SingleValue);

                    CommandOption allowUntrustedRootOption = certificateCommand.Option(
                                                "--allow-untrusted-root",
                                                Strings.TrustCommandAllowUntrustedRoot,
                                                CommandOptionType.NoValue);

                    CommandOption configFile = certificateCommand.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);

                    certificateCommand.HelpOption(XPlatUtility.HelpOption);

                    CommandOption verbosity = certificateCommand.Option(
                        "-v|--verbosity",
                        Strings.Verbosity_Description,
                        CommandOptionType.SingleValue);

                    CommandArgument name = certificateCommand.Argument("<NAME>",
                                               "The name of the trusted signer to add. If a trusted signer with the given name already exists, the certificate item is added to that signer. Otherwise a trusted author is created with a certificate item from the given certificate information.");
                    CommandArgument fingerprint = certificateCommand.Argument("<FINGERPRINT>",
                                               "The fingerprint of the certificate.");

                    certificateCommand.OnExecute(async () =>
                    {
                        return await ExecuteCommand(TrustCommand.Certificate, algorithm, allowUntrustedRootOption.HasValue(), owners: null, verbosity, configFile, getLogger, setLogLevel, name: name.Value, sourceUrl: null, packagePath: null, fingerprint: fingerprint.Value);
                    });
                });

                trustedSignersCmd.Command("source", (sourceCommand) =>
                {
                    sourceCommand.Description = Strings.TrustRepositoryCommandDescription;

                    CommandOption configFile = sourceCommand.Option(
                        "--configfile",
                        Strings.Option_ConfigFile,
                        CommandOptionType.SingleValue);

                    sourceCommand.HelpOption(XPlatUtility.HelpOption);

                    CommandOption owners = trustedSignersCmd.Option(
                        "--owners",
                        Strings.TrustCommandOwners,
                        CommandOptionType.MultipleValue);

                    CommandOption sourceUrl = sourceCommand.Option(
                        "--source-url",
                        "If a source-url is provided, it must be a v3 package source URL (like https://api.nuget.org/v3/index.json). Other package source types are not supported.",
                        CommandOptionType.SingleValue);

                    CommandOption verbosity = sourceCommand.Option(
                        "-v|--verbosity",
                        Strings.Verbosity_Description,
                        CommandOptionType.SingleValue);

                    CommandArgument name = sourceCommand.Argument("<NAME>",
                                               "The name of the trusted signer to add. If only <NAME> is provided without --<source-url>, the package source from your NuGet configuration files with the same name is added to the trusted list. If <NAME> already exists in the configuration, the package source is appended to it.");
                    CommandArgument fingerprint = sourceCommand.Argument("<FINGERPRINT>",
                                               "The fingerprint of the certificate.");

                    sourceCommand.OnExecute(async () =>
                    {
                        return await ExecuteCommand(TrustCommand.Source, algorithm: null, allowUntrustedRootOption: false, owners, verbosity, configFile, getLogger, setLogLevel, name: name.Value, sourceUrl: sourceUrl.Value());
                    });
                });

                // Main command
                trustedSignersCmd.Description = Strings.TrustCommandDescription;
                CommandOption mainConfigFile = trustedSignersCmd.Option(
                    "--configfile",
                    Strings.Option_ConfigFile,
                    CommandOptionType.SingleValue);

                trustedSignersCmd.HelpOption(XPlatUtility.HelpOption);

                CommandOption mainVerbosity = trustedSignersCmd.Option(
                    "-v|--verbosity",
                    Strings.Verbosity_Description,
                    CommandOptionType.SingleValue);

                trustedSignersCmd.OnExecute(async () =>
                {
                    // If no command specified then default to List command.
                    return await ExecuteCommand(TrustCommand.List, algorithm: null, allowUntrustedRootOption: false, owners : null, mainVerbosity, mainConfigFile, getLogger, setLogLevel);
                });
            });
        }

        private static async Task<int> ExecuteCommand(TrustCommand action,
                      CommandOption algorithm,
                      bool allowUntrustedRootOption,
                      CommandOption owners,
                      CommandOption verbosity,
                      CommandOption configFile,
                      Func<ILogger> getLogger,
                      Action<LogLevel> setLogLevel,
                      string name = null,
                      string sourceUrl = null,
                      string packagePath = null,
                      string fingerprint = null)
        {
            ILogger logger = getLogger();

            try
            {
                ISettings settings = ProcessConfigFile(configFile.Value());

                var trustedSignersArgs = new TrustedSignersArgs()
                {
                    Action = MapTrustEnumAction(action),
                    PackagePath = packagePath,
                    Name = name,
                    ServiceIndex = sourceUrl,
                    CertificateFingerprint = fingerprint,
                    FingerprintAlgorithm = algorithm.Value(),
                    AllowUntrustedRoot = allowUntrustedRootOption,
                    Author = action == TrustCommand.Author,
                    Repository = action == TrustCommand.Repository,
                    Owners = CommandLineUtility.SplitAndJoinAcrossMultipleValues(owners?.Values),
                    Logger = logger
                };

                setLogLevel(XPlatUtility.MSBuildVerbosityToNuGetLogLevel(verbosity.Value()));

#pragma warning disable CS0618 // Type or member is obsolete
                var sourceProvider = new PackageSourceProvider(settings, enablePackageSourcesChangedEvent: false);
#pragma warning restore CS0618 // Type or member is obsolete
                var trustedSignersProvider = new TrustedSignersProvider(settings);

                var runner = new TrustedSignersCommandRunner(trustedSignersProvider, sourceProvider);
                Task<int> trustedSignTask = runner.ExecuteCommandAsync(trustedSignersArgs);
                return await trustedSignTask;
            }
            catch (Exception e)
            {
                // nuget trust command handled exceptions.
                if (e is InvalidOperationException)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var error_TrustedSignerAlreadyExistsMessage = StringFormatter.Log_TrustedSignerAlreadyExists(name);

                        if (e.Message == error_TrustedSignerAlreadyExistsMessage)
                        {
                            logger.LogError(error_TrustedSignerAlreadyExistsMessage);
                            return 1;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(sourceUrl))
                    {
                        var error_TrustedRepoAlreadyExists = StringFormatter.Log_TrustedRepoAlreadyExists(sourceUrl);

                        if (e.Message == error_TrustedRepoAlreadyExists)
                        {
                            logger.LogError(error_TrustedRepoAlreadyExists);
                            return 1;
                        }
                    }
                }

                // Unhandled exceptions bubble up.
                throw;
            }
        }

        private static TrustedSignersAction MapTrustEnumAction(TrustCommand trustCommand)
        {
            switch (trustCommand)
            {
                case TrustCommand.List:
                    return TrustedSignersAction.List;
                case TrustCommand.Remove:
                    return TrustedSignersAction.Remove;
                case TrustCommand.Sync:
                    return TrustedSignersAction.Sync;
                default:
                    return TrustedSignersAction.Add;
            }
        }

        private static ISettings ProcessConfigFile(string configFile)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                return XPlatUtility.CreateDefaultSettings();
            }

            var configFileFullPath = Path.GetFullPath(configFile);
            var directory = Path.GetDirectoryName(configFileFullPath);
            var configFileName = Path.GetFileName(configFileFullPath);
            return Settings.LoadDefaultSettings(
                directory,
                configFileName,
                machineWideSettings: new XPlatMachineWideSetting());
        }
    }
}
