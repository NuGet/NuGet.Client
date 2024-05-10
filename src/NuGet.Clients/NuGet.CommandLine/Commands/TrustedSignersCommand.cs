// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging.Signing;
using static NuGet.Commands.TrustedSignersArgs;

namespace NuGet.CommandLine.Commands
{
    [Command(typeof(NuGetCommand), "trusted-signers", "TrustedSignersCommandDescription",
        MinArgs = 0,
        MaxArgs = 2,
        UsageSummaryResourceName = "TrustedSignersCommandUsageSummary",
        UsageExampleResourceName = "TrustedSignersCommandUsageExamples")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class TrustedSignersCommand : Command
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        [Option(typeof(NuGetCommand), "TrustedSignersCommandNameDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Name { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "TrustedSignersCommandServiceIndexDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string ServiceIndex { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "TrustedSignersCommandCertificateFingerprintDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string CertificateFingerprint { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "TrustedSignersCommandFingerprintAlgorithmDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string FingerprintAlgorithm { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "TrustedSignersCommandAllowUntrustedRootDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool AllowUntrustedRoot { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "TrustedSignersCommandAuthorDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool Author { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "TrustedSignersCommandRepositoryDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool Repository { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "TrustedSignersCommandOwnersDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public ICollection<string> Owners { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        internal ITrustedSignersCommandRunner TrustedSignersCommandRunner { get; set; }

        internal TrustedSignersCommand() : base()
        {
            Owners = new List<string>();
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override async Task ExecuteCommandAsync()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            var actionString = Arguments.FirstOrDefault();

            TrustedSignersAction action;
            if (string.IsNullOrEmpty(actionString))
            {
                action = TrustedSignersAction.List;
            }
            else if (!Enum.TryParse(actionString, ignoreCase: true, result: out action))
            {
                throw new CommandLineArgumentCombinationException(string.Format(CultureInfo.CurrentCulture, NuGetResources.Error_UnknownAction, actionString));
            }

            string packagePath = null;
            if (Arguments.Count() > 1)
            {
                packagePath = Arguments[1];
            }

            var trustedSignersProvider = new TrustedSignersProvider(Settings);

            var trustedSignersArgs = new TrustedSignersArgs()
            {
                Action = action,
                PackagePath = packagePath,
                Name = Name,
                ServiceIndex = ServiceIndex,
                CertificateFingerprint = CertificateFingerprint,
                FingerprintAlgorithm = FingerprintAlgorithm,
                AllowUntrustedRoot = AllowUntrustedRoot,
                Author = Author,
                Repository = Repository,
                Owners = Owners,
                Logger = Console
            };

            if (TrustedSignersCommandRunner == null)
            {
                TrustedSignersCommandRunner = new TrustedSignersCommandRunner(trustedSignersProvider, SourceProvider);
            }

            var result = await TrustedSignersCommandRunner.ExecuteCommandAsync(trustedSignersArgs);

            if (result > 0)
            {
                throw new ExitCodeException(1);
            }
        }
    }
}
