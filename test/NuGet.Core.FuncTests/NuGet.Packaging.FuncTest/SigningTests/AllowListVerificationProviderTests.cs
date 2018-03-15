// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
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
        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private SignedPackageVerifierSettings _settings;

        public AllowListVerificationProviderTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowUntrustedSelfIssuedCertificate: true,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_PrimaryPlacementAndAuthorTarget_WithCertificateInAllowList_Success()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { certificateFingerprintString, "abc" };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.PrimarySignature, VerificationTarget.Author, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

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
        public async Task GetTrustResultAsync_AuthorSignedPackage_PrimaryPlacementAndAuthorTarget_WithoutCertificateInAllowList_Fail()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { "abc" };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.PrimarySignature, VerificationTarget.Author, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

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
                    totalErrorIssues.First().Message.Should().Contain("No allowed certificate");
                }
            }
        }

                [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_PrimaryPlacementAndAuthorTarget_WithoutCertificateInAllowList_Warn()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { "abc" };
                var allowList = allowListHashes.Select(hash => new CertificateHashAllowListEntry(VerificationTarget.Primary, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowIllegal: false,
                    allowUntrusted: true,
                    allowUntrustedSelfIssuedCertificate: true,
                    allowIgnoreTimestamp: true,
                    allowMultipleTimestamps: true,
                    allowNoTimestamp: true,
                    allowUnknownRevocation: true);

                var verifier = new PackageSignatureVerifier(trustProviders, settings);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
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
        public async Task GetTrustResultAsync_AuthorSignedPackage_CounterPlacementAndAuthorTarget_WithCertificateInAllowList_Fails()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { trustedCertFingerprint, "abc" };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.Countersignature, VerificationTarget.Author, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

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
                    totalErrorIssues.First().Message.Should().Contain("No allowed certificate");
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_PrimaryPlacementAndRepositoryTarget_WithCertificateInAllowList_Fails()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { trustedCertFingerprint, "abc" };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.PrimarySignature, VerificationTarget.Repository, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

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
                    totalErrorIssues.First().Message.Should().Contain("No allowed certificate");
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_CounterPlacementAndRepositoryTarget_WithCertificateInAllowList_Fails()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { trustedCertFingerprint, "abc" };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.Countersignature, VerificationTarget.Repository, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

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
                    totalErrorIssues.First().Message.Should().Contain("No allowed certificate");
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_AuthorSignedPackage_CounterAndPrimaryPlacementAndRepositoryTarget_WithCertificateInAllowList_Fails()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { trustedCertFingerprint, "abc" };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature, VerificationTarget.Repository, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

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
                    totalErrorIssues.First().Message.Should().Contain("No allowed certificate");
                }
            }
        }

        public async Task GetTrustResultAsync_AuthorSignedPackage_CounterAndPrimaryPlacementAndAuthorTarget_WithCertificateInAllowList_Succes()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { trustedCertFingerprint, "abc" };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature, VerificationTarget.Author, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

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
        public async Task GetTrustResultAsync_AuthorSignedPackage_CounterAndPrimaryPlacementAndAuthorAndRepositoryTarget_WithCertificateInAllowList_Succes()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);

                var allowListHashes = new[] { trustedCertFingerprint, "abc" };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature, VerificationTarget.Author | VerificationTarget.Repository, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

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
        public async Task GetTrustResultAsync_RepositorySignedPackage_PrimaryPlacementAndAuthorTarget_WithCertificateInAllowList_Fails()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);
                var repositorySignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(testCertificate, nupkg, dir, new Uri("https://v3ServiceIndex.test/api/index"));

                var allowListHashes = new[] { trustedCertFingerprint, "abc" };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.PrimarySignature, VerificationTarget.Author, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

                using (var packageReader = new PackageArchiveReader(repositorySignedPackagePath))
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
                    totalErrorIssues.First().Message.Should().Contain("No allowed certificate");
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositorySignedPackage_CounterAndPrimaryPlacementAndAuthorAndRepositoryTarget_WithCertificateInAllowList_Succes()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);
                var repositorySignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(testCertificate, nupkg, dir, new Uri("https://v3ServiceIndex.test/api/index"));

                var allowListHashes = new[] { trustedCertFingerprint, "abc" };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature, VerificationTarget.Author | VerificationTarget.Repository, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

                using (var packageReader = new PackageArchiveReader(repositorySignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);

                    // Assert
                    result.Valid.Should().BeTrue();
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_PrimaryPlacementAndRepositoryTarget_WithCountersignatureCertificateInAllowList_Fail()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var trusted = SigningTestUtility.GenerateTrustedTestCertificate())
            using (var trustedCertificate = new X509Certificate2(trusted.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(trustedCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var repositoryCountersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedCertificate, signedPackagePath, dir, new Uri("https://v3ServiceIndex.test/api/index"));

                var allowListHashes = new[] { "abc", trustedCertFingerprint };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.PrimarySignature, VerificationTarget.Repository, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

                using (var packageReader = new PackageArchiveReader(repositoryCountersignedPackagePath))
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
                    totalErrorIssues.First().Message.Should().Contain("No allowed certificate");
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_CounterPlacementAndRepositoryTarget_WithCountersignatureCertificateInAllowList_Success()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var trusted = SigningTestUtility.GenerateTrustedTestCertificate())
            using (var trustedCertificate = new X509Certificate2(trusted.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(trustedCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var repositoryCountersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedCertificate, signedPackagePath, dir, new Uri("https://v3ServiceIndex.test/api/index"));

                var allowListHashes = new[] { "abc", trustedCertFingerprint };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.Countersignature, VerificationTarget.Repository, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

                using (var packageReader = new PackageArchiveReader(repositoryCountersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);

                    // Assert
                    result.Valid.Should().BeTrue();
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_CounterPlacementAndRepositoryTarget_WithoutCertificateInAllowList_Fail()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var trusted = SigningTestUtility.GenerateTrustedTestCertificate())
            using (var counterCertificate = new X509Certificate2(trusted.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var repositoryCountersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(counterCertificate, signedPackagePath, dir, new Uri("https://v3ServiceIndex.test/api/index"));

                var allowListHashes = new[] { "abc" };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.Countersignature, VerificationTarget.Repository, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

                using (var packageReader = new PackageArchiveReader(repositoryCountersignedPackagePath))
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
                    totalErrorIssues.First().Message.Should().Contain("No allowed certificate");
                }
            }
        }


        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_CounterPlacementAndRepositoryTarget_WithPrimarySignatureCertificateInAllowList_Fail()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var trusted = SigningTestUtility.GenerateTrustedTestCertificate())
            using (var counterCertificate = new X509Certificate2(trusted.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(primaryCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var repositoryCountersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(counterCertificate, signedPackagePath, dir, new Uri("https://v3ServiceIndex.test/api/index"));

                var allowListHashes = new[] { "abc", trustedCertFingerprint };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.Countersignature, VerificationTarget.Repository, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

                using (var packageReader = new PackageArchiveReader(repositoryCountersignedPackagePath))
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
                    totalErrorIssues.First().Message.Should().Contain("No allowed certificate");
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_PrimaryAndCounterPlacementAndRepositoryAndAuthorTarget_WithPrimarySignatureCertificateInAllowList_Success()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var trusted = SigningTestUtility.GenerateTrustedTestCertificate())
            using (var counterCertificate = new X509Certificate2(trusted.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(primaryCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var repositoryCountersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(counterCertificate, signedPackagePath, dir, new Uri("https://v3ServiceIndex.test/api/index"));

                var allowListHashes = new[] { "abc", trustedCertFingerprint };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature, VerificationTarget.Author|VerificationTarget.Repository, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

                using (var packageReader = new PackageArchiveReader(repositoryCountersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);

                    // Assert
                    result.Valid.Should().BeTrue();
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_PrimaryAndCounterPlacementAndRepositoryAndAuthorTarget_WithCountersignatureCertificateInAllowList_Success()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var trusted = SigningTestUtility.GenerateTrustedTestCertificate())
            using (var counterCertificate = new X509Certificate2(trusted.Source.Cert))
            {
                var trustedCertFingerprint = SignatureTestUtility.GetFingerprint(counterCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var repositoryCountersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(counterCertificate, signedPackagePath, dir, new Uri("https://v3ServiceIndex.test/api/index"));

                var allowListHashes = new[] { "abc", trustedCertFingerprint };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature, VerificationTarget.Author | VerificationTarget.Repository, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

                using (var packageReader = new PackageArchiveReader(repositoryCountersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);

                    // Assert
                    result.Valid.Should().BeTrue();
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignedPackage_PrimaryAndCounterPlacementAndRepositoryAndAuthorTarget_WithoutCertificateInAllowList_Fail()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var primaryCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var trusted = SigningTestUtility.GenerateTrustedTestCertificate())
            using (var counterCertificate = new X509Certificate2(trusted.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(primaryCertificate, nupkg, dir);
                var repositoryCountersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(counterCertificate, signedPackagePath, dir, new Uri("https://v3ServiceIndex.test/api/index"));

                var allowListHashes = new[] { "abc" };
                var allowList = allowListHashes.Select(hash =>
                    new CertificateHashAllowListEntry(SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature, VerificationTarget.Author | VerificationTarget.Repository, hash)).ToList();

                var trustProviders = new[]
                {
                    new AllowListVerificationProvider(allowList)
                };

                var verifier = new PackageSignatureVerifier(trustProviders, _settings);

                using (var packageReader = new PackageArchiveReader(repositoryCountersignedPackagePath))
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
                    totalErrorIssues.First().Message.Should().Contain("No allowed certificate");
                }
            }
        }
    }
}
#endif