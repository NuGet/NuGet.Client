// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading.Tasks;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "sign", "SignCommandDescription", MinArgs = 2, MaxArgs = 9,
        UsageSummaryResourceName = "SignCommandSummary",
        UsageExampleResourceName = "SignCommandExamples")]
    public class SignCommand : Command
    {
        [Option(typeof(NuGetCommand), "SignCommandPackagePathDescription")]
        public string PackagePath { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandOutputDirectoryDescription")]
        public string OutputDirectory { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCertificatePathDescription")]
        public string CertificatePath { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCertificateSubjectNameDescription")]
        public string CertificateSubjectName { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCertificateSubjectFingerprintDescription")]
        public string CertificateFingerprint { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCertificatePassphraseDescription")]
        public string CertificatePassphrase { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandCryptographicServiceProviderDescription")]
        public string CryptographicServiceProvider { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandKeyContainerDescription")]
        public string KeyContainer { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandHashingAlgorithmDescription")]
        public string HashingAlgorithm { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandRSASignaturePaddingDescription")]
        public string RSASignaturePadding { get; set; }

        [Option(typeof(NuGetCommand), "SignCommandForceDescription")]
        public bool Force { get; set; }

        public SignCommandRunner SignCommandRunner { get; set; }

        public override Task ExecuteCommandAsync()
        {
            if (string.IsNullOrEmpty(PackagePath))
            {
                throw new ArgumentException("No package provided for signing");
            }

            var signArgs = new SignArgs();

            var signCommandRunner = new SignCommandRunner();

            var result = signCommandRunner.ExecuteCommand(signArgs);

            return Task.FromResult(result);
        }
    }
}
