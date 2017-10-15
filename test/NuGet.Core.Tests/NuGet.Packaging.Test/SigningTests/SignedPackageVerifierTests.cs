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
                await SignPackageAsync(testLogger, testCert.Source.Cert, signPackage);

                var settings = SignedPackageVerifierSettings.RequireSigned;

                var result = await VerifySignatureAsync(testLogger, signPackage, settings);

                result.Valid.Should().BeTrue();
            }
        }

        //// Verify a trusted signature
        //[Fact]
        //public async Task SignedPackageVerifier_CreateTrustedPackageVerifySignature()
        //{
        //    var signature = new Signature()
        //    {
        //        DisplayName = "Test Signer",
        //        TestTrust = SignatureVerificationStatus.Trusted,
        //        Type = SignatureType.Author
        //    };

        //    var verifyResult = await GetTrustResultAsync(signature, SignedPackageVerifierSettings.RequireSigned);

        //    verifyResult.Valid.Should().BeTrue();
        //    verifyResult.Results.Single().Trust.Should().Be(SignatureVerificationStatus.Trusted);
        //}

        //// Verify a valid package that does not have a trusted cert
        //[Fact]
        //public async Task SignedPackageVerifier_CreateUntrustedPackageVerifySignature()
        //{
        //    var signature = new Signature()
        //    {
        //        DisplayName = "Test Signer",
        //        TestTrust = SignatureVerificationStatus.Untrusted,
        //        Type = SignatureType.Author
        //    };

        //    var verifyResult = await GetTrustResultAsync(signature, SignedPackageVerifierSettings.RequireSignedAllowUntrusted);

        //    verifyResult.Valid.Should().BeTrue();
        //    verifyResult.Results.Single().Trust.Should().Be(SignatureVerificationStatus.Untrusted);
        //}

        //// Verify a valid package that does not have a trusted cert
        //[Fact]
        //public async Task SignedPackageVerifier_CreateUntrustedPackageVerifySignatureFails()
        //{
        //    var signature = new Signature()
        //    {
        //        DisplayName = "Test Signer",
        //        TestTrust = SignatureVerificationStatus.Untrusted,
        //        Type = SignatureType.Author
        //    };

        //    var verifyResult = await GetTrustResultAsync(signature, SignedPackageVerifierSettings.RequireSigned);

        //    verifyResult.Valid.Should().BeFalse();
        //    verifyResult.Results.Single().Trust.Should().Be(SignatureVerificationStatus.Untrusted);
        //}

        //// Verify a package that has been tampered with
        //[Fact]
        //public async Task SignedPackageVerifier_CreateInvalidPackageVerifySignature()
        //{
        //    var signature = new Signature()
        //    {
        //        DisplayName = "Test Signer",
        //        TestTrust = SignatureVerificationStatus.Invalid,
        //        Type = SignatureType.Author
        //    };

        //    var verifyResult = await GetTrustResultAsync(signature, SignedPackageVerifierSettings.AllowAll);

        //    verifyResult.Valid.Should().BeFalse();
        //    verifyResult.Results.Single().Trust.Should().Be(SignatureVerificationStatus.Invalid);
        //}

        //// Verify a package with no signature
        //[Fact]
        //public async Task SignedPackageVerifier_CreateUnSignedPackageVerifySignatureWithAllowAll()
        //{
        //    var verifyResult = await GetTrustResultAsync(null, SignedPackageVerifierSettings.AllowAll);

        //    verifyResult.Valid.Should().BeTrue();
        //    verifyResult.Results.Should().BeEmpty();
        //}

        //// Verify a package with no signature and require signing
        //[Fact]
        //public async Task SignedPackageVerifier_CreateUnSignedPackageVerifySignatureWithRequireSigned()
        //{
        //    var verifyResult = await GetTrustResultAsync(null, SignedPackageVerifierSettings.RequireSigned);

        //    verifyResult.Valid.Should().BeFalse();
        //    verifyResult.Results.Should().BeEmpty();
        //}

        //private static Task<VerifySignaturesResult> GetTrustResultAsync(Signature signature, SignedPackageVerifierSettings settings)
        //{
        //    var nupkg = new SimpleTestPackageContext();
        //    if (signature != null)
        //    {
        //        nupkg.Signatures.Add(signature);
        //    }

        //    var testLogger = new TestLogger();
        //    var zip = nupkg.Create();

        //    using (var signPackage = new SignedPackageArchive(zip))
        //    {
        //        var trustProviders = new[] { new TestSignatureVerificationProvider() };
        //        var verifier = new SignedPackageVerifier(trustProviders, settings);

        //        return verifier.VerifySignaturesAsync(signPackage, testLogger, CancellationToken.None);
        //    }
        //}

        private static async Task SignPackageAsync(TestLogger testLogger, X509Certificate2 cert, SignedPackageArchive signPackage)
        {
            var testSignatureProvider = new X509SignatureProvider(new TimestampProvider());
            var signer = new Signer(signPackage, testSignatureProvider);

            var request = new SignPackageRequest()
            {
                Certificate = cert,
                HashAlgorithm = Common.HashAlgorithmName.SHA256
            };

            await signer.SignAsync(request, testLogger, CancellationToken.None);
        }

        private static async Task<VerifySignaturesResult> VerifySignatureAsync(TestLogger testLogger, SignedPackageArchive signPackage, SignedPackageVerifierSettings settings)
        {
            var trustProviders = new[] { new X509SignatureVerificationProvider() };
            var verifier = new SignedPackageVerifier(trustProviders, settings);
            var result = await verifier.VerifySignaturesAsync(signPackage, testLogger, CancellationToken.None);
            return result;
        }
    }
}
#endif