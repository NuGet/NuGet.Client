// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "verify", "VerifyCommandDescription",
        MinArgs = 1,
        MaxArgs = 1,
        UsageSummaryResourceName = "VerifyCommandUsageSummary",
        UsageExampleResourceName = "VerifyCommandUsageExamples")]
    public class VerifyCommand : Command
    {
        protected VerifyCommand() : base()
        {
            CertificateFingerprint = new List<string>();
        }

        [Option(typeof(NuGetCommand), "VerifyCommandCertificateFingerprintDescription")]
        public ICollection<string> CertificateFingerprint { get; set; }

        [Option(typeof(NuGetCommand), "VerifyCommandSignaturesDescription")]
        public bool Signatures { get; set; }

        public override Task ExecuteCommandAsync()
        {
            var PackagePath = Arguments[0];

            if (string.IsNullOrEmpty(PackagePath))
            {
                throw new ArgumentNullException(nameof(PackagePath));
            }

            var verifyArgs = new VerifyArgs()
            {
                Type = Signatures ? VerifyArgs.VerificationType.Signatures : VerifyArgs.VerificationType.Unknown,
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

            var verifyCommandRunner = new VerifyCommandRunner();
            var result = verifyCommandRunner.ExecuteCommandAsync(verifyArgs).Result;
            if (result > 0)
            {
                throw new ExitCodeException(1);
            }
            return Task.FromResult(result);
        }
    }
}
