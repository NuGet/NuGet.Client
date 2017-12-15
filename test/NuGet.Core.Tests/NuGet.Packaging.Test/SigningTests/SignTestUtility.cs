// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;

namespace NuGet.Packaging.Test
{
    public static class SignTestUtility
    {
        // Environment variable to internal timestamp server. Server will be used as a valid valid rfc3161 timestamping service.
        private static readonly string _testTimestampServer = Environment.GetEnvironmentVariable("TIMESTAMP_SERVER_URL");

        /// <summary>
        /// Sign a package for test purposes.
        /// </summary>
        public static async Task SignPackageAsync(TestLogger testLogger, X509Certificate2 cert, SignedPackageArchive signPackage)
        {
            var testSignatureProvider = new X509SignatureProvider(new Rfc3161TimestampProvider(new Uri(_testTimestampServer)));
            var signer = new Signer(signPackage, testSignatureProvider);

            var request = new SignPackageRequest()
            {
                Certificate = cert,
                SignatureHashAlgorithm = Common.HashAlgorithmName.SHA256
            };

            await signer.SignAsync(request, testLogger, CancellationToken.None);
        }

        public static async Task<VerifySignaturesResult> VerifySignatureAsync(SignedPackageArchive signPackage, SignedPackageVerifierSettings settings)
        {
            var verificationProviders = new[] { new SignatureTrustAndValidityVerificationProvider() };
            var verifier = new PackageSignatureVerifier(verificationProviders, settings);
            var result = await verifier.VerifySignaturesAsync(signPackage, CancellationToken.None);
            return result;
        }
    }
}
