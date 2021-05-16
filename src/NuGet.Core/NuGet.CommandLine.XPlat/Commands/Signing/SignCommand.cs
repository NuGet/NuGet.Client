// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace NuGet.CommandLine.XPlat
{
    internal static class SignCommand
    {
        internal static void Register(CommandLineApplication app,
                         Func<ILogger> getLogger,
                         Action<LogLevel> setLogLevel,
                         Func<ISignCommandRunner> getCommandRunner)
        {
            app.Command("sign", signCmd =>
            {
                CommandArgument packagePaths = signCmd.Argument(
                    "<packages-path>",
                    Strings.SignCommandPackagePathDescription,
                    multipleValues: true);

                CommandOption outputDirectory = signCmd.Option(
                    "-o|--output",
                    Strings.SignCommandOutputDirectoryDescription,
                    CommandOptionType.SingleValue);

                CommandOption path = signCmd.Option(
                    "--certificate-path",
                    Strings.SignCommandCertificatePathDescription,
                    CommandOptionType.SingleValue);

                CommandOption store = signCmd.Option(
                    "--certificate-store-name",
                    Strings.SignCommandCertificateStoreNameDescription,
                    CommandOptionType.SingleValue);

                CommandOption location = signCmd.Option(
                    "--certificate-store-location",
                    Strings.SignCommandCertificateStoreLocationDescription,
                    CommandOptionType.SingleValue);

                CommandOption subject = signCmd.Option(
                    "--certificate-subject-name",
                    Strings.SignCommandCertificateSubjectNameDescription,
                    CommandOptionType.SingleValue);

                CommandOption fingerPrint = signCmd.Option(
                    "--certificate-fingerprint",
                    Strings.SignCommandCertificateFingerprintDescription,
                    CommandOptionType.SingleValue);

                CommandOption password = signCmd.Option(
                    "--certificate-password",
                    Strings.SignCommandCertificatePasswordDescription,
                    CommandOptionType.SingleValue);

                CommandOption algorithm = signCmd.Option(
                    "--hash-algorithm",
                    Strings.SignCommandHashAlgorithmDescription,
                    CommandOptionType.SingleValue);

                CommandOption timestamper = signCmd.Option(
                    "--timestamper",
                    Strings.SignCommandTimestamperDescription,
                    CommandOptionType.SingleValue);

                CommandOption timestamperAlgorithm = signCmd.Option(
                    "--timestamp-hash-algorithm",
                    Strings.SignCommandTimestampHashAlgorithmDescription,
                    CommandOptionType.SingleValue);

                CommandOption overwrite = signCmd.Option(
                    "--overwrite",
                    Strings.SignCommandOverwriteDescription,
                    CommandOptionType.NoValue);

                CommandOption verbosity = signCmd.Option(
                    "-v|--verbosity",
                    Strings.Verbosity_Description,
                    CommandOptionType.SingleValue);

                CommandOption interactive = signCmd.Option(
                    "--interactive",
                    Strings.Verbosity_Description,
                    CommandOptionType.NoValue);

                signCmd.HelpOption(XPlatUtility.HelpOption);

                signCmd.Description = Strings.SignCommandDescription;

                signCmd.OnExecute(async () =>
                {
                    var logger = getLogger();

                    ValidatePackagePaths(packagePaths);
                    WarnIfNoTimestamper(logger, timestamper);
                    ValidateCertificateInputs(path, fingerPrint, subject, store, location);
                    ValidateAndCreateOutputDirectory(outputDirectory);

                    var signingSpec = SigningSpecifications.V1;
                    var storeLocation = ValidateAndParseStoreLocation(location);
                    var storeName = ValidateAndParseStoreName(store);
                    var hashAlgorithm = CommandLineUtility.ParseAndValidateHashAlgorithm(algorithm.Value(), algorithm.LongName, signingSpec);
                    var timestampHashAlgorithm = CommandLineUtility.ParseAndValidateHashAlgorithm(timestamperAlgorithm.Value(), timestamperAlgorithm.LongName, signingSpec);

                    var args = new SignArgs()
                    {
                        PackagePaths = packagePaths.Values,
                        OutputDirectory = outputDirectory.Value(),
                        CertificatePath = path.Value(),
                        CertificateStoreName = storeName,
                        CertificateStoreLocation = storeLocation,
                        CertificateSubjectName = subject.Value(),
                        CertificateFingerprint = fingerPrint.Value(),
                        CertificatePassword = password.Value(),
                        SignatureHashAlgorithm = hashAlgorithm,
                        Logger = logger,
                        Overwrite = overwrite.HasValue(),
                        NonInteractive = !interactive.HasValue(),
                        Timestamper = timestamper.Value(),
                        TimestampHashAlgorithm = timestampHashAlgorithm
                    };

                    if (verbosity.HasValue())
                    {
                        setLogLevel(XPlatUtility.MSBuildVerbosityToNuGetLogLevel(verbosity.Value()));
                    }

                    var runner = getCommandRunner();
                    int result = await runner.ExecuteCommandAsync(args);
                    return result;
                });
            });
        }

        private static void ValidatePackagePaths(CommandArgument argument)
        {
            if (argument.Values.Count == 0)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PkgMissingArgument,
                    "sign",
                    argument.Name));
            }
        }

        private static void WarnIfNoTimestamper(ILogger logger, CommandOption timeStamper)
        {
            if (timeStamper.HasValue() && string.IsNullOrEmpty(timeStamper.Value()))
            {
                logger.Log(LogMessage.CreateWarning(NuGetLogCode.NU3002, Strings.SignCommandNoTimestamperWarning));
            }
        }

        private static void ValidateAndCreateOutputDirectory(CommandOption output)
        {
            if (output.HasValue())
            {
                string outputDir = output.Value();

                if (!!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
            }
        }

        private static StoreLocation ValidateAndParseStoreLocation(CommandOption location)
        {
            var storeLocation = StoreLocation.CurrentUser;

            if (location.HasValue())
            {
                if (!string.IsNullOrEmpty(location.Value()) &&
                    !Enum.TryParse(location.Value(), ignoreCase: true, result: out storeLocation))
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                        Strings.Err_InvalidValue,
                        nameof(location.LongName), string.Join(",", Enum.GetValues(typeof(StoreLocation)))));
                }
            }

            return storeLocation;
        }

        private static StoreName ValidateAndParseStoreName(CommandOption store)
        {
            var storeName = StoreName.My;

            if (store.HasValue())
            {
                if (!string.IsNullOrEmpty(store.Value()) &&
                    !Enum.TryParse(store.Value(), ignoreCase: true, result: out storeName))
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                        Strings.Err_InvalidValue,
                        nameof(store.LongName), string.Join(",", Enum.GetValues(typeof(StoreName)))));
                }
            }

            return storeName;
        }

        private static void ValidateCertificateInputs(CommandOption path, CommandOption fingerPrint,
                                                      CommandOption subject, CommandOption store, CommandOption location)
        {
            if (string.IsNullOrEmpty(path.Value()) &&
                string.IsNullOrEmpty(fingerPrint.Value()) &&
                string.IsNullOrEmpty(subject.Value()))
            {
                // Throw if user gave no certificate input
                throw new ArgumentException(Strings.SignCommandNoCertificateException);
            }
            else if (!string.IsNullOrEmpty(path.Value()) &&
                ((!string.IsNullOrEmpty(fingerPrint.Value()) ||
                 !string.IsNullOrEmpty(subject.Value())) ||
                 !string.IsNullOrEmpty(location.Value()) ||
                 !string.IsNullOrEmpty(store.Value())))
            {
                // Thow if the user provided a path and any one of the other options
                throw new ArgumentException(Strings.SignCommandMultipleCertificateException);
            }
            else if (!string.IsNullOrEmpty(fingerPrint.Value()) && !string.IsNullOrEmpty(subject.Value()))
            {
                // Thow if the user provided a fingerprint and a subject
                throw new ArgumentException(Strings.SignCommandMultipleCertificateException);
            }
        }
    }
}
