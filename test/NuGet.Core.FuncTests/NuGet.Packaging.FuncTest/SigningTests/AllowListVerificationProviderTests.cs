// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection("Signing Functional Test Collection")]
    public class AllowListVerificationProviderTests
    {
        private const string _noCertInAllowList = "No certificate matching";

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private IList<ISignatureVerificationProvider> _trustProviders;

        public AllowListVerificationProviderTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithCertificateInAllowList_Success()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, nupkg, dir);
                var certificateFingerprint = CertificateUtility.GetHash(testCertificate, HashAlgorithmName.SHA256);
                var certificateFingerprintString = BitConverter.ToString(certificateFingerprint).Replace("-", "");

                var allowListHashes = new[] { certificateFingerprintString, "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(VerificationTarget.Primary, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, SignedPackageVerifierSettings.Default);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);

                    // Assert
                    result.Valid.Should().BeTrue();
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithoutCertificateInAllowList_Fail()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(VerificationTarget.Primary, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, SignedPackageVerifierSettings.Default);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3003);
                    totalErrorIssues.First().Message.Should().Contain(_noCertInAllowList);
                }
            }
        }
    }
}
#endif