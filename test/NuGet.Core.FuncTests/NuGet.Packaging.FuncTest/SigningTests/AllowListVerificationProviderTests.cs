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
        private const string _noMatchInClientAllowList = "The package signature certificate fingerprint does not match any certificate fingerprint in client allow list.";
        private const string _noMatchInRepoAllowList = "The package signature certificate fingerprint does not match any certificate fingerprint in repository allow list.";
        private const string _noClientAllowList = "A list of trusted signers is required by the client but none was found.";
        private const string _noRepoAllowList = "A repository announced that their packages should be signed but an empty list of trusted certificates was found.";

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedAuthorTestCert;
        private TrustedTestCert<TestCertificate> _trustedRepoTestCert;

        public AllowListVerificationProviderTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedAuthorTestCert = _testFixture.TrustedTestCertificate;
            _trustedRepoTestCert = SigningTestUtility.GenerateTrustedTestCertificate();
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithCertificateInClientAllowList_SuccessAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { certificateFingerprintString, "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author | VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(allowUntrusted: false, clientAllowList: allowList, repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeTrue();
                    totalWarningIssues.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithCertificateInRepoAllowList_SuccessAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA512);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { certificateFingerprintString, "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author | VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature,
                    hash,
                    HashAlgorithmName.SHA512)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(allowUntrusted: false, clientAllowList: null, repoAllowList: allowList);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeTrue();
                    totalWarningIssues.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyTheory]
        [MemberData(nameof(EmptyNullAndRequiredListCombinations))]
        public async Task GetTrustResultAsync_AuthorSignedPackage_RequirementsAsync(
            SignedPackageVerifierSettings verifierSettings,
            bool valid,
            int resultsWithErrorsCount,
            int totalErrorsCount,
            object[][] issues)
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any()).ToList();

                    // Given that the runs are in parallel we need to specify an order to verify them to avoid test flakiness
                    var totalErrors = resultsWithErrors.SelectMany(r => r.GetErrorIssues()).OrderByDescending(i => i.Message.Length).ToList();

                    // Assert
                    result.Valid.Should().Be(valid);
                    resultsWithErrors.Count().Should().Be(resultsWithErrorsCount);
                    totalErrors.Count().Should().Be(totalErrorsCount);

                    for (var i = 0; i < totalErrorsCount; i++)
                    {
                        totalErrors[i].Code.Should().Be((NuGetLogCode)issues[i][0]);
                        totalErrors[i].Message.Should().Be((string)issues[i][1]);
                    }
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_VerifyWithoutCertificateInClientAllowList_AllowUntrusted_WarnAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author | VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: true,
                    clientAllowList: allowList,
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
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
                    totalWarningIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalWarningIssues.First().Message.Should().Be(_noMatchInClientAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithoutCertificateInClientAllowList_NotAllowUntrusted_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author | VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: allowList,
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(1);
                    totalWarningIssues.Count().Should().Be(0);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noMatchInClientAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithoutCertificateInRepoAllowList_AllowUntrusted_WarnAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author | VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: true,
                    clientAllowList: null,
                    repoAllowList: allowList);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
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
                    totalWarningIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalWarningIssues.First().Message.Should().Be(_noMatchInRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithoutCertificateInRepoAllowList_NotAllowUntrusted_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author | VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: null,
                    repoAllowList: allowList);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(1);
                    totalWarningIssues.Count().Should().Be(0);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noMatchInRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositorySignedPackage_RepoAllowListWithAuthorTarget_AndPrimaryPlacement_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(testCertificate, nupkg, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var allowListHashes = new[] { "abc", certificateFingerprintString };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author,
                    SignaturePlacement.PrimarySignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: null,
                    repoAllowList: allowList);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(1);
                    totalWarningIssues.Count().Should().Be(0);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noMatchInRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositorySignedPackage_ClientAllowListWithAuthorTarget_AndPrimaryPlacement_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(testCertificate, nupkg, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var allowListHashes = new[] { "abc", certificateFingerprintString };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author,
                    SignaturePlacement.PrimarySignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: allowList,
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(1);
                    totalWarningIssues.Count().Should().Be(0);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noMatchInClientAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositorySignedPackage_RepoAllowListWithRepositoryTarget_AndCounterPlacementOnly_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(testCertificate, nupkg, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var allowListHashes = new[] { "abc", certificateFingerprintString };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Repository,
                    SignaturePlacement.Countersignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: null,
                    repoAllowList: allowList);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(1);
                    totalWarningIssues.Count().Should().Be(0);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noMatchInRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositorySignedPackage_ClientAllowListWithRepositoryTarget_AndCounterPlacementOnly_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(testCertificate, nupkg, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var allowListHashes = new[] { "abc", certificateFingerprintString };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Repository,
                    SignaturePlacement.Countersignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: allowList,
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(1);
                    totalWarningIssues.Count().Should().Be(0);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noMatchInClientAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositorySignedPackage_RepoAllowListWithRepositoryTarget_AndPrimaryPlacement_SuccessAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(testCertificate, nupkg, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var allowListHashes = new[] { "abc", certificateFingerprintString };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: null,
                    repoAllowList: allowList);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(0);
                    totalWarningIssues.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositorySignedPackage_ClientAllowListWithRepositoryTarget_AndPrimaryPlacement_SuccessAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(testCertificate, nupkg, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var allowListHashes = new[] { "abc", certificateFingerprintString };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: allowList,
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(0);
                    totalWarningIssues.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_RepoAllowListWithAuthorTarget_AndPrimaryPlacement_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(counterCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(counterCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var allowListHashes = new[] { "abc", certificateFingerprintString };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author,
                    SignaturePlacement.PrimarySignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: null,
                    repoAllowList: allowList);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(1);
                    totalWarningIssues.Count().Should().Be(0);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noMatchInRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_ClientAllowListWithAuthorTarget_AndPrimaryPlacement_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(counterCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(counterCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var allowListHashes = new[] { "abc", certificateFingerprintString };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author,
                    SignaturePlacement.PrimarySignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: allowList,
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(1);
                    totalWarningIssues.Count().Should().Be(0);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noMatchInClientAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_RepoAllowListWithRepositoryTarget_AndPrimaryPlacementOnly_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(counterCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(counterCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var allowListHashes = new[] { "abc", certificateFingerprintString };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author | VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: null,
                    repoAllowList: allowList);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(1);
                    totalWarningIssues.Count().Should().Be(0);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noMatchInRepoAllowList);
                }
            }
        }


        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_ClientAllowListWithRepositoryTarget_AndPrimaryPlacementOnly_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(counterCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(counterCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var allowListHashes = new[] { "abc", certificateFingerprintString };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author | VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: allowList,
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(1);
                    totalWarningIssues.Count().Should().Be(0);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noMatchInClientAllowList);
                }
            }
        }


        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_RepoAllowListWithRepositoryTarget_AndCounterPlacement_SuccessAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(counterCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(counterCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var allowListHashes = new[] { "abc", certificateFingerprintString };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author | VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: null,
                    repoAllowList: allowList);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(0);
                    totalWarningIssues.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_ClientAllowListWithRepositoryTarget_AndCounterPlacement_SuccessAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(counterCertificate, HashAlgorithmName.SHA256);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(counterCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var allowListHashes = new[] { "abc", certificateFingerprintString };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Author | VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature,
                    hash,
                    HashAlgorithmName.SHA256)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUntrusted: false,
                    clientAllowList: allowList,
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                    totalErrorIssues.Count().Should().Be(0);
                    totalWarningIssues.Count().Should().Be(0);
                }
            }
        }

        private static SignedPackageVerifierSettings GetSettings(
                    bool allowUntrusted,
                    IReadOnlyList<VerificationAllowListEntry> repoAllowList,
                    IReadOnlyList<VerificationAllowListEntry> clientAllowList)
        {
            return GetSettings(
                allowUntrusted,
                (repoAllowList == null || repoAllowList.Count == 0),
                (clientAllowList == null || clientAllowList.Count == 0),
                repoAllowList,
                clientAllowList);
        }

        private static SignedPackageVerifierSettings GetSettings(
            bool allowUntrusted,
            bool allowNoRepositoryCertificateList,
            bool allowNoClientCertificateList,
            IReadOnlyList<VerificationAllowListEntry> repoAllowList,
            IReadOnlyList<VerificationAllowListEntry> clientAllowList)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: true,
                allowUntrusted: allowUntrusted,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                allowNoClientCertificateList: allowNoClientCertificateList,
                allowNoRepositoryCertificateList: allowNoRepositoryCertificateList,
                alwaysVerifyCountersignature: true,
                repoAllowListEntries: repoAllowList,
                clientAllowListEntries: clientAllowList);
        }

        public static IEnumerable<object[]> EmptyNullAndRequiredListCombinations()
        {
        // AllowUntrusted | AllowNoRepositoryCertificateList | AllowNoClientCertificateList | RepoList | Client List | Valid | ResultsWithErrorCount | TotalErrorIssues | ErrorLogCodes and Messages
            // NoAllowUntrusted_RepoListNotRequire_ClientListRequired_NoClientAllowList_NoRepoAllowList_Error
            yield return new object[]
            { GetSettings(false, true, false, null, null), false, 1, 1, new object[]{  new object[]{ NuGetLogCode.NU3034, _noClientAllowList } } };
            // NoAllowUntrusted_RepoListNotRequire_ClientListRequired_EmptyClientAllowList_NoRepoAllowList_Error
            yield return new object[]
            { GetSettings(false, true, false, null, new List<CertificateHashAllowListEntry>()), false, 1, 1, new object[]{  new object[]{ NuGetLogCode.NU3034, _noClientAllowList } } };
            // NoAllowUntrusted_RepoListRequire_ClientListNotRequired_NoClientAllowList_NoRepoAllowList_Error
            yield return new object[]
            { GetSettings(false, false, true, null, null), false, 1, 1, new object[]{  new object[]{ NuGetLogCode.NU3034, _noRepoAllowList } } };
            // NoAllowUntrusted_RepoListRequire_ClientListNotRequired_NoClientAllowList_EmptyRepoAllowList_Error
            yield return new object[]
            { GetSettings(false, false, true, new List<CertificateHashAllowListEntry>(), null), false, 1, 1, new object[]{  new object[]{ NuGetLogCode.NU3034, _noRepoAllowList } } };
            // NoAllowUntrusted_RepoListRequire_ClientListRequired_NoClientAllowList_NoRepoAllowList_Error
            yield return new object[]
            { GetSettings(false, false, false, null, null), false, 1, 2, new object[]{ new object[]{ NuGetLogCode.NU3034, _noRepoAllowList }, new object[]{ NuGetLogCode.NU3034, _noClientAllowList } } };
            // NoAllowUntrusted_RepoListRequire_ClientListRequired_EmptyClientAllowList_EmptyRepoAllowList_Error
            yield return new object[]
            { GetSettings(false, false, false,  new List<CertificateHashAllowListEntry>(),  new List<CertificateHashAllowListEntry>()), false, 1, 2, new object[]{ new object[]{ NuGetLogCode.NU3034, _noRepoAllowList }, new object[]{ NuGetLogCode.NU3034, _noClientAllowList } } };
            // NoAllowUntrusted_RepoListNotRequire_ClientListNotRequired_NoClientAllowList_NoRepoAllowList_Succeess
            yield return new object[]
            { GetSettings(false, true, true, null, null), true, 0, 0, null };
            // NoAllowUntrusted_RepoListNotRequire_ClientListNotRequired_NoClientAllowList_NoRepoAllowList_Succeess
            yield return new object[]
            { GetSettings(false, true, true, new List<CertificateHashAllowListEntry>(), null), true, 0, 0, null };
            // NoAllowUntrusted_RepoListNotRequire_ClientListNotRequired_NoClientAllowList_EmptyRepoAllowList_Succeess
            yield return new object[]
            { GetSettings(false, true, true, null, new List<CertificateHashAllowListEntry>()), true, 0, 0, null };
        }
    }
}
#endif