// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
                CommandArgument command = trustedSignersCmd.Argument(
                    "<command>",
                    Strings.TrustCommandActionDescription,
                    multipleValues: true);

                CommandOption algorithm = trustedSignersCmd.Option(
                    "--algorithm",
                    Strings.TrustCommandAlgorithm,
                    CommandOptionType.SingleValue);

                CommandOption allowUntrustedRootOption = trustedSignersCmd.Option(
                    "--allow-untrusted-root",
                    Strings.TrustCommandAllowUntrustedRoot,
                    CommandOptionType.NoValue);

                CommandOption owners = trustedSignersCmd.Option(
                    "--owners",
                    Strings.TrustCommandOwners,
                    CommandOptionType.MultipleValue);

                CommandOption verbosity = trustedSignersCmd.Option(
                    "-v|--verbosity",
                    Strings.Verbosity_Description,
                    CommandOptionType.SingleValue);

                CommandOption configFile = trustedSignersCmd.Option(
                    "--configfile",
                    Strings.Option_ConfigFile,
                    CommandOptionType.SingleValue);

                trustedSignersCmd.HelpOption(XPlatUtility.HelpOption);
                trustedSignersCmd.Description = Strings.TrustCommandDescription;

                trustedSignersCmd.OnExecute(async () =>
                {
                    TrustCommand action;

                    if (!command.Values.Any() || string.IsNullOrEmpty(command.Values[0]))
                    {
                        action = TrustCommand.List;
                    }
                    else if (!Enum.TryParse(command.Values[0], ignoreCase: true, result: out action))
                    {
                        throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_UnknownAction, command.Values[0]));
                    }

                    string name = null;

                    if (command.Values.Count > 1)
                    {
                        name = command.Values[1];
                    }

                    string packagePath = null;
                    string sourceUrl = null;
                    string fingerprint = null;
                    if (command.Values.Count() > 2)
                    {
                        if (action == TrustCommand.Author || action == TrustCommand.Repository)
                        {
                            packagePath = command.Values[2];
                        }
                        else if (action == TrustCommand.Source)
                        {
                            sourceUrl = command.Values[2];
                        }
                        else if (action == TrustCommand.Certificate)
                        {
                            fingerprint = command.Values[2];
                        }
                    }

                    ISettings settings = ProcessConfigFile(configFile.Value());

                    var trustedSignersArgs = new TrustedSignersArgs()
                    {
                        Action = MapTrustEnumAction(action),
                        PackagePath = packagePath,
                        Name = name,
                        ServiceIndex = sourceUrl,
                        CertificateFingerprint = fingerprint,
                        FingerprintAlgorithm = algorithm.Value(),
                        AllowUntrustedRoot = allowUntrustedRootOption.HasValue(),
                        Author = action == TrustCommand.Author,
                        Repository = action == TrustCommand.Repository,
                        Owners = CommandLineUtility.SplitAndJoinAcrossMultipleValues(owners.Values),
                        Logger = getLogger()
                    };

                    setLogLevel(XPlatUtility.MSBuildVerbosityToNuGetLogLevel(verbosity.Value()));

#pragma warning disable CS0618 // Type or member is obsolete
                    var sourceProvider = new PackageSourceProvider(settings, enablePackageSourcesChangedEvent: false);
#pragma warning restore CS0618 // Type or member is obsolete
                    var trustedSignersProvider = new TrustedSignersProvider(settings);

                    var runner = new TrustedSignersCommandRunner(trustedSignersProvider, sourceProvider);
                    Task<int> trustedSignTask = runner.ExecuteCommandAsync(trustedSignersArgs);
                    return await trustedSignTask;
                });
            });
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
