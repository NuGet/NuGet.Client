// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CommandLine.XPlat.Commands;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging.Signing;
using static NuGet.Commands.VerifyArgs;

namespace NuGet.CommandLine.XPlat
{
    internal static class VerifyCommand
    {
        // Registers a placeholder on the legacy CommandLineApplication so that `dotnet nuget --help`
        // still lists `verify`. The command is implemented with System.CommandLine (see overload below).
        internal static void Register(CommandLineApplication app)
        {
            app.Command("verify", verifyCmd =>
            {
                verifyCmd.Description = Strings.VerifyCommandDescription;
            });
        }

        internal static void Register(Command rootCommand,
                              Func<ILogger> getLogger,
                              Action<LogLevel> setLogLevel,
                              Func<IVerifyCommandRunner> getCommandRunner)
        {
            var verifyCommand = new DocumentedCommand("verify", Strings.VerifyCommandDescription, "https://aka.ms/dotnet/nuget/verify");

            var packagePaths = new Argument<List<string>>("package-paths")
            {
                Description = Strings.VerifyCommandPackagePathDescription,
                Arity = ArgumentArity.ZeroOrMore,
            };

            var all = new Option<bool>("--all")
            {
                Description = Strings.VerifyCommandAllDescription,
                Arity = ArgumentArity.Zero,
            };

            var fingerPrint = new Option<List<string>>("--certificate-fingerprint")
            {
                Description = Strings.VerifyCommandCertificateFingerprintDescription,
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = true,
            };

            var configFile = new Option<string>("--configfile")
            {
                Description = Strings.Option_ConfigFile,
                Arity = ArgumentArity.ExactlyOne,
            };

            var verbosity = new Option<string>("--verbosity", "-v")
            {
                Description = Strings.Verbosity_Description,
                Arity = ArgumentArity.ExactlyOne,
            };

            var forceEnglishOutput = new Option<bool>(CommandConstants.ForceEnglishOutputOption)
            {
                Description = Strings.ForceEnglishOutput_Description,
                Arity = ArgumentArity.Zero,
            };

            var help = new HelpOption()
            {
                Arity = ArgumentArity.Zero,
            };

            verifyCommand.Arguments.Add(packagePaths);
            verifyCommand.Options.Add(all);
            verifyCommand.Options.Add(fingerPrint);
            verifyCommand.Options.Add(configFile);
            verifyCommand.Options.Add(verbosity);
            verifyCommand.Options.Add(forceEnglishOutput);
            verifyCommand.Options.Add(help);

            verifyCommand.SetAction(async (parseResult, cancellationToken) =>
            {
                List<string>? packagePathsValue = parseResult.GetValue(packagePaths);

                ValidatePackagePaths(packagePathsValue, "<package-paths>");

                VerifyArgs args = new VerifyArgs();
                args.PackagePaths = packagePathsValue!;
                args.Verifications = parseResult.GetValue(all) ?
                    new List<Verification>() { Verification.All } :
                    new List<Verification>() { Verification.Signatures };
                args.CertificateFingerprint = parseResult.GetValue(fingerPrint);
                args.Logger = getLogger();
                args.Settings = XPlatUtility.ProcessConfigFile(parseResult.GetValue(configFile));
                setLogLevel(XPlatUtility.MSBuildVerbosityToNuGetLogLevel(parseResult.GetValue(verbosity)));

                X509TrustStore.InitializeForDotNetSdk(args.Logger);

                var runner = getCommandRunner();
                return await runner.ExecuteCommandAsync(args);
            });

            rootCommand.Subcommands.Add(verifyCommand);
        }

        private static void ValidatePackagePaths(List<string>? packagePaths, string argumentName)
        {
            if (packagePaths == null ||
                packagePaths.Count == 0 ||
                packagePaths.Any<string>(packagePath => string.IsNullOrEmpty(packagePath)))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PkgMissingArgument,
                    "verify",
                    argumentName));
            }
        }
    }
}
