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
        // Verify a valid but untrusted package with a certificate.
        [Fact]
        public async Task SignedPackageVerifier_CreateValidPackageVerifyUntrustedResult()
        {
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();
            var zip = nupkg.Create();

            using (var testCert = TestCertificate.Generate().WithTrust())
            using (var signPackage = new SignedPackageArchive(zip))
            {
                File.WriteAllBytes(@"d:\tmp\cert.pfx", testCert.Source.Cert.Export(X509ContentType.Pfx));
                File.WriteAllBytes(@"d:\tmp\ca.pfx", testCert.Source.CA.Export(X509ContentType.Pfx));

                var before = new List<string>(zip.Entries.Select(e => e.FullName));
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Trusted,
                    Type = SignatureType.Author
                };

                var testSignatureProvider = new X509SignatureProvider(new TimestampProvider());

                var signer = new Signer(signPackage, testSignatureProvider);

                var request = new SignPackageRequest()
                {
                    Certificate = testCert.Source.Cert,
                    HashAlgorithm = Common.HashAlgorithmName.SHA256
                };

                await signer.SignAsync(request, testLogger, CancellationToken.None);

                var trustProviders = new[] { new X509SignatureVerificationProvider() };
                var verifier = new SignedPackageVerifier(trustProviders, SignedPackageVerifierSettings.RequireSignedAllowUntrusted);

                var result = await verifier.VerifySignaturesAsync(signPackage, testLogger, CancellationToken.None);

                result.Valid.Should().BeTrue();
            }
        }

        // Verify a trusted signature
        [Fact]
        public async Task SignedPackageVerifier_CreateTrustedPackageVerifySignature()
        {
            var signature = new Signature()
            {
                DisplayName = "Test Signer",
                TestTrust = SignatureVerificationStatus.Trusted,
                Type = SignatureType.Author
            };

            var verifyResult = await GetTrustResultAsync(signature, SignedPackageVerifierSettings.RequireSigned);

            verifyResult.Valid.Should().BeTrue();
            verifyResult.Results.Single().Trust.Should().Be(SignatureVerificationStatus.Trusted);
        }

        // Verify a valid package that does not have a trusted cert
        [Fact]
        public async Task SignedPackageVerifier_CreateUntrustedPackageVerifySignature()
        {
            var signature = new Signature()
            {
                DisplayName = "Test Signer",
                TestTrust = SignatureVerificationStatus.Untrusted,
                Type = SignatureType.Author
            };

            var verifyResult = await GetTrustResultAsync(signature, SignedPackageVerifierSettings.RequireSignedAllowUntrusted);

            verifyResult.Valid.Should().BeTrue();
            verifyResult.Results.Single().Trust.Should().Be(SignatureVerificationStatus.Untrusted);
        }

        // Verify a valid package that does not have a trusted cert
        [Fact]
        public async Task SignedPackageVerifier_CreateUntrustedPackageVerifySignatureFails()
        {
            var signature = new Signature()
            {
                DisplayName = "Test Signer",
                TestTrust = SignatureVerificationStatus.Untrusted,
                Type = SignatureType.Author
            };

            var verifyResult = await GetTrustResultAsync(signature, SignedPackageVerifierSettings.RequireSigned);

            verifyResult.Valid.Should().BeFalse();
            verifyResult.Results.Single().Trust.Should().Be(SignatureVerificationStatus.Untrusted);
        }

        // Verify a package that has been tampered with
        [Fact]
        public async Task SignedPackageVerifier_CreateInvalidPackageVerifySignature()
        {
            var signature = new Signature()
            {
                DisplayName = "Test Signer",
                TestTrust = SignatureVerificationStatus.Invalid,
                Type = SignatureType.Author
            };

            var verifyResult = await GetTrustResultAsync(signature, SignedPackageVerifierSettings.AllowAll);

            verifyResult.Valid.Should().BeFalse();
            verifyResult.Results.Single().Trust.Should().Be(SignatureVerificationStatus.Invalid);
        }

        // Verify a package with no signature
        [Fact]
        public async Task SignedPackageVerifier_CreateUnSignedPackageVerifySignatureWithAllowAll()
        {
            var verifyResult = await GetTrustResultAsync(null, SignedPackageVerifierSettings.AllowAll);

            verifyResult.Valid.Should().BeTrue();
            verifyResult.Results.Should().BeEmpty();
        }

        // Verify a package with no signature and require signing
        [Fact]
        public async Task SignedPackageVerifier_CreateUnSignedPackageVerifySignatureWithRequireSigned()
        {
            var verifyResult = await GetTrustResultAsync(null, SignedPackageVerifierSettings.RequireSigned);

            verifyResult.Valid.Should().BeFalse();
            verifyResult.Results.Should().BeEmpty();
        }

        private static Task<VerifySignaturesResult> GetTrustResultAsync(Signature signature, SignedPackageVerifierSettings settings)
        {
            var nupkg = new SimpleTestPackageContext();
            if (signature != null)
            {
                nupkg.Signatures.Add(signature);
            }

            var testLogger = new TestLogger();
            var zip = nupkg.Create();

            using (var signPackage = new SignedPackageArchive(zip))
            {
                var trustProviders = new[] { new TestSignatureVerificationProvider() };
                var verifier = new SignedPackageVerifier(trustProviders, settings);

                return verifier.VerifySignaturesAsync(signPackage, testLogger, CancellationToken.None);
            }
        }
    }
}
#endif