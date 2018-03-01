// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CommandLine;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace NuGet.MSSigning.Extensions
{
    [Command(typeof(NuGetMSSignCommand), "reposign", "RepoSignCommandDescription",
       MinArgs = 1,
       MaxArgs = 1,
       UsageSummaryResourceName = "RepoSignCommandUsageSummary",
       UsageExampleResourceName = "RepoSignCommandUsageExamples",
       UsageDescriptionResourceName = "RepoSignCommandUsageDescription")]
    public class RepoSignCommand : MSSignAbstract
    {
        // Default constructor used only for testing, since the Command Default Constructor is protected
        public RepoSignCommand() : base()
        {
        }

        private readonly List<string> _owners = new List<string>();

        [Option(typeof(NuGetMSSignCommand), "RepoSignCommandOwnersDescription")]
        public IList<string> Owners => _owners;

        [Option(typeof(NuGetMSSignCommand), "RepoSignCommandV3ServiceIndexUrlDescription")]
        public string V3ServiceIndexUrl { get; set; }

        public override async Task ExecuteCommandAsync()
        {
            using (var signRequest = GetRepositorySignRequest())
            {
                var packages = GetPackages();
                var signCommandRunner = new SignCommandRunner();
                var result = await signCommandRunner.ExecuteCommandAsync(
                    packages, signRequest, Timestamper, Console, OutputDirectory, false, CancellationToken.None);

                if (result != 0)
                {
                    throw new ExitCodeException(exitCode: result);
                }
            }
        }

        private RepositorySignPackageRequest GetRepositorySignRequest()
        {
            ValidatePackagePath();
            WarnIfNoTimestamper(Console);
            ValidateCertificateInputs();
            EnsureOutputDirectory();
            ValidatePackageOwners();

            var signingSpec = SigningSpecifications.V1;
            var signatureHashAlgorithm = ValidateAndParseHashAlgorithm(HashAlgorithm, nameof(HashAlgorithm), signingSpec);
            var timestampHashAlgorithm = ValidateAndParseHashAlgorithm(TimestampHashAlgorithm, nameof(TimestampHashAlgorithm), signingSpec);
            var certCollection = GetCertificateCollection();
            var certificate = GetCertificate(certCollection);
            var privateKey = GetPrivateKey(certificate);
            var v3ServiceIndexUri = ValidateAndParseV3ServiceIndexUrl();

            var request = new RepositorySignPackageRequest(
                certificate,
                signatureHashAlgorithm,
                timestampHashAlgorithm,
                v3ServiceIndexUri,
                new ReadOnlyCollection<string>(Owners))
            {
                PrivateKey = privateKey
            };

            request.AdditionalCertificates.AddRange(certCollection);

            return request;
        }

        private Uri ValidateAndParseV3ServiceIndexUrl()
        {
            // Assert mandatory argument
            if (!string.IsNullOrEmpty(V3ServiceIndexUrl) ||
                Uri.IsWellFormedUriString(V3ServiceIndexUrl, UriKind.Absolute))
            {
                var uri = UriUtility.CreateSourceUri(V3ServiceIndexUrl, UriKind.Absolute);
                if (uri.Scheme == Uri.UriSchemeHttps)
                {
                    return uri;
                }
            }

            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    NuGetMSSignCommand.MSSignCommandInvalidArgumentException,
                    nameof(V3ServiceIndexUrl)));
        }

        private void ValidatePackageOwners()
        {
            if (Owners.Any(packageOwner => string.IsNullOrWhiteSpace(packageOwner)))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    NuGetMSSignCommand.MSSignCommandInvalidArgumentException,
                    nameof(Owners)));
            }
        }
    }
}
