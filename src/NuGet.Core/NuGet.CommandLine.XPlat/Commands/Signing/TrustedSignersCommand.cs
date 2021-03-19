// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Signing;
using static NuGet.Commands.TrustedSignersArgs;

namespace NuGet.CommandLine.XPlat.Commands.Signing
{
    internal static class TrustedSignersCommand
    {
        public enum TrustCommand
        {
            List,
            Author,
            Repository,
            Source,
            Certificate,
            Remove,
            Sync
        }

        internal static void Register(CommandLineApplication app,
                      Func<ILogger> getLogger,
                      Action<LogLevel> setLogLevel)
        {
            app.Command("trust", trustedSignersCmd =>
            {
                CommandArgument command = trustedSignersCmd.Argument(
                    "command",
                    Strings.VerifyCommandPackagePathDescription,
                    multipleValues: true);

                CommandOption algorithm = trustedSignersCmd.Option(
                    "--algorithm",
                    Strings.VerifyCommandAllDescription, // TrustedSignersCommandFingerprintAlgorithmDescription
                    CommandOptionType.SingleValue);

                CommandOption allowuntrustedrootOption = trustedSignersCmd.Option(
                    "--allow-untrusted-root",
                    Strings.VerifyCommandAllDescription, //TrustedSignersCommandAllowUntrustedRootDescription
                    CommandOptionType.NoValue);

                CommandOption owners = trustedSignersCmd.Option(
                    "--owners",
                    Strings.VerifyCommandAllDescription, //TrustedSignersCommandAllowUntrustedRootDescription
                    CommandOptionType.MultipleValue);

                CommandOption verbosity = trustedSignersCmd.Option(
                    "-v|--verbosity",
                    Strings.Verbosity_Description,
                    CommandOptionType.SingleValue);

                CommandOption configfile = trustedSignersCmd.Option(
                    "--configfile",
                    Strings.Option_ConfigFile,
                    CommandOptionType.SingleValue);

                trustedSignersCmd.HelpOption(XPlatUtility.HelpOption);
                trustedSignersCmd.Description = Strings.VerifyCommandDescription;

                trustedSignersCmd.OnExecute(async () =>
                {
                    ValidateCommand(command);

                    TrustCommand action;

                    if (!Enum.TryParse(command.Values[0], ignoreCase: true, result: out action))
                    {
                        throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture, "Unknowcommand", command.Values[0]));
                    }

                    string name = null;
                    if (command.Values.Count() > 1)
                    {
                        name = command.Values[1];
                    }

                    string packagePath = null;
                    string sourceUrl = null;
                    string fingerPrint = null;
                    if (command.Values.Count() > 2)
                    {
                        if(action == TrustCommand.Author || action == TrustCommand.Repository)
                        {
                            packagePath = command.Values[2];
                        }
                        else if(action == TrustCommand.Source)
                        {
                            sourceUrl = command.Values[2];
                        }
                        else if(action == TrustCommand.Certificate)
                        {
                            fingerPrint = command.Values[2];
                        }
                    }

                    ISettings settings = ProcessConfigFile(configfile.Value());

                    var trustedSignersArgs = new TrustedSignersArgs()
                    {
                        Action = MapTrustEnumAction(action),
                        PackagePath = packagePath,
                        Name = name,
                        ServiceIndex = sourceUrl,
                        CertificateFingerprint = fingerPrint,
                        FingerprintAlgorithm = algorithm.Value(),
                        AllowUntrustedRoot = allowuntrustedrootOption.HasValue(),
                        Author = action == TrustCommand.Author,
                        Repository = action == TrustCommand.Repository,
                        Owners = CommandLineUtility.SplitAndJoinAcrossMultipleValues(owners.Values),
                        Logger = getLogger()
                    };

                    // MSbuild doesn't pass verbosity parameter means it's normal verbosity for NuGet.
                    setLogLevel(XPlatUtility.MSBuildVerbosityToNuGetLogLevel(string.IsNullOrWhiteSpace(verbosity.Value()) ? "N" : verbosity.Value()));

                    var sourceProvider = PackageSourceBuilder.CreateSourceProvider(settings);
                    var trustedSignersProvider = new TrustedSignersProvider(settings);

                    var runner = new TrustedSignersCommandRunner(trustedSignersProvider, sourceProvider);
                    var trustedSignTask = runner.ExecuteCommandAsync(trustedSignersArgs);
                    await trustedSignTask;

                    return trustedSignTask.Result;
                });
            });
        }

        private static void ValidateCommand(CommandArgument argument)
        {
            if (argument.Values.Count == 0 ||
                argument.Values.Any<string>(packagePath => string.IsNullOrEmpty(packagePath)))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PkgMissingArgument,
                    "trust",
                    argument.Name));
            }
        }

        private static TrustedSignersAction MapTrustEnumAction(TrustCommand trustCommand)
        {
            switch(trustCommand)
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
