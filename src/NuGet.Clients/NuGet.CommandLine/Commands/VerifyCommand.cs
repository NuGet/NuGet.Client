// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Commands;
using static NuGet.Commands.VerifyArgs;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "verify", "VerifyCommandDescription",
        MinArgs = 1,
        MaxArgs = 1,
        UsageSummaryResourceName = "VerifyCommandUsageSummary",
        UsageExampleResourceName = "VerifyCommandUsageExamples")]
    public class VerifyCommand : Command
    {
        internal VerifyCommand() : base()
        {
            CertificateFingerprint = new List<string>();
        }

        [Option(typeof(NuGetCommand), "VerifyCommandCertificateFingerprintDescription")]
        public ICollection<string> CertificateFingerprint { get; set; }

        [Option(typeof(NuGetCommand), "VerifyCommandSignaturesDescription")]
        public bool Signatures { get; set; }

        [Option(typeof(NuGetCommand), "VerifyCommandAllDescription")]
        public bool All { get; set; }

        internal IVerifyCommandRunner VerifyCommandRunner { get; set; }

        public override Task ExecuteCommandAsync()
        {
            var PackagePath = Arguments[0];

            if (string.IsNullOrEmpty(PackagePath))
            {
                throw new ArgumentNullException(nameof(PackagePath));
            }

            var verifyArgs = new VerifyArgs()
            {
                Verifications = GetVerificationTypes(),
                PackagePath = PackagePath,
                CertificateFingerprint = CertificateFingerprint,
                Logger = Console
            };

            switch (Verbosity)
            {
                case Verbosity.Detailed:
                    verifyArgs.LogLevel = Common.LogLevel.Verbose;
                    break;
                case Verbosity.Normal:
                    verifyArgs.LogLevel = Common.LogLevel.Information;
                    break;
                case Verbosity.Quiet:
                    verifyArgs.LogLevel = Common.LogLevel.Minimal;
                    break;
            }

            if (VerifyCommandRunner == null)
            {
                VerifyCommandRunner = new VerifyCommandRunner();
            }

            var result = VerifyCommandRunner.ExecuteCommandAsync(verifyArgs).Result;
            if (result > 0)
            {
                throw new ExitCodeException(1);
            }
            return Task.FromResult(result);
        }

        private IList<Verification> GetVerificationTypes()
        {
            if (All)
            {
                return new[] { Verification.All };
            }

            var verifications = new List<Verification>();

            if (Signatures)
            {
                verifications.Add(Verification.Signatures);
            }

            if (!verifications.Any())
            {
                verifications.Add(Verification.All);
            }

            return verifications;
        }
    }
}
