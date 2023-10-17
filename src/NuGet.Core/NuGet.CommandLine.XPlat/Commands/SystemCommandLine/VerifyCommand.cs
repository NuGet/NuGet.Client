// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging.Signing;
using static NuGet.Commands.VerifyArgs;

namespace NuGet.CommandLine.XPlat
{
    internal static class VerifyCommandSystemCommandLine
    {
        internal static Func<Exception, int> CommandExceptionHandler;
        internal static Func<ILogger> GetLoggerFunction;

        internal static Command Register(Command app, Func<ILogger> getLogger, Action<LogLevel> setLogLevel, Func<IVerifyCommandRunner> getCommandRunner)
        {
            var verifyCmd = new Command(name: "verify", description: Strings.VerifyCommandDescription);

            // Options under sub-command: update source
            RegisterOptionsForCommandUpdateSource(verifyCmd, getLogger, setLogLevel, getCommandRunner);

            GetLoggerFunction = getLogger;
            //CommandExceptionHandler = commandExceptionHandler;
            app.AddCommand(verifyCmd);

            return verifyCmd;
        } // End noun method

        private static void RegisterOptionsForCommandUpdateSource(Command cmd, Func<ILogger> getLogger, Action<LogLevel> setLogLevel, Func<IVerifyCommandRunner> getCommandRunner)
        {
            var package_Paths = new Argument<IEnumerable<string>>(name: "package-paths", description: Strings.VerifyCommandPackagePathDescription, parse: result =>
            {
                var packagePaths = result.Tokens.Where(t => t.Type.Equals(TokenType.Argument)).Select(path => path.Value);
                ValidatePackagePaths(packagePaths);
                return packagePaths;
            })
            {
                Arity = ArgumentArity.OneOrMore,
            };
            cmd.Add(package_Paths);

            var configfile_Option = new Option<string>(name: "--configfile", description: Strings.Option_ConfigFile)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            cmd.Add(configfile_Option);

            var all_Option = new Option<bool>(name: "--all", description: Strings.VerifyCommandAllDescription)
            {
                Arity = ArgumentArity.Zero,
            };
            cmd.Add(all_Option);

            var certificate_Fingerprint = new Option<string[]>(name: "--certificate-fingerprint", description: Strings.VerifyCommandCertificateFingerprintDescription)
            {
                Arity = ArgumentArity.ZeroOrMore,
            };
            cmd.Add(certificate_Fingerprint);
            // Create handler delegate handler for cmd
            cmd.SetHandler((args) =>
            {
                int verifyTask;
                VerifyArgs verifyArgs = (VerifyArgs)args;
                try
                {
                    ValidatePackagePaths(verifyArgs.PackagePaths);

                    setLogLevel(XPlatUtility.MSBuildVerbosityToNuGetLogLevel("N"));

                    X509TrustStore.InitializeForDotNetSdk(verifyArgs.Logger);

                    var runner = getCommandRunner();
                    verifyTask = runner.ExecuteCommandAsync(verifyArgs).Result;
                }
                catch (Exception e)
                {
                    // Log the error
                    if (ExceptionLogger.Instance.ShowStack)
                    {
                        verifyArgs.Logger.LogError(e.ToString());
                    }
                    else
                    {
                        verifyArgs.Logger.LogError(ExceptionUtilities.DisplayMessage(e));
                    }
                    verifyTask = 1;
                }
                return Task.FromResult(verifyTask);
            }, new VerifyCustomBinder(package_Paths, configfile_Option, all_Option, certificate_Fingerprint, getLogger));
        }

        internal partial class VerifyCustomBinder : BinderBase<VerifyArgs>
        {
            private readonly Argument<IEnumerable<string>> _packagePaths;
            private readonly Option<string> _configfile;
            private readonly Option<bool> _all;
            private readonly Option<string[]> _certificates;
            private readonly Func<ILogger> _getLogger;

            public VerifyCustomBinder(Argument<IEnumerable<string>> packagePaths, Option<string> configfile, Option<bool> all, Option<string[]> certificates, Func<ILogger> getLogger)
            {
                _packagePaths = packagePaths;
                _configfile = configfile;
                _all = all;
                _certificates = certificates;
                _getLogger = getLogger;
            }

            protected override VerifyArgs GetBoundValue(BindingContext bindingContext)
            {
                var returnValue = new VerifyArgs()
                {
                    PackagePaths = bindingContext.ParseResult.GetValueForArgument(_packagePaths).ToList(),
                    Settings = XPlatUtility.ProcessConfigFile(bindingContext.ParseResult.GetValueForOption(_configfile)),
                    CertificateFingerprint = bindingContext.ParseResult.GetValueForOption(_certificates),
                    Verifications = bindingContext.ParseResult.GetValueForOption(_all) ?
                        new List<Verification>() { Verification.All } :
                        new List<Verification>() { Verification.Signatures },
                    Logger = _getLogger()
                };

                return returnValue;
            }
        }

        private static void ValidatePackagePaths(IEnumerable<string> argument)
        {
            if (!argument.Any() ||
                argument.Any<string>(packagePath => string.IsNullOrEmpty(packagePath)))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PkgMissingArgument,
                    "verify",
                    "package paths"));
            }
        }
    }
}
