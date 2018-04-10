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
        private const string _noClientAllowList = "The client allow list is required and was empty or not found.";
        private const string _noRepoAllowList = "The repository allow list is required and was empty or not found.";

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedAuthorTestCert;
        private TrustedTestCert<TestCertificate> _trustedRepoTestCert;
        private ISignatureVerificationProvider[] _trustProviders;

        public AllowListVerificationProviderTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedAuthorTestCert = _testFixture.TrustedTestCertificate;
            _trustedRepoTestCert = SigningTestUtility.GenerateTrustedTestCertificate();
            _trustProviders = new[]
            {
                new AllowListVerificationProvider()
            };
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(allowUnsigned: false, allowUntrusted: false, clientAllowList: allowList, repoAllowList: null);

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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(allowUnsigned: false, allowUntrusted: false, clientAllowList: null, repoAllowList: allowList);

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
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithoutClientCertificateAllowList_ClientListRequired_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
                    allowUntrusted: false,
                    allowNoClientCertificateList: false,
                    allowNoRepositoryCertificateList: true,
                    clientAllowList: null,
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any()).ToList();
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues()).ToList();

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noClientAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithEmptyClientCertificateAllowList_ClientListRequired_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
                    allowUntrusted: false,
                    allowNoClientCertificateList: false,
                    allowNoRepositoryCertificateList: true,
                    clientAllowList: new List<CertificateHashAllowListEntry>(),
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any()).ToList();
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues()).ToList();

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noClientAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithoutRepositoryCertificateAllowList_RepositoryListRequired_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: true,
                    allowUntrusted: false,
                    allowNoClientCertificateList: true,
                    allowNoRepositoryCertificateList: false,
                    clientAllowList: null,
                    repoAllowList: null);

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
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithEmptyRepositoryCertificateAllowList_RepositoryListRequired_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: true,
                    allowUntrusted: false,
                    allowNoClientCertificateList: true,
                    allowNoRepositoryCertificateList: false,
                    clientAllowList: null,
                    repoAllowList: new List<CertificateHashAllowListEntry>());

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
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues.First().Message.Should().Be(_noRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithoutRepositoryAndClientCertificateAllowList_RepoListAndClientListRequired_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: true,
                    allowUntrusted: false,
                    allowNoClientCertificateList: false,
                    allowNoRepositoryCertificateList: false,
                    clientAllowList: null,
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues()).ToList();

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(2);
                    totalErrorIssues[0].Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues[0].Message.Should().Be(_noClientAllowList);
                    totalErrorIssues[1].Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues[1].Message.Should().Be(_noRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithEmptyRepositoryAndClientCertificateAllowList_RepoListAndClientListRequired_ErrorAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: true,
                    allowUntrusted: false,
                    allowNoClientCertificateList: false,
                    allowNoRepositoryCertificateList: false,
                    clientAllowList: new List<CertificateHashAllowListEntry>(),
                    repoAllowList: new List<CertificateHashAllowListEntry>());

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues()).ToList();

                    // Assert
                    result.Valid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(2);
                    totalErrorIssues[0].Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues[0].Message.Should().Be(_noClientAllowList);
                    totalErrorIssues[1].Code.Should().Be(NuGetLogCode.NU3034);
                    totalErrorIssues[1].Message.Should().Be(_noRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithoutRepositoryCertificateAllowList_RepositoryAndClientListNotRequired_SuccessAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: true,
                    allowUntrusted: false,
                    allowNoClientCertificateList: true,
                    allowNoRepositoryCertificateList: true,
                    clientAllowList: null,
                    repoAllowList: null);

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
        public async Task GetTrustResultAsync_AuthorSignedPackage_WithEmptyRepositoryCertificateAllowList_RepositoryAndClientListNotRequired_SuccessAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedAuthorTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: true,
                    allowUntrusted: false,
                    allowNoClientCertificateList: true,
                    allowNoRepositoryCertificateList: true,
                    clientAllowList: null,
                    repoAllowList: new List<CertificateHashAllowListEntry>());

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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

                var verifier = new PackageSignatureVerifier(_trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
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

        private SignedPackageVerifierSettings GetSettings(
                    bool allowUnsigned,
                    bool allowUntrusted,
                    IReadOnlyList<VerificationAllowListEntry> repoAllowList,
                    IReadOnlyList<VerificationAllowListEntry> clientAllowList)
        {
            return GetSettings(
                allowUnsigned,
                allowUntrusted,
                (repoAllowList == null || repoAllowList.Count == 0),
                (clientAllowList == null || clientAllowList.Count == 0),
                repoAllowList,
                clientAllowList);
        }

        private SignedPackageVerifierSettings GetSettings(
            bool allowUnsigned,
            bool allowUntrusted,
            bool allowNoRepositoryCertificateList,
            bool allowNoClientCertificateList,
            IReadOnlyList<VerificationAllowListEntry> repoAllowList,
            IReadOnlyList<VerificationAllowListEntry> clientAllowList)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: allowUnsigned,
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
    }
}
#endif