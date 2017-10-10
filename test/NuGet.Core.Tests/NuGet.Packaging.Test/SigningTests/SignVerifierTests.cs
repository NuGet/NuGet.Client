// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test.SigningTests
{
    public class SignVerifierTests
    {
        // Verify a trusted signature
        [Fact]
        public async Task SignVerifier_CreateTrustedPackageVerifySignature()
        {
            var signature = new Signature()
            {
                DisplayName = "Test Signer",
                TestTrust = SignatureTrust.Trusted,
                Type = SignatureType.Author
            };

            var verifyResult = await GetTrustResultAsync(signature, SignVerifierSettings.RequireSigned);

            verifyResult.Valid.Should().BeTrue();
            verifyResult.Results.Single().Trust.Should().Be(SignatureTrust.Trusted);
        }

        // Verify a valid package that does not have a trusted cert
        [Fact]
        public async Task SignVerifier_CreateUntrustedPackageVerifySignature()
        {
            var signature = new Signature()
            {
                DisplayName = "Test Signer",
                TestTrust = SignatureTrust.Untrusted,
                Type = SignatureType.Author
            };

            var verifyResult = await GetTrustResultAsync(signature, SignVerifierSettings.RequireSignedAllowUntrusted);

            verifyResult.Valid.Should().BeTrue();
            verifyResult.Results.Single().Trust.Should().Be(SignatureTrust.Untrusted);
        }

        // Verify a valid package that does not have a trusted cert
        [Fact]
        public async Task SignVerifier_CreateUntrustedPackageVerifySignatureFails()
        {
            var signature = new Signature()
            {
                DisplayName = "Test Signer",
                TestTrust = SignatureTrust.Untrusted,
                Type = SignatureType.Author
            };

            var verifyResult = await GetTrustResultAsync(signature, SignVerifierSettings.RequireSigned);

            verifyResult.Valid.Should().BeFalse();
            verifyResult.Results.Single().Trust.Should().Be(SignatureTrust.Untrusted);
        }

        // Verify a package that has been tampered with
        [Fact]
        public async Task SignVerifier_CreateInvalidPackageVerifySignature()
        {
            var signature = new Signature()
            {
                DisplayName = "Test Signer",
                TestTrust = SignatureTrust.Invalid,
                Type = SignatureType.Author
            };

            var verifyResult = await GetTrustResultAsync(signature, SignVerifierSettings.AllowAll);

            verifyResult.Valid.Should().BeFalse();
            verifyResult.Results.Single().Trust.Should().Be(SignatureTrust.Invalid);
        }

        // Verify a package with no signature
        [Fact]
        public async Task SignVerifier_CreateUnSignedPackageVerifySignatureWithAllowAll()
        {
            var verifyResult = await GetTrustResultAsync(null, SignVerifierSettings.AllowAll);

            verifyResult.Valid.Should().BeTrue();
            verifyResult.Results.Should().BeEmpty();
        }

        // Verify a package with no signature and require signing
        [Fact]
        public async Task SignVerifier_CreateUnSignedPackageVerifySignatureWithRequireSigned()
        {
            var verifyResult = await GetTrustResultAsync(null, SignVerifierSettings.RequireSigned);

            verifyResult.Valid.Should().BeFalse();
            verifyResult.Results.Should().BeEmpty();
        }

        private static Task<VerifySignaturesResult> GetTrustResultAsync(Signature signature, SignVerifierSettings settings)
        {
            var nupkg = new SimpleTestPackageContext();
            if (signature != null)
            {
                nupkg.Signatures.Add(signature);
            }

            var testLogger = new TestLogger();
            var zip = nupkg.Create();

            using (var signPackage = new SignPackageArchive(zip))
            {
                var trustProviders = new[] { new TestTrustProvider() };
                var verifier = new SignVerifier(trustProviders, settings);

                return verifier.VerifySignaturesAsync(signPackage, testLogger, CancellationToken.None);
            }
        }
    }
}
