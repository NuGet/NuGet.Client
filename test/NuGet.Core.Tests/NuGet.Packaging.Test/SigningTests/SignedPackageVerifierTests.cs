// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.Test.SigningTests
{
    public class SignedPackageVerifierTests
    {
        // Verify a valid and trusted signature on a package.
        [Fact]
        public async Task SignedPackageVerifier_CreateValidPackageVerifyTrustedResult()
        {
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();
            var zip = nupkg.Create();

            using (var testCert = TestCertificate.Generate().WithTrust())
            using (var signPackage = new SignedPackageArchive(zip))
            {
                await SignTestUtility.SignPackageAsync(testLogger, testCert.Source.Cert, signPackage);

                var settings = SignedPackageVerifierSettings.RequireSigned;

                var result = await VerifySignatureAsync(testLogger, signPackage, settings);

                result.Valid.Should().BeTrue();
            }
        }

        private static async Task<VerifySignaturesResult> VerifySignatureAsync(TestLogger testLogger, SignedPackageArchive signPackage, SignedPackageVerifierSettings settings)
        {
            var verificationProviders = new[] { new X509SignatureVerificationProvider() };
            var verifier = new PackageSignatureVerifier(verificationProviders, settings);
            var result = await verifier.VerifySignaturesAsync(signPackage, testLogger, CancellationToken.None);
            return result;
        }
    }
}
#endif