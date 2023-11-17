// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging.Signing;
using static NuGet.Commands.VerifyArgs;

namespace NuGet.CommandLine.XPlat
{
    internal static class VerifyCommand
    {
        internal static Action<LogLevel> SetLogLevel;
        internal static Func<IVerifyCommandRunner> GetCommandRunner;

        internal static CliCommand Register(CliCommand app, Func<ILogger> getLogger, Action<LogLevel> setLogLevel, Func<IVerifyCommandRunner> getCommandRunner)
        {
            var VerifyCmd = new CliCommand(name: "verify", description: Strings.VerifyCommandDescription);

            // Options directly under the verb 'push'

            // Options under sub-command: push
            RegisterOptionsForCommandVerify(VerifyCmd, getLogger);
            SetLogLevel = setLogLevel;
            GetCommandRunner = getCommandRunner;
            app.TreatUnmatchedTokensAsErrors = true;
            app.Subcommands.Add(VerifyCmd);

            return VerifyCmd;
        }

        private static void RegisterOptionsForCommandVerify(CliCommand cmd, Func<ILogger> getLogger)
        {
            var packagePaths_Argument = new CliArgument<string[]>(name: "package-paths")
            {
                Arity = ArgumentArity.ZeroOrMore, // Validate if is ZeroOrMore
                Description = Strings.VerifyCommandPackagePathDescription,
            };
            cmd.Add(packagePaths_Argument);
            var all_Option = new CliOption<bool>(name: "--all")
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.VerifyCommandAllDescription,
            };
            cmd.Add(all_Option);
            var certificateFingerprints_Option = new CliOption<string[]>(name: "--certificate-fingerprint")
            {
                Arity = ArgumentArity.ZeroOrMore,
                Description = Strings.VerifyCommandCertificateFingerprintDescription,
            };
            cmd.Add(certificateFingerprints_Option);
            var verbosity_Option = new CliOption<string>(name: "--verbosity", aliases: new[] { "-v" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Verbosity_Description,
            };
            cmd.Add(verbosity_Option);
            var configfile_Option = new CliOption<string>(name: "--configfile")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_ConfigFile,
            };
            cmd.Add(configfile_Option);
            // Create handler delegate handler for cmd
            cmd.SetAction(async (parseResult, cancellationToken) =>
            {
                var packagePaths = parseResult.GetValue(packagePaths_Argument);
                var all = parseResult.GetValue(all_Option);
                var fingerPrints = parseResult.GetValue(certificateFingerprints_Option);
                var verbosity = parseResult.GetValue(verbosity_Option);
                var configFile = parseResult.GetValue(configfile_Option);

                ValidatePackagePaths(packagePaths, packagePaths_Argument.Name);

                VerifyArgs args = new VerifyArgs();
                args.PackagePaths = packagePaths;
                args.Verifications = all ?
                    new List<Verification>() { Verification.All } :
                    new List<Verification>() { Verification.Signatures };
                args.CertificateFingerprint = fingerPrints;
                args.Logger = getLogger();
                args.Settings = XPlatUtility.ProcessConfigFile(configFile);
                SetLogLevel(XPlatUtility.MSBuildVerbosityToNuGetLogLevel(verbosity));

                X509TrustStore.InitializeForDotNetSdk(args.Logger);

                var runner = GetCommandRunner();
                var verifyTask = runner.ExecuteCommandAsync(args);
                await verifyTask;

                return verifyTask.Result;
            });
        }

        internal static void Register(CommandLineApplication app,
                              Func<ILogger> getLogger,
                              Action<LogLevel> setLogLevel,
                              Func<IVerifyCommandRunner> getCommandRunner)
        {
            app.Command("verify", verifyCmd =>
            {
                CommandArgument packagePaths = verifyCmd.Argument(
                    "<package-paths>",
                    Strings.VerifyCommandPackagePathDescription,
                    multipleValues: true);

                CommandOption all = verifyCmd.Option(
                    "--all",
                    Strings.VerifyCommandAllDescription,
                    CommandOptionType.NoValue);

                CommandOption fingerPrint = verifyCmd.Option(
                    "--certificate-fingerprint",
                    Strings.VerifyCommandCertificateFingerprintDescription,
                    CommandOptionType.MultipleValue);

                CommandOption configFile = verifyCmd.Option(
                    "--configfile",
                    Strings.Option_ConfigFile,
                    CommandOptionType.SingleValue);

                CommandOption verbosity = verifyCmd.Option(
                    "-v|--verbosity",
                    Strings.Verbosity_Description,
                    CommandOptionType.SingleValue);

                verifyCmd.HelpOption(XPlatUtility.HelpOption);
                verifyCmd.Description = Strings.VerifyCommandDescription;

                verifyCmd.OnExecute(async () =>
                {
                    ValidatePackagePaths(packagePaths);

                    VerifyArgs args = new VerifyArgs();
                    args.PackagePaths = packagePaths.Values;
                    args.Verifications = all.HasValue() ?
                        new List<Verification>() { Verification.All } :
                        new List<Verification>() { Verification.Signatures };
                    args.CertificateFingerprint = fingerPrint.Values;
                    args.Logger = getLogger();
                    args.Settings = XPlatUtility.ProcessConfigFile(configFile.Value());
                    setLogLevel(XPlatUtility.MSBuildVerbosityToNuGetLogLevel(verbosity.Value()));

                    X509TrustStore.InitializeForDotNetSdk(args.Logger);

                    var runner = getCommandRunner();
                    var verifyTask = runner.ExecuteCommandAsync(args);
                    await verifyTask;

                    return verifyTask.Result;
                });
            });
        }

        private static void ValidatePackagePaths(CommandArgument argument)
        {
            if (argument.Values.Count == 0 ||
                argument.Values.Any<string>(packagePath => string.IsNullOrEmpty(packagePath)))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PkgMissingArgument,
                    "verify",
                    argument.Name));
            }
        }

        private static void ValidatePackagePaths(string[] argument, string argumentName)
        {
            if (argument.Length == 0 ||
                argument.Any<string>(packagePath => string.IsNullOrEmpty(packagePath)))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PkgMissingArgument,
                    "verify",
                    argumentName));
            }
        }
    }
}
