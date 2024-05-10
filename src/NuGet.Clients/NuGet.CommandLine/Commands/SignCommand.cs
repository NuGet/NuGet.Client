// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "sign", "SignCommandDescription",
        MinArgs = 1,
        MaxArgs = 1,
        UsageSummaryResourceName = "SignCommandUsageSummary",
        UsageExampleResourceName = "SignCommandUsageExamples",
        UsageDescriptionResourceName = "SignCommandUsageDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class SignCommand : Command
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        // Default constructor used only for testing, since the Command Default Constructor is protected
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public SignCommand() : base()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
        }

        [Option(typeof(NuGetCommand), "SignCommandOutputDirectoryDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string OutputDirectory { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SignCommandCertificatePathDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string CertificatePath { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SignCommandCertificateStoreNameDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string CertificateStoreName { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SignCommandCertificateStoreLocationDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string CertificateStoreLocation { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SignCommandCertificateSubjectNameDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string CertificateSubjectName { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SignCommandCertificateFingerprintDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string CertificateFingerprint { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SignCommandCertificatePasswordDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string CertificatePassword { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SignCommandHashAlgorithmDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string HashAlgorithm { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SignCommandTimestamperDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Timestamper { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SignCommandTimestampHashAlgorithmDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string TimestampHashAlgorithm { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "SignCommandOverwriteDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool Overwrite { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override async Task ExecuteCommandAsync()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            var signArgs = GetSignArgs();
            var signCommandRunner = new SignCommandRunner();
            var result = await signCommandRunner.ExecuteCommandAsync(signArgs);

            if (result != 0)
            {
                throw new ExitCodeException(exitCode: result);
            }
        }

        /// <summary>
        /// Generates a SignArgs object from the arguments and options passed to the SignCommand object.
        /// </summary>
        /// <returns>SignArgs object containing the arguments and options passed to the SignCommand object.</returns>
        public SignArgs GetSignArgs()
        {
            ValidatePackagePath();
            WarnIfNoTimestamper(Console);
            ValidateCertificateInputs();
            ValidateOutputDirectory();

            var signingSpec = SigningSpecifications.V1;
            var storeLocation = ValidateAndParseStoreLocation();
            var storeName = ValidateAndParseStoreName();
            var hashAlgorithm = CommandLineUtility.ParseAndValidateHashAlgorithmFromArgument(HashAlgorithm, nameof(HashAlgorithm), signingSpec);
            var timestampHashAlgorithm = CommandLineUtility.ParseAndValidateHashAlgorithmFromArgument(TimestampHashAlgorithm, nameof(TimestampHashAlgorithm), signingSpec);

            return new SignArgs()
            {
                PackagePaths = new[] { Arguments[0] },
                OutputDirectory = OutputDirectory,
                CertificatePath = CertificatePath,
                CertificateStoreName = storeName,
                CertificateStoreLocation = storeLocation,
                CertificateSubjectName = CertificateSubjectName,
                CertificateFingerprint = CertificateFingerprint,
                CertificatePassword = CertificatePassword,
                SignatureHashAlgorithm = hashAlgorithm,
                Logger = Console,
                Overwrite = Overwrite,
                NonInteractive = NonInteractive,
                Timestamper = Timestamper,
                TimestampHashAlgorithm = timestampHashAlgorithm,
                PasswordProvider = new ConsolePasswordProvider(Console)
            };
        }

        private StoreName ValidateAndParseStoreName()
        {
            var storeName = StoreName.My;

            if (!string.IsNullOrEmpty(CertificateStoreName) &&
                !Enum.TryParse(CertificateStoreName, ignoreCase: true, result: out storeName))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    NuGetCommand.CommandInvalidArgumentException,
                    nameof(CertificateStoreName)));
            }

            return storeName;
        }

        private StoreLocation ValidateAndParseStoreLocation()
        {
            var storeLocation = StoreLocation.CurrentUser;

            if (!string.IsNullOrEmpty(CertificateStoreLocation) &&
                !Enum.TryParse(CertificateStoreLocation, ignoreCase: true, result: out storeLocation))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    NuGetCommand.CommandInvalidArgumentException,
                    nameof(CertificateStoreLocation)));
            }

            return storeLocation;
        }

        private void ValidatePackagePath()
        {
            // Assert mandatory argument
            if (Arguments.Count < 1 ||
                string.IsNullOrEmpty(Arguments[0]))
            {
                throw new ArgumentException(NuGetCommand.SignCommandNoPackageException);
            }
        }

        private void WarnIfNoTimestamper(ILogger logger)
        {
            if (string.IsNullOrEmpty(Timestamper))
            {
                logger.Log(LogMessage.CreateWarning(NuGetLogCode.NU3002, NuGetCommand.SignCommandNoTimestamperWarning));
            }
        }

        private void ValidateOutputDirectory()
        {
            if (!string.IsNullOrEmpty(OutputDirectory) &&
                            !Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }
        }

        private void ValidateCertificateInputs()
        {
            if (string.IsNullOrEmpty(CertificatePath) &&
                string.IsNullOrEmpty(CertificateFingerprint) &&
                string.IsNullOrEmpty(CertificateSubjectName))
            {
                // Throw if user gave no certificate input
                throw new ArgumentException(NuGetCommand.SignCommandNoCertificateException);
            }
            else if (!string.IsNullOrEmpty(CertificatePath) &&
                (!string.IsNullOrEmpty(CertificateFingerprint) ||
                 !string.IsNullOrEmpty(CertificateSubjectName) ||
                 !string.IsNullOrEmpty(CertificateStoreLocation) ||
                 !string.IsNullOrEmpty(CertificateStoreName)))
            {
                // Thow if the user provided a path and any one of the other options
                throw new ArgumentException(NuGetCommand.SignCommandMultipleCertificateException);
            }
            else if (!string.IsNullOrEmpty(CertificateFingerprint) && !string.IsNullOrEmpty(CertificateSubjectName))
            {
                // Thow if the user provided a fingerprint and a subject
                throw new ArgumentException(NuGetCommand.SignCommandMultipleCertificateException);
            }
        }
    }
}
