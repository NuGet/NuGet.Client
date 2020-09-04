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

namespace NuGet.CommandLine.XPlat
{
    internal partial class VerifyCommand
    {
        internal static void Register(CommandLineApplication app,
                              Func<ILogger> getLogger,
                              Action<LogLevel> setLogLevel)
        {
            app.Command("verify", verifyCmd =>
            {
                CommandArgument packagePath = verifyCmd.Argument(
                    "PackagePath",
                    Strings.VerifyCommandDescription,
                    multipleValues: true);

                CommandOption all = verifyCmd.Option(
                    "--all",
                    Strings.VerifyCommandAllDescription,
                    CommandOptionType.NoValue);

                CommandOption fingerPrint = verifyCmd.Option(
                    "--certificate-fingerprint",
                    Strings.VerifyCommandCertificateFingerprintDescription,
                    CommandOptionType.MultipleValue);

                CommandOption interactive = verifyCmd.Option(
                    "--interactive",
                    Strings.NuGetXplatCommand_Interactive,
                    CommandOptionType.NoValue);

                CommandOption configFile = verifyCmd.Option(
                    "--configfile",
                    Strings.Option_ConfigFile,
                CommandOptionType.SingleValue);

                CommandOption verbosity = verifyCmd.Option(
                    "-v|--verbosity",
                    Strings.Verbosity_Description,
                    CommandOptionType.SingleValue);

                verifyCmd.HelpOption("-h|--help");
                verifyCmd.Description = Strings.VerifyCommandDescription;

                verifyCmd.OnExecute(async () =>
                {
                    VerifyArgs args = new VerifyArgs();
                    args.PackagePath = packagePath.Value;
                    args.CertificateFingerprint = fingerPrint.Values;
                    args.Verifications = all.HasValue() ?
                        new List<VerifyArgs.Verification>() { VerifyArgs.Verification.All } :
                        new List<VerifyArgs.Verification>() { VerifyArgs.Verification.Signatures };
                    args.Logger = getLogger();
                    setLogLevel(XPlatUtility.MSBuildVerbosityToNuGetLogLevel(verbosity.Value()));                     

                    VerifyCommandRunner runner = new VerifyCommandRunner();
                    await runner.ExecuteCommandAsync(args);

                    return 0;
                });
            });
        }
    }
}
