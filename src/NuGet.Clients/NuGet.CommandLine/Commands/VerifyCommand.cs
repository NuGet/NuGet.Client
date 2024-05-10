// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class VerifyCommand : Command
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected VerifyCommand() : base()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            CertificateFingerprint = new List<string>();
        }

        [Option(typeof(NuGetCommand), "VerifyCommandCertificateFingerprintDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public ICollection<string> CertificateFingerprint { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "VerifyCommandSignaturesDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool Signatures { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "VerifyCommandAllDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool All { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override Task ExecuteCommandAsync()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            var PackagePath = Arguments[0];

            if (string.IsNullOrEmpty(PackagePath))
            {
                throw new ArgumentException(nameof(PackagePath));
            }

            var verifyArgs = new VerifyArgs()
            {
                Verifications = GetVerificationTypes(),
                PackagePaths = new[] { PackagePath },
                CertificateFingerprint = CertificateFingerprint,
                Logger = Console,
                Settings = Settings
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

            var verifyCommandRunner = new VerifyCommandRunner();
            var result = verifyCommandRunner.ExecuteCommandAsync(verifyArgs).Result;
            if (result > 0)
            {
                throw new ExitCodeException(1);
            }
            return Task.CompletedTask;
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

            return verifications;
        }
    }
}
