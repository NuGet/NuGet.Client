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
        private const string _noClientAllowList = "The package signature certificate cannot be trusted as no client allow list found.";
        private const string _noRepoAllowList = "The package signature certificate cannot be trusted as no repository allow list found.";

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;

        public AllowListVerificationProviderTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithCertificateInClientAllowList_Success_NoWarnAsync()
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
        public async Task GetTrustResultAsync_VerifyWithCertificateInRepoAllowList_Success_NoWarnAsync()
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
        public async Task GetTrustResultAsync_VerifyWithoutClientCertificateAllowList_ErrorsWhenRequiredAndNotAllowUntrustedAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
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
        public async Task GetTrustResultAsync_VerifyWithEmptyClientCertificateAllowList_ErrorsWhenRequiredAndNotAllowUntrustedAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
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
        public async Task GetTrustResultAsync_VerifyWithoutRepositoryCertificateAllowList_ErrorsWhenRequiredAndNotAllowUntrustedAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
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
        public async Task GetTrustResultAsync_VerifyWithEmptyRepositoryCertificateAllowList_ErrorsWhenRequiredAndNotAllowUntrustedAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
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
        public async Task GetTrustResultAsync_VerifyWithoutRepositoryAndClientCertificateAllowList_ErrorsWhenRequiredAndNotAllowUntrustedAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
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
        public async Task GetTrustResultAsync_VerifyWithEmptyRepositoryAndClientCertificateAllowList_ErrorsWhenRequiredAndNotAllowUntrustedAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
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
        public async Task GetTrustResultAsync_VerifyWithoutClientCertificateAllowList_WarnsWhenRequiredAndAllowUntrustedAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
                    allowUntrusted: true,
                    allowNoClientCertificateList: false,
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
                    totalWarningIssues.Count().Should().Be(1);
                    totalWarningIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalWarningIssues.First().Message.Should().Be(_noClientAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithEmptyClientCertificateAllowList_WarnsWhenRequiredAndAllowUntrustedAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: false,
                    allowUntrusted: true,
                    allowNoClientCertificateList: false,
                    allowNoRepositoryCertificateList: true,
                    clientAllowList: new List<CertificateHashAllowListEntry>(),
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues());

                    // Assert
                    result.Valid.Should().BeTrue();
                    totalWarningIssues.Count().Should().Be(1);
                    totalWarningIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalWarningIssues.First().Message.Should().Be(_noClientAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithoutRepositoryCertificateAllowList_WarnsWhenRequiredAndAllowUntrustedAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: true,
                    allowUntrusted: true,
                    allowNoClientCertificateList: true,
                    allowNoRepositoryCertificateList: false,
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
                    totalWarningIssues.Count().Should().Be(1);
                    totalWarningIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalWarningIssues.First().Message.Should().Be(_noRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithEmptyRepositoryCertificateAllowList_WarnsWhenRequiredAndAllowUntrustedAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: true,
                    allowUntrusted: true,
                    allowNoClientCertificateList: true,
                    allowNoRepositoryCertificateList: false,
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
                    totalWarningIssues.Count().Should().Be(1);
                    totalWarningIssues.First().Code.Should().Be(NuGetLogCode.NU3034);
                    totalWarningIssues.First().Message.Should().Be(_noRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithoutRepositoryAndClientCertificateAllowList_WarnsWhenRequiredAndAllowUntrustedAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: true,
                    allowUntrusted: true,
                    allowNoClientCertificateList: false,
                    allowNoRepositoryCertificateList: false,
                    clientAllowList: null,
                    repoAllowList: null);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues()).ToList();

                    // Assert
                    result.Valid.Should().BeTrue();
                    totalWarningIssues.Count().Should().Be(2);
                    totalWarningIssues[0].Code.Should().Be(NuGetLogCode.NU3034);
                    totalWarningIssues[0].Message.Should().Be(_noClientAllowList);
                    totalWarningIssues[1].Code.Should().Be(NuGetLogCode.NU3034);
                    totalWarningIssues[1].Message.Should().Be(_noRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithEmptyRepositoryAndClientCertificateAllowList_WarnsWhenRequiredAndAllowUntrustedAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
                var verifierSettings = GetSettings(
                    allowUnsigned: true,
                    allowUntrusted: true,
                    allowNoClientCertificateList: false,
                    allowNoRepositoryCertificateList: false,
                    clientAllowList: new List<CertificateHashAllowListEntry>(),
                    repoAllowList: new List<CertificateHashAllowListEntry>());

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                    var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());
                    var totalWarningIssues = resultsWithWarnings.SelectMany(r => r.GetWarningIssues()).ToList();

                    // Assert
                    result.Valid.Should().BeTrue();
                    totalWarningIssues.Count().Should().Be(2);
                    totalWarningIssues[0].Code.Should().Be(NuGetLogCode.NU3034);
                    totalWarningIssues[0].Message.Should().Be(_noClientAllowList);
                    totalWarningIssues[1].Code.Should().Be(NuGetLogCode.NU3034);
                    totalWarningIssues[1].Message.Should().Be(_noRepoAllowList);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_VerifyWithoutRepositoryCertificateAllowList_PassesWhenNotRequiredAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
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
        public async Task GetTrustResultAsync_VerifyWithEmptyRepositoryCertificateAllowList_PassesWhenNotRequiredAsync()
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

                var verifier = new PackageSignatureVerifier(trustProviders);
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
        public async Task GetTrustResultAsync_VerifyWithoutCertificateInClientAllowList_WarnsWhenAllowUntrustedAsync()
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
        public async Task GetTrustResultAsync_VerifyWithoutCertificateInClientAllowList_ErrorsWhenNotAllowUntrustedAsync()
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
        public async Task GetTrustResultAsync_VerifyWithoutCertificateInRepoAllowList_WarnsWhenAllowUntrustedAsync()
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
        public async Task GetTrustResultAsync_VerifyWithoutCertificateInRepoAllowList_ErrorsWhenNotAllowUntrustedAsync()
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
                allowAlwaysVerifyingCountersignature: true,
                repoAllowListEntries: repoAllowList,
                clientAllowListEntries: clientAllowList);
        }
    }
}
#endif