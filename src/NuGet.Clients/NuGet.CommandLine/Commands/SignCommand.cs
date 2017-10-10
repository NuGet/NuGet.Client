// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using NuGet.Commands;

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
        public SecureString CertificatePassword { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCryptographicServiceProviderDescription")]
        public string CryptographicServiceProvider { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandKeyContainerDescription")]
        public string KeyContainer { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandHashAlgorithmDescription")]
        public string HashingAlgorithm { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandTimestamperDescription")]
        public string Timestamper { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandTimestampHashAlgorithmDescription")]
        public string TimestampHashAlgorithm { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandOverwriteDescription")]
        public bool Overwrite { get; set; }

        public SignCommandRunner SignCommandRunner { get; set; }

        public override Task ExecuteCommandAsync()
        {
            if (string.IsNullOrEmpty(Arguments[0]))
            {
                throw new ArgumentException("No package provided for signing");
            }

            var signArgs = new SignArgs()
            {
                PackagePath = Arguments[0],
                OutputDirectory = OutputDirectory,
                CertificatePath = CertificatePath,
                CertificateStoreName = string.IsNullOrEmpty(CertificateStoreName) ? "My" : CertificateStoreName,
                CertificateStoreLocation = string.IsNullOrEmpty(CertificateStoreLocation) ? "CurrentUser" : CertificateStoreLocation,
                CertificateSubjectName = CertificateSubjectName,
                CertificateFingerprint = CertificateFingerprint,
                CertificatePassword = CertificatePassword,
                CryptographicServiceProvider = CryptographicServiceProvider,
                KeyContainer = KeyContainer,
                HashingAlgorithm = string.IsNullOrEmpty(HashingAlgorithm) ? "SHA256" : HashingAlgorithm,
                Logger = Console,
                Overwrite = Overwrite,
                NonInteractive = NonInteractive,
                Timestamper = Timestamper,
                TimestampHashAlgorithm = string.IsNullOrEmpty(TimestampHashAlgorithm) ? "SHA256" : TimestampHashAlgorithm
            };

            var signCommandRunner = new SignCommandRunner();

            var result = signCommandRunner.ExecuteCommand(signArgs);

            return Task.FromResult(result);
        }
    }
}
