// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Packaging.Signing;
using NuGet.Shared;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "sign", "SignCommandDescription",
        MinArgs = 1,
        MaxArgs = 1,
        UsageSummaryResourceName = "SignCommandUsageSummary",
        UsageExampleResourceName = "SignCommandUsageExamples",
        UsageDescriptionResourceName = "SignCommandUsageDescription")]
    public class SignCommand : Command
    {
        // Default constructor used only for testing, since the Command Default Constructor is protected
        public SignCommand() : base()
        {
        }

        public ISignCommandRunner SignCommandRunner { get; set; }

        // List of possible values - https://github.com/Microsoft/referencesource/blob/master/mscorlib/system/security/cryptography/HashAlgorithmName.cs
        private static string[] _acceptedHashAlgorithms = { "SHA256", "SHA384", "SHA512" };

        [Option(typeof(NuGetCommand), "SignCommandOutputDirectoryDescription")]
        public string OutputDirectory { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCertificatePathDescription")]
        public string CertificatePath { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCertificateStoreNameDescription")]
        public string CertificateStoreName { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCertificateStoreLocationDescription")]
        public string CertificateStoreLocation { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCertificateSubjectNameDescription")]
        public string CertificateSubjectName { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCertificateFingerprintDescription")]
        public string CertificateFingerprint { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCertificatePasswordDescription")]
        public string CertificatePassword { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCryptographicServiceProviderDescription")]
        public string CryptographicServiceProvider { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandKeyContainerDescription")]
        public string KeyContainer { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandHashAlgorithmDescription")]
        public string HashAlgorithm { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandTimestamperDescription")]
        public string Timestamper { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandTimestampHashAlgorithmDescription")]
        public string TimestampHashAlgorithm { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandOverwriteDescription")]
        public bool Overwrite { get; set; }

        public override Task ExecuteCommandAsync()
        {
            var storeName = StoreName.My;
            var storeLocation = StoreLocation.CurrentUser;
            var hashAlgorithm = HashAlgorithmName.SHA256;
            var timestampHashAlgorithm = HashAlgorithmName.SHA256;
            

            // Assert mandatory argument
            if (Arguments.Count < 1 ||
                string.IsNullOrEmpty(Arguments[0]))
            {
                throw new ArgumentException(NuGetCommand.SignCommandNoPackageException);
            }

            var packagePath = Arguments[0];

            // Assert options
            if (string.IsNullOrEmpty(Timestamper))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                    NuGetCommand.SignCommandNoArgumentException,
                    nameof(Timestamper)));
            }
            else if (string.IsNullOrEmpty(CertificatePath) &&
                string.IsNullOrEmpty(CertificateFingerprint) &&
                string.IsNullOrEmpty(CertificateSubjectName))
            {
                throw new ArgumentException(NuGetCommand.SignCommandNoCertificateException);
            }
            else if (!string.IsNullOrEmpty(CertificatePath) &&
                ((!string.IsNullOrEmpty(CertificateFingerprint) ||
                 !string.IsNullOrEmpty(CertificateSubjectName)) ||
                 !string.IsNullOrEmpty(CertificateStoreLocation) ||
                 !string.IsNullOrEmpty(CertificateStoreName)))
            {
                // Thow if the user provided a path and any one of the other options
                throw new ArgumentException(NuGetCommand.SignCommandMultipleCertificateException);
            }
            else if (!string.IsNullOrEmpty(CertificateFingerprint) && !string.IsNullOrEmpty(CertificateSubjectName))
            {
                // Thow if the user provided a path and any one of the other options
                throw new ArgumentException(NuGetCommand.SignCommandMultipleCertificateException);
            }
            else if (!string.IsNullOrEmpty(CertificateStoreLocation) &&
                !Enum.TryParse(CertificateStoreLocation, ignoreCase: true, result: out storeLocation))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                    NuGetCommand.SignCommandInvalidArgumentException,
                    nameof(CertificateStoreLocation)));
            }
            else if (!string.IsNullOrEmpty(CertificateStoreName) &&
                !Enum.TryParse(CertificateStoreName, ignoreCase: true, result: out storeName))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                    NuGetCommand.SignCommandInvalidArgumentException,
                    nameof(CertificateStoreName)));
            }
            else if (!string.IsNullOrEmpty(HashAlgorithm))
            {
                if (!_acceptedHashAlgorithms.Contains(HashAlgorithm, StringComparer.InvariantCultureIgnoreCase) ||
                    !Enum.TryParse(HashAlgorithm, ignoreCase: true, result: out hashAlgorithm))
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                        NuGetCommand.SignCommandInvalidArgumentException,
                        nameof(HashAlgorithm)));
                }
            }
            else if (!string.IsNullOrEmpty(TimestampHashAlgorithm))
            {
                if (!_acceptedHashAlgorithms.Contains(TimestampHashAlgorithm, StringComparer.InvariantCultureIgnoreCase) ||
                    !Enum.TryParse(TimestampHashAlgorithm, ignoreCase: true, result: out timestampHashAlgorithm))
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                        NuGetCommand.SignCommandInvalidArgumentException,
                        nameof(TimestampHashAlgorithm)));
                }
            }
            else if (!string.IsNullOrEmpty(OutputDirectory) &&
                !Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }

            var signArgs = new SignArgs()
            {
                PackagePath = packagePath,
                OutputDirectory = OutputDirectory,
                CertificatePath = CertificatePath,
                CertificateStoreName = storeName,
                CertificateStoreLocation = storeLocation,
                CertificateSubjectName = CertificateSubjectName,
                CertificateFingerprint = CertificateFingerprint,
                CertificatePassword = CertificatePassword,
                CryptographicServiceProvider = CryptographicServiceProvider,
                KeyContainer = KeyContainer,
                HashingAlgorithm = hashAlgorithm,
                Logger = Console,
                Overwrite = Overwrite,
                NonInteractive = NonInteractive,
                Timestamper = Timestamper,
                TimestampHashAlgorithm = timestampHashAlgorithm
            };

            if (SignCommandRunner == null)
            {
                SignCommandRunner = new SignCommandRunner();
            }

            var result = SignCommandRunner.ExecuteCommand(signArgs);

            return Task.FromResult(result);
        }
    }
}
