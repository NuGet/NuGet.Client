// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using static NuGet.Commands.VerifyArgs;

namespace NuGet.CommandLine.XPlat
{
    internal partial class VerifyCommand
    {
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

                CommandOption verbosity = verifyCmd.Option(
                    "-v|--verbosity",
                    Strings.Verbosity_Description,
                    CommandOptionType.SingleValue);

                verifyCmd.HelpOption("-h|--help");
                verifyCmd.Description = Strings.VerifyCommandDescription;

                verifyCmd.OnExecute(async () =>
                {
                    ValidatePackagesPath(packagePaths);

                    VerifyArgs args = new VerifyArgs();
#if IS_DESKTOP
                    args.PackagePath = packagePaths.Value;
#else
                    args.PackagePaths = packagePaths.Values;
#endif
                    args.Verifications = all.HasValue() ?
                        new List<Verification>() { Verification.All } :
                        new List<Verification>() { Verification.Signatures };
                    args.CertificateFingerprint = fingerPrint.Values;
                    args.Logger = getLogger();
                    setLogLevel(XPlatUtility.MSBuildVerbosityToNuGetLogLevel(verbosity.Value()));

                    var runner = getCommandRunner();
                    var verifyTask = runner.ExecuteCommandAsync(args);
                    await verifyTask;

                    return verifyTask.Result;
                });
            });
        }
        private static void ValidatePackagesPath(CommandArgument argument)
        {
            if (argument.Values.Count == 0 ||
                argument.Values.Any<string>(packagePath => string.IsNullOrEmpty(packagePath)))
            {
                throw new ArgumentNullException(argument.Name);
            }
        }
    }
}
