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
    [Collection(SigningTestCollection.Name)]
    public class AllowListVerificationProviderTests
    {
        private const string _noCertInAllowList = "The package signature certificate fingerprint does not match any certificate fingerprint";

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;

        public AllowListVerificationProviderTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithCertificateInClientAllowList_Success()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { certificateFingerprintString, "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(VerificationTarget.Primary, hash, HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };

                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(clientAllowList: allowList, repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);

                    // Assert
                    result.Valid.Should().BeTrue();
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithCertificateInRepoAllowList_Success()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA512);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { certificateFingerprintString, "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(VerificationTarget.Primary, hash, HashAlgorithmName.SHA512)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };

                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(clientAllowList: null, repoAllowList: allowList);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);

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
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(VerificationTarget.Primary, hash, HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };

                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(clientAllowList: allowList, repoAllowList: allowList);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
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

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithoutCertificateInAllowListWithAllowUntrusted_Warn()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(VerificationTarget.Primary, hash, HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };

                var settings = new SignedPackageVerifierSettings(
                     allowUnsigned: false,
                     allowIllegal: false,
                     allowUntrusted: true,
                     allowUntrustedSelfIssuedCertificate: true,
                     allowIgnoreTimestamp: true,
                     allowMultipleTimestamps: true,
                     allowNoTimestamp: true,
                     allowUnknownRevocation: true,
                     clientAllowListEntries: allowList,
                     repoAllowListEntries: null);

                var verifier = new PackageSignatureVerifier(trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(0);
                    totalWarningIssues.Count().Should().Be(1);
                    totalWarningIssues.First().Code.Should().Be(NuGetLogCode.NU3003);
                    totalWarningIssues.First().Message.Should().Contain(_noCertInAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithoutAllowListWithAllowUntrusted_Warn()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };

                var settings = new SignedPackageVerifierSettings(
                     allowUnsigned: false,
                     allowIllegal: false,
                     allowUntrusted: true,
                     allowUntrustedSelfIssuedCertificate: true,
                     allowIgnoreTimestamp: true,
                     allowMultipleTimestamps: true,
                     allowNoTimestamp: true,
                     allowUnknownRevocation: true,
                     clientAllowListEntries: null,
                     repoAllowListEntries: null);

                var verifier = new PackageSignatureVerifier(trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(0);
                    totalWarningIssues.Count().Should().Be(1);
                    totalWarningIssues.First().Code.Should().Be(NuGetLogCode.NU3003);
                    totalWarningIssues.First().Message.Should().Contain(_noCertInAllowList);
                }
            }
        }

        private SignedPackageVerifierSettings GetSettings(
            IReadOnlyList<VerificationAllowListEntry> repoAllowList,
            IReadOnlyList<VerificationAllowListEntry> clientAllowList)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowUntrustedSelfIssuedCertificate: true,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                repoAllowListEntries: repoAllowList,
                clientAllowListEntries: clientAllowList);
        }
    }
}
#endif