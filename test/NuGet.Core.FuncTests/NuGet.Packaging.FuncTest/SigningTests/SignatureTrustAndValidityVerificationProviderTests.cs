// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.Asn1;

using Test.Utility.Signing;
using Xunit;
using BcAccuracy = Org.BouncyCastle.Asn1.Tsp.Accuracy;
using HashAlgorithmName = NuGet.Common.HashAlgorithmName;

namespace NuGet.Packaging.FuncTest
{
    [Collection(SigningTestCollection.Name)]
    public class SignatureTrustAndValidityVerificationProviderTests
    {
        private readonly SignedPackageVerifierSettings _verifyCommandSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();
        private readonly SignedPackageVerifierSettings _vsClientAcceptModeSettings = SignedPackageVerifierSettings.GetAcceptModeDefaultPolicy();
        private readonly SignedPackageVerifierSettings _defaultSettings = SignedPackageVerifierSettings.GetDefault();
        private readonly SigningTestFixture _testFixture;
        private readonly TrustedTestCert<TestCertificate> _trustedTestCert;
        private readonly TrustedTestCert<TestCertificate> _trustedRepoTestCert;
        private readonly TestCertificate _untrustedTestCertificate;
        private readonly IList<ISignatureVerificationProvider> _trustProviders;

        public SignatureTrustAndValidityVerificationProviderTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _trustedRepoTestCert = _testFixture.TrustedRepositoryCertificate;
            _untrustedTestCertificate = _testFixture.UntrustedTestCertificate;
            _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new SignatureTrustAndValidityVerificationProvider()
            };
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ValidCertificate_SuccessAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);
                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }
#if SUPPORTS_FULL_SIGNING
        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ValidCertificateAndTimestamp_SuccessAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir,
                    timestampService.Url);
                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }
#endif
        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ValidCertificateAndTimestampWithDifferentHashAlgorithms_SuccessAsync()
        {
            var packageContext = new SimpleTestPackageContext();

            using (var directory = TestDirectory.Create())
            using (var certificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    signatureHashAlgorithm: HashAlgorithmName.SHA512);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

#if SUPPORTS_FULL_SIGNING
        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ValidCertificateAndTimestampWithDifferentHashAlgorithms_Timestamped_SuccessAsync()
        {
            var packageContext = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var directory = TestDirectory.Create())
            using (var certificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url,
                    signatureHashAlgorithm: HashAlgorithmName.SHA512);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync()
        {
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var now = DateTimeOffset.UtcNow;

            using (var keyPair = RSA.Create(keySizeInBits: 2048))
            {
                var issueOptions = new IssueCertificateOptions()
                {
                    KeyPair = keyPair,
                    NotAfter = now.AddSeconds(10),
                    NotBefore = now.AddSeconds(-2),
                    SubjectName = new X500DistinguishedName("CN=NuGet Test Expired Certificate")
                };

                using (var certificate = ca.IssueCertificate(issueOptions))
                using (var directory = TestDirectory.Create())
                {
                    var notAfter = certificate.NotAfter.ToUniversalTime();

                    var packageContext = new SimpleTestPackageContext();
                    var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                        certificate,
                        packageContext,
                        directory,
                        timestampService.Url);

                    var waitDuration = (notAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                    // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                    await Task.Delay(waitDuration);

                    Assert.True(DateTime.UtcNow > notAfter);

                    var verifier = new PackageSignatureVerifier(_trustProviders);

                    using (var packageReader = new PackageArchiveReader(signedPackagePath))
                    {
                        var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                        var trustProvider = result.Results.Single();

                        Assert.True(result.IsValid);
                        Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                        Assert.Equal(0, trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error));
                        Assert.Equal(0, trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning));
                    }
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
            var now = DateTimeOffset.UtcNow;

            using (var keyPair = RSA.Create(keySizeInBits: 2048))
            {
                var issueOptions = new IssueCertificateOptions()
                {
                    KeyPair = keyPair,
                    NotAfter = now.AddSeconds(10),
                    NotBefore = now.AddSeconds(-2),
                    SubjectName = new X500DistinguishedName("CN=NuGet Test Expired Certificate")
                };

                using (testServer.RegisterResponder(timestampService))
                using (var certificate = ca.IssueCertificate(issueOptions))
                using (var directory = TestDirectory.Create())
                {
                    var packageContext = new SimpleTestPackageContext();
                    var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                        certificate,
                        packageContext,
                        directory,
                        timestampService.Url);

                    var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                    // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                    if (waitDuration > TimeSpan.Zero)
                    {
                        await Task.Delay(waitDuration);
                    }

                    Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                    var verifier = new PackageSignatureVerifier(_trustProviders);

                    using (var packageReader = new PackageArchiveReader(signedPackagePath))
                    {
                        var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                        var result = results.Results.Single();

                        Assert.False(results.IsValid);
                        Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                        Assert.Equal(1, result.Issues.Count(issue => issue.Level == LogLevel.Error));
                        Assert.Equal(0, result.Issues.Count(issue => issue.Level == LogLevel.Warning));

                        Assert.Contains(result.Issues, issue =>
                            issue.Code == NuGetLogCode.NU3037 &&
                            issue.Level == LogLevel.Error &&
                            issue.Message.Contains("validity period has expired."));
                    }
                }
            }
        }
#endif
        // Verify a package meeting minimum signature requirements.
        // This signature is neither an author nor repository signature.
        [CIOnlyFact]
        public async Task VerifySignaturesAsync_WithBasicSignedCms_SucceedsAsync()
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: false,
                reportUnknownRevocation: false,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Online);

            using (var directory = TestDirectory.Create())
            using (var certificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var packageContext = new SimpleTestPackageContext();
                var unsignedPackageFile = await packageContext.CreateAsFileAsync(directory, "Package.nupkg");
                var signedPackageFile = await SignedArchiveTestUtility.SignPackageFileWithBasicSignedCmsAsync(
                    directory,
                    unsignedPackageFile,
                    certificate);
                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackageFile.FullName))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);

                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    Assert.Equal(1, result.Results.Count);

                    var signedPackageVerificationResult = (SignedPackageVerificationResult)result.Results[0];
                    var signer = signedPackageVerificationResult.Signature.SignedCms.SignerInfos[0];

                    Assert.Equal(0, signer.SignedAttributes.Count);
                    Assert.Equal(0, signer.UnsignedAttributes.Count);

                    Assert.Equal(0, resultsWithErrors.Count());
                    Assert.Equal(0, totalErrorIssues.Count());
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_SettingsRequireTimestamp_NoTimestamp_FailsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: true,
                allowNoTimestamp: false,
                allowUnknownRevocation: false,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Online);

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);
                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    // Assert
                    result.IsValid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                    totalErrorIssues.Count().Should().Be(1);
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3027);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_SettingsNotRequireCheckCountersignature_WithValidPrimarySignatureAndInvalidCountersignature_SucceedsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Online);

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_untrustedTestCertificate.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(repoTestCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }
#if SUPPORTS_FULL_SIGNING
        [CIOnlyFact]
        public async Task VerifySignaturesAsync_SettingsNotRequireCheckCountersignature_WithValidPrimarySignatureAndInvalidCountersignature_Timestamped_SucceedsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Online);

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_untrustedTestCertificate.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir,
                    timestampService.Url);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(repoTestCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"), timestampService.Url);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_SettingsRequireCheckCountersignature_WithValidPrimarySignatureAndInvalidCountersignature_Timestamped_FailsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                revocationMode: RevocationMode.Online);

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_untrustedTestCertificate.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir,
                    timestampService.Url);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(repoTestCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"), timestampService.Url);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                }
            }
        }
#endif
        [CIOnlyFact]
        public async Task VerifySignaturesAsync_SettingsRequireCheckCountersignature_WithValidPrimarySignatureAndInvalidCountersignature_FailsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                revocationMode: RevocationMode.Online);

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_untrustedTestCertificate.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(repoTestCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                }
            }
        }
        [CIOnlyFact]
        public async Task VerifySignaturesAsync_SettingsRequireCheckCountersignature_WithValidPrimarySignatureAndValidCountersignature_SucceedsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Online);

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(repoTestCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }
#if SUPPORTS_FULL_SIGNING
        [CIOnlyFact]
        public async Task VerifySignaturesAsync_SettingsRequireCheckCountersignature_WithValidPrimarySignatureAndValidCountersignature_Timestamped_SucceedsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Online);

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir,
                    timestampService.Url);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(repoTestCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"), timestampService.Url);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_WithExpiredPrimarySignature_ValidCountersignature_AndPrimarySignatureValidAtCountersignTime_SucceedsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Online);

            using (var dir = TestDirectory.Create())
            using (var trustedCertificate = _testFixture.TrustedTestCertificateWillExpireIn10Seconds)
            using (var willExpireCert = new X509Certificate2(trustedCertificate.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    willExpireCert,
                    nupkg,
                    dir);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(repoTestCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"), timestampService.Url);

                await SignatureTestUtility.WaitForCertificateExpirationAsync(willExpireCert);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_WithExpiredAndUntrustedPrimarySignature_ValidCountersignature_AndPrimarySignatureValidAtCountersignTime_SucceedsAsync()
        {
            var nupkg = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Online);
            var verifier = new PackageSignatureVerifier(_trustProviders);

            using (var testDirectory = TestDirectory.Create())
            using (var untrustedCertificate = _testFixture.UntrustedTestCertificateWillExpireIn10Seconds.Cert)
            using (var repositoryCertificate = new X509Certificate2(_testFixture.TrustedRepositoryCertificate.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    untrustedCertificate,
                    nupkg,
                    testDirectory);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    repositoryCertificate,
                    signedPackagePath,
                    testDirectory,
                    new Uri("https://v3serviceIndex.test/api/index.json"),
                    timestampService.Url);

                await SignatureTestUtility.WaitForCertificateExpirationAsync(untrustedCertificate);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_WithExpiredPrimarySignature_ValidCountersignature_AndPrimarySignatureExpiredAtCountersignTime_FailsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                revocationMode: RevocationMode.Online);

            using (var dir = TestDirectory.Create())
            using (var trustedCertificate = _testFixture.TrustedTestCertificateWillExpireIn10Seconds)
            using (var willExpireCert = new X509Certificate2(trustedCertificate.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    willExpireCert,
                    nupkg,
                    dir);

                await SignatureTestUtility.WaitForCertificateExpirationAsync(willExpireCert);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(repoTestCertificate, signedPackagePath, dir, new Uri("https://v3serviceIndex.test/api/index.json"), timestampService.Url);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithInvalidSignature_Timestamped_ThrowsAsync()
        {
            var package = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var directory = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var packageFilePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    package,
                    directory,
                    timestampService.Url);

                using (var packageReader = new PackageArchiveReader(packageFilePath))
                {
                    var signature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    var invalidSignature = SignatureTestUtility.GenerateInvalidPrimarySignature(signature);
                    var provider = new SignatureTrustAndValidityVerificationProvider();

                    var result = await provider.GetTrustResultAsync(
                        packageReader,
                        invalidSignature,
                        _defaultSettings,
                        CancellationToken.None);

                    var issue = result.Issues.FirstOrDefault(log => log.Code == NuGetLogCode.NU3012);

                    Assert.NotNull(issue);
                    Assert.Contains("validation failed.", issue.Message);
                }
            }
        }
#endif

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithInvalidSignature_ThrowsAsync()
        {
            var package = new SimpleTestPackageContext();

            using (var directory = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var packageFilePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    package,
                    directory);

                using (var packageReader = new PackageArchiveReader(packageFilePath))
                {
                    var signature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    var invalidSignature = SignatureTestUtility.GenerateInvalidPrimarySignature(signature);
                    var provider = new SignatureTrustAndValidityVerificationProvider();

                    var result = await provider.GetTrustResultAsync(
                        packageReader,
                        invalidSignature,
                        _defaultSettings,
                        CancellationToken.None);

                    var issue = result.Issues.FirstOrDefault(log => log.Code == NuGetLogCode.NU3012);

                    Assert.NotNull(issue);
                    Assert.Contains("validation failed.", issue.Message);
                }
            }
        }
#if SUPPORTS_FULL_SIGNING
        [CIOnlyFact]
        public async Task GetTrustResultAsync_SettingsRequireExactlyOneTimestamp_MultipleTimestamps_FailsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: false,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Online);
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var verificationProvider = new SignatureTrustAndValidityVerificationProvider();

            using (var package = new PackageArchiveReader(await nupkg.CreateAsStreamAsync(), leaveStreamOpen: false))
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var signatureRequest = new AuthorSignPackageRequest(testCertificate, HashAlgorithmName.SHA256))
            {
                var signature = await SignedArchiveTestUtility.CreatePrimarySignatureForPackageAsync(package, signatureRequest);
                var timestampedSignature = await SignedArchiveTestUtility.TimestampSignature(timestampProvider, signature, signatureRequest.TimestampHashAlgorithm, SignaturePlacement.PrimarySignature, testLogger);
                var reTimestampedSignature = await SignedArchiveTestUtility.TimestampSignature(timestampProvider, timestampedSignature, signatureRequest.TimestampHashAlgorithm, SignaturePlacement.PrimarySignature, testLogger);

                timestampedSignature.Timestamps.Count.Should().Be(1);
                reTimestampedSignature.Timestamps.Count.Should().Be(2);

                // Act
                var result = await verificationProvider.GetTrustResultAsync(package, reTimestampedSignature, settings, CancellationToken.None);
                var totalErrorIssues = result.GetErrorIssues();

                // Assert
                result.Trust.Should().Be(SignatureVerificationStatus.Disallowed);
                totalErrorIssues.Count().Should().Be(1);
                totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3000);
            }
        }
#endif
        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithSignedAndCountersignedPackage_SucceedsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir);

                var repositorySignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    counterCertificate,
                    signedPackagePath,
                    dir,
                    new Uri("https://v3ServiceIndex.test/api/index"));

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(repositorySignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(), CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }
#if SUPPORTS_FULL_SIGNING
        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithSignedTimestampedCountersignedAndCountersignatureTimestampedPackage_SucceedsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir,
                    timestampService.Url);

                var repositorySignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    counterCertificate,
                    signedPackagePath,
                    dir,
                    new Uri("https://v3ServiceIndex.test/api/index"),
                    timestampService.Url);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(repositorySignedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(), CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithExpiredTimestamp_NotAllowIgnoreTimestamp_ShouldNotBeAnErrorAsync()
        {
            using (var nupkgStream = new MemoryStream(GetResource("UntrustedTimestampPackage.nupkg")))
            using (var package = new PackageArchiveReader(nupkgStream, leaveStreamOpen: false))
            {
                var verifier = new PackageSignatureVerifier(_trustProviders);

                // Act 
                var result = await verifier.VerifySignaturesAsync(package, SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(), CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                // Assert 
                totalErrorIssues.Select(i => i.Message).Should().NotContain("A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file.");
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_PrimarySignatureWithUntrustedRoot_EmptyAllowedUntrustedRootList_AllowUntrustedFalse_Timestamped_ErrorAsync()
        {
            var settings = new SignedPackageVerifierSettings(
                              allowUnsigned: false,
                              allowIllegal: false,
                              allowUntrusted: false,
                              allowIgnoreTimestamp: false,
                              allowMultipleTimestamps: false,
                              allowNoTimestamp: false,
                              allowUnknownRevocation: true,
                              reportUnknownRevocation: true,
                              verificationTarget: VerificationTarget.Repository,
                              signaturePlacement: SignaturePlacement.PrimarySignature,
                              repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                              revocationMode: RevocationMode.Online);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                _testFixture.UntrustedTestCertificate.Cert,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList: null);

                var status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);
                SigningTestUtility.AssertUntrustedRoot(status.Issues, Common.LogLevel.Error);
            }
        }
#endif
        [CIOnlyFact]
        public async Task GetTrustResultAsync_PrimarySignatureWithUntrustedRoot_EmptyAllowedUntrustedRootList_AllowUntrustedFalse_ErrorAsync()
        {
            var settings = new SignedPackageVerifierSettings(
                              allowUnsigned: false,
                              allowIllegal: false,
                              allowUntrusted: false,
                              allowIgnoreTimestamp: false,
                              allowMultipleTimestamps: false,
                              allowNoTimestamp: true,
                              allowUnknownRevocation: true,
                              reportUnknownRevocation: true,
                              verificationTarget: VerificationTarget.Repository,
                              signaturePlacement: SignaturePlacement.PrimarySignature,
                              repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                              revocationMode: RevocationMode.Online);

            using (var test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                _testFixture.UntrustedTestCertificate.Cert))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList: null);

                var status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);
                SigningTestUtility.AssertUntrustedRoot(status.Issues, Common.LogLevel.Error);
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignatureWithUntrustedRoot_EmptyAllowedUntrustedRootList_AllowUntrustedFalse_ErrorAsync()
        {
            var settings = new SignedPackageVerifierSettings(
                       allowUnsigned: false,
                       allowIllegal: false,
                       allowUntrusted: false,
                       allowIgnoreTimestamp: false,
                       allowMultipleTimestamps: false,
                       allowNoTimestamp: true,
                       allowUnknownRevocation: true,
                       reportUnknownRevocation: true,
                       verificationTarget: VerificationTarget.Repository,
                       signaturePlacement: SignaturePlacement.Countersignature,
                       repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Always,
                       revocationMode: RevocationMode.Online);

            using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                _testFixture.TrustedTestCertificate.Source.Cert,
                _testFixture.UntrustedTestCertificate.Cert))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList: null);

                var status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);
                SigningTestUtility.AssertUntrustedRoot(status.Issues, Common.LogLevel.Error);
            }
        }
#if SUPPORTS_FULL_SIGNING
        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignatureWithUntrustedRoot_EmptyAllowedUntrustedRootList_AllowUntrustedFalse_Timestamped_ErrorAsync()
        {
            var settings = new SignedPackageVerifierSettings(
                       allowUnsigned: false,
                       allowIllegal: false,
                       allowUntrusted: false,
                       allowIgnoreTimestamp: false,
                       allowMultipleTimestamps: false,
                       allowNoTimestamp: false,
                       allowUnknownRevocation: true,
                       reportUnknownRevocation: true,
                       verificationTarget: VerificationTarget.Repository,
                       signaturePlacement: SignaturePlacement.Countersignature,
                       repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Always,
                       revocationMode: RevocationMode.Online);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                _testFixture.TrustedTestCertificate.Source.Cert,
                _testFixture.UntrustedTestCertificate.Cert,
                timestampService.Url,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList: null);

                var status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);
                SigningTestUtility.AssertUntrustedRoot(status.Issues, Common.LogLevel.Error);
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_PrimarySignatureWithUntrustedRoot_NotInAllowedUntrustedRootList_AllowUntrustedFalse_Timestamped_ErrorAsync()
        {
            var settings = new SignedPackageVerifierSettings(
                  allowUnsigned: false,
                  allowIllegal: false,
                  allowUntrusted: false,
                  allowIgnoreTimestamp: false,
                  allowMultipleTimestamps: false,
                  allowNoTimestamp: false,
                  allowUnknownRevocation: true,
                  reportUnknownRevocation: true,
                  verificationTarget: VerificationTarget.Repository,
                  signaturePlacement: SignaturePlacement.PrimarySignature,
                  repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                  revocationMode: RevocationMode.Online);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                _testFixture.UntrustedTestCertificate.Cert,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList:
                    new List<KeyValuePair<string, HashAlgorithmName>>() { new KeyValuePair<string, HashAlgorithmName>("abc", HashAlgorithmName.SHA256) });

                var status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);
                SigningTestUtility.AssertUntrustedRoot(status.Issues, Common.LogLevel.Error);
            }
        }
#endif

        [CIOnlyFact]
        public async Task GetTrustResultAsync_PrimarySignatureWithUntrustedRoot_NotInAllowedUntrustedRootList_AllowUntrustedFalse_ErrorAsync()
        {
            var settings = new SignedPackageVerifierSettings(
                  allowUnsigned: false,
                  allowIllegal: false,
                  allowUntrusted: false,
                  allowIgnoreTimestamp: false,
                  allowMultipleTimestamps: false,
                  allowNoTimestamp: true,
                  allowUnknownRevocation: true,
                  reportUnknownRevocation: true,
                  verificationTarget: VerificationTarget.Repository,
                  signaturePlacement: SignaturePlacement.PrimarySignature,
                  repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                  revocationMode: RevocationMode.Online);

            using (var test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                _testFixture.UntrustedTestCertificate.Cert))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList:
                    new List<KeyValuePair<string, HashAlgorithmName>>() { new KeyValuePair<string, HashAlgorithmName>("abc", HashAlgorithmName.SHA256) });

                var status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);
                SigningTestUtility.AssertUntrustedRoot(status.Issues, Common.LogLevel.Error);
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignatureWithUntrustedRoot_NotInAllowedUntrustedRootList_AllowUntrustedFalse_ErrorAsync()
        {
            var settings = new SignedPackageVerifierSettings(
             allowUnsigned: false,
             allowIllegal: false,
             allowUntrusted: false,
             allowIgnoreTimestamp: false,
             allowMultipleTimestamps: false,
             allowNoTimestamp: true,
             allowUnknownRevocation: true,
             reportUnknownRevocation: true,
             verificationTarget: VerificationTarget.Repository,
             signaturePlacement: SignaturePlacement.Countersignature,
             repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Always,
             revocationMode: RevocationMode.Online);

            using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                _testFixture.TrustedTestCertificate.Source.Cert,
                _testFixture.UntrustedTestCertificate.Cert))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList:
                    new List<KeyValuePair<string, HashAlgorithmName>>() { new KeyValuePair<string, HashAlgorithmName>("abc", HashAlgorithmName.SHA256) });

                var status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);
                SigningTestUtility.AssertUntrustedRoot(status.Issues, Common.LogLevel.Error);
            }
        }

#if SUPPORTS_FULL_SIGNING
        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignatureWithUntrustedRoot_NotInAllowedUntrustedRootList_AllowUntrustedFalse_Timestamped_ErrorAsync()
        {
            var settings = new SignedPackageVerifierSettings(
             allowUnsigned: false,
             allowIllegal: false,
             allowUntrusted: false,
             allowIgnoreTimestamp: false,
             allowMultipleTimestamps: false,
             allowNoTimestamp: false,
             allowUnknownRevocation: true,
             reportUnknownRevocation: true,
             verificationTarget: VerificationTarget.Repository,
             signaturePlacement: SignaturePlacement.Countersignature,
             repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Always,
             revocationMode: RevocationMode.Online);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                _testFixture.TrustedTestCertificate.Source.Cert,
                _testFixture.UntrustedTestCertificate.Cert,
                timestampService.Url,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList:
                    new List<KeyValuePair<string, HashAlgorithmName>>() { new KeyValuePair<string, HashAlgorithmName>("abc", HashAlgorithmName.SHA256) });

                var status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);
                SigningTestUtility.AssertUntrustedRoot(status.Issues, Common.LogLevel.Error);
            }
        }
#endif

        [CIOnlyFact]
        public async Task GetTrustResultAsync_PrimarySignatureWithUntrustedRoot_InAllowedUntrustedRootList_AllowUntrustedFalse_SucceedsAsync()
        {
            var settings = new SignedPackageVerifierSettings(
           allowUnsigned: false,
           allowIllegal: false,
           allowUntrusted: false,
           allowIgnoreTimestamp: false,
           allowMultipleTimestamps: false,
           allowNoTimestamp: true,
           allowUnknownRevocation: true,
           reportUnknownRevocation: true,
           verificationTarget: VerificationTarget.Repository,
           signaturePlacement: SignaturePlacement.PrimarySignature,
           repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
           revocationMode: RevocationMode.Online);

            var untrustedCertFingerprint = SignatureTestUtility.GetFingerprint(_testFixture.UntrustedTestCertificate.Cert, HashAlgorithmName.SHA256);

            using (var test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                _testFixture.UntrustedTestCertificate.Cert))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList:
                    new List<KeyValuePair<string, HashAlgorithmName>>() { new KeyValuePair<string, HashAlgorithmName>(untrustedCertFingerprint, HashAlgorithmName.SHA256) });

                var status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);
                Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                SigningTestUtility.AssertNotUntrustedRoot(status.Issues, Common.LogLevel.Warning);
            }
        }
#if SUPPORTS_FULL_SIGNING
        [CIOnlyFact]
        public async Task GetTrustResultAsync_PrimarySignatureWithUntrustedRoot_InAllowedUntrustedRootList_AllowUntrustedFalse_Timestamped_SucceedsAsync()
        {
            var settings = new SignedPackageVerifierSettings(
           allowUnsigned: false,
           allowIllegal: false,
           allowUntrusted: false,
           allowIgnoreTimestamp: false,
           allowMultipleTimestamps: false,
           allowNoTimestamp: false,
           allowUnknownRevocation: true,
           reportUnknownRevocation: true,
           verificationTarget: VerificationTarget.Repository,
           signaturePlacement: SignaturePlacement.PrimarySignature,
           repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
           revocationMode: RevocationMode.Online);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var untrustedCertFingerprint = SignatureTestUtility.GetFingerprint(_testFixture.UntrustedTestCertificate.Cert, HashAlgorithmName.SHA256);

            using (var test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                _testFixture.UntrustedTestCertificate.Cert,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList:
                    new List<KeyValuePair<string, HashAlgorithmName>>() { new KeyValuePair<string, HashAlgorithmName>(untrustedCertFingerprint, HashAlgorithmName.SHA256) });

                var status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                SigningTestUtility.AssertNotUntrustedRoot(status.Issues, Common.LogLevel.Warning);
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignatureWithUntrustedRoot_InAllowedUntrustedRootList_AllowUntrustedFalse_Timestamped_SucceedsAsync()
        {
            var settings = new SignedPackageVerifierSettings(
               allowUnsigned: false,
               allowIllegal: false,
               allowUntrusted: false,
               allowIgnoreTimestamp: false,
               allowMultipleTimestamps: false,
               allowNoTimestamp: false,
               allowUnknownRevocation: true,
               reportUnknownRevocation: true,
               verificationTarget: VerificationTarget.Repository,
               signaturePlacement: SignaturePlacement.Countersignature,
               repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Always,
               revocationMode: RevocationMode.Online);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var untrustedCertFingerprint = SignatureTestUtility.GetFingerprint(_testFixture.UntrustedTestCertificate.Cert, HashAlgorithmName.SHA256);

            using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                _testFixture.TrustedTestCertificate.Source.Cert,
                _testFixture.UntrustedTestCertificate.Cert,
                timestampService.Url,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList:
                    new List<KeyValuePair<string, HashAlgorithmName>>() { new KeyValuePair<string, HashAlgorithmName>(untrustedCertFingerprint, HashAlgorithmName.SHA256) });

                var status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                SigningTestUtility.AssertNotUntrustedRoot(status.Issues, Common.LogLevel.Warning);
            }
        }
#endif

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignatureWithUntrustedRoot_InAllowedUntrustedRootList_AllowUntrustedFalse_SucceedsAsync()
        {
            var settings = new SignedPackageVerifierSettings(
               allowUnsigned: false,
               allowIllegal: false,
               allowUntrusted: false,
               allowIgnoreTimestamp: false,
               allowMultipleTimestamps: false,
               allowNoTimestamp: true,
               allowUnknownRevocation: true,
               reportUnknownRevocation: true,
               verificationTarget: VerificationTarget.Repository,
               signaturePlacement: SignaturePlacement.Countersignature,
               repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Always,
               revocationMode: RevocationMode.Online);

            var untrustedCertFingerprint = SignatureTestUtility.GetFingerprint(_testFixture.UntrustedTestCertificate.Cert, HashAlgorithmName.SHA256);

            using (var test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                _testFixture.TrustedTestCertificate.Source.Cert,
                _testFixture.UntrustedTestCertificate.Cert))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList:
                    new List<KeyValuePair<string, HashAlgorithmName>>() { new KeyValuePair<string, HashAlgorithmName>(untrustedCertFingerprint, HashAlgorithmName.SHA256) });

                var status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                SigningTestUtility.AssertNotUntrustedRoot(status.Issues, Common.LogLevel.Warning);
            }
        }
#if SUPPORTS_FULL_SIGNING
        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationInAcceptMode_DoesNotWarnAsync()
        {
            // Arrange
            var settings = SignedPackageVerifierSettings.GetAcceptModeDefaultPolicy();

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                settings);

            Assert.Empty(matchingIssues);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationInRequireMode_WarnsAsync()
        {
            // Arrange
            var settings = SignedPackageVerifierSettings.GetRequireModeDefaultPolicy();

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                settings);

            Assert.Equal(2, matchingIssues.Count);

            AssertOfflineRevocationOnlineMode(matchingIssues, LogLevel.Warning);
            AssertRevocationStatusUnknown(matchingIssues, NuGetLogCode.NU3018, LogLevel.Warning);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationInVerify_WarnsAsync()
        {

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                _verifyCommandSettings);

            Assert.Equal(2, matchingIssues.Count);

            AssertOfflineRevocationOnlineMode(matchingIssues, LogLevel.Warning);
            AssertRevocationStatusUnknown(matchingIssues, NuGetLogCode.NU3018, LogLevel.Warning);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationAndAllowIllegal_WarnsAsync()
        {
            // Arrange
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: true,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                reportUnknownRevocation: false,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Online);

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                settings);

            Assert.Empty(matchingIssues);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationAndAllowUnknownRevocation_WithOnlineRevocationMode_WarnsAsync()
        {
            // Arrange
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Online);

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                settings);

            Assert.Equal(2, matchingIssues.Count);

            AssertOfflineRevocationOnlineMode(matchingIssues, LogLevel.Warning);
            AssertRevocationStatusUnknown(matchingIssues, NuGetLogCode.NU3018, LogLevel.Warning);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationAndAllowUnknownRevocation_WithOfflineRevocationMode_WarnsAsync()
        {
            // Arrange
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Offline);

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Information,
                settings);

            AssertOfflineRevocationOfflineMode(matchingIssues);
            AssertRevocationStatusUnknown(matchingIssues, NuGetLogCode.Undefined, LogLevel.Information);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithTrustedButExpiredPrimaryAndTimestampCertificates_WithUnavailableRevocationInformationAndAllowUnknownRevocation_WarnsAsync()
        {
            var matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                _verifyCommandSettings,
                "ExpiredPrimaryAndTimestampCertificatesWithUnavailableRevocationInfo.nupkg");

            Assert.Equal(4, matchingIssues.Count);

            AssertOfflineRevocationOnlineMode(matchingIssues, LogLevel.Warning);
            AssertRevocationStatusUnknown(matchingIssues, NuGetLogCode.NU3018, LogLevel.Warning);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithTimestampChainingToUntrustedRoot_NotAllowIgnoreTimestamp_FailAsync()
        {
            using (var nupkgStream = new MemoryStream(GetResource("UntrustedTimestampPackage.nupkg")))
            using (var package = new PackageArchiveReader(nupkgStream, leaveStreamOpen: false))
            {
                var verifier = new PackageSignatureVerifier(_trustProviders);

                // Act
                var result = await verifier.VerifySignaturesAsync(package, SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(), CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                // Assert
                result.IsValid.Should().BeFalse();
                resultsWithErrors.Count().Should().Be(1);
                totalErrorIssues.Count().Should().Be(1);
                totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3028);
                totalErrorIssues.First().Message.Should().Contain("A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider");
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithNoIgnoringTimestamp_TimestampWithGeneralizedTimeOutsideCertificateValidity_FailAsync()
        {
            var verificationProvider = new SignatureTrustAndValidityVerificationProvider();

            using (var nupkgStream = new MemoryStream(GetResource("TimestampInvalidGenTimePackage.nupkg")))
            using (var package = new PackageArchiveReader(nupkgStream, leaveStreamOpen: false))
            {
                var signature = await package.GetPrimarySignatureAsync(CancellationToken.None);

                // Act
                var result = await verificationProvider.GetTrustResultAsync(package, signature, SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(), CancellationToken.None);
                var errorIssues = result.Issues.Where(r => r.Level >= LogLevel.Error);

                // Assert
                result.Trust.Should().Be(SignatureVerificationStatus.Disallowed);
                errorIssues.Count().Should().Be(1);
                errorIssues.First().Code.Should().Be(NuGetLogCode.NU3036);
                errorIssues.First().Message.Should().Contain("signature's timestamp's generalized time is outside the timestamping certificate's validity period.");
            }
        }
#endif
         private sealed class Test : IDisposable
        {
            private readonly TestDirectory _directory;
            private bool _isDisposed;

            internal FileInfo PackageFile { get; }

            private Test(TestDirectory directory, FileInfo package)
            {
                _directory = directory;
                PackageFile = package;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _directory.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }

            internal static async Task<Test> CreateAuthorSignedPackageAsync(
                X509Certificate2 certificate,
                Uri timestampServiceUrl = null)
            {
                var packageContext = new SimpleTestPackageContext();
                var directory = TestDirectory.Create();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampServiceUrl);

                return new Test(directory, new FileInfo(signedPackagePath));
            }

            internal static async Task<Test> CreateRepositoryPrimarySignedPackageAsync(
                X509Certificate2 certificate,
                Uri timestampServiceUrl = null)
            {
                var packageContext = new SimpleTestPackageContext();
                var directory = TestDirectory.Create();
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    new Uri("https://nuget.test"),
                    timestampServiceUrl);

                return new Test(directory, new FileInfo(signedPackagePath));
            }

            internal static async Task<Test> CreateAuthorSignedRepositoryCountersignedPackageAsync(
                X509Certificate2 authorCertificate,
                X509Certificate2 repositoryCertificate,
                Uri authorTimestampServiceUrl = null,
                Uri repoTimestampServiceUrl = null)
            {
                var directory = TestDirectory.Create();

                using (var test = await CreateAuthorSignedPackageAsync(authorCertificate, authorTimestampServiceUrl))
                {
                    var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                        repositoryCertificate,
                        test.PackageFile.FullName,
                        directory,
                        new Uri("https://nuget.test"),
                        repoTimestampServiceUrl);

                    return new Test(directory, new FileInfo(signedPackagePath));
                }
            }
        }

        private static async Task<List<SignatureLog>> VerifyUnavailableRevocationInfoAsync(
            SignatureVerificationStatus expectedStatus,
            LogLevel expectedLogLevel,
            SignedPackageVerifierSettings settings,
            string resourceName = "UnavailableCrlPackage.nupkg")
        {
            var verificationProvider = new SignatureTrustAndValidityVerificationProvider();

            using (var nupkgStream = new MemoryStream(GetResource(resourceName)))
            using (var package = new PackageArchiveReader(nupkgStream, leaveStreamOpen: false))
            {
                // Read a signature that is valid in every way except that the CRL information is unavailable.
                var signature = await package.GetPrimarySignatureAsync(CancellationToken.None);

                using (TrustPrimaryRootCertificate(signature))
                using (TrustPrimaryTimestampRootCertificate(signature))
                {
                    // Act
                    var result = await verificationProvider.GetTrustResultAsync(package, signature, settings, CancellationToken.None);

                    // Assert
                    Assert.Equal(expectedStatus, result.Trust);
                    return result
                        .Issues
                        .Where(x => x.Level >= expectedLogLevel)
                        .OrderBy(x => x.Message)
                        .ToList();
                }
            }
        }

        private static void AssertOfflineRevocationOnlineMode(IEnumerable<SignatureLog> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message.Contains("The revocation function was unable to check revocation because the revocation server could not be reached. For more information, visit https://aka.ms/certificateRevocationMode."));
        }

        private static void AssertOfflineRevocationOfflineMode(IEnumerable<SignatureLog> issues)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.Undefined &&
                issue.Level == LogLevel.Information &&
                issue.Message.Contains("The revocation function was unable to check revocation because the certificate is not available in the cached certificate revocation list and NUGET_CERT_REVOCATION_MODE environment variable has been set to offline. For more information, visit https://aka.ms/certificateRevocationMode."));
        }

        private static void AssertRevocationStatusUnknown(IEnumerable<SignatureLog> issues, NuGetLogCode code, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == code &&
                issue.Level == logLevel &&
                issue.Message.Contains("The revocation function was unable to check revocation for the certificate"));
        }

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"NuGet.Packaging.FuncTest.compiler.resources.{name}",
                typeof(SignatureTrustAndValidityVerificationProviderTests));
        }

        private static IDisposable TrustPrimaryRootCertificate(PrimarySignature signature)
        {
            using (var certificateChain = SignatureUtility.GetCertificateChain(signature))
            using (var certDir = TestDirectory.Create())
            {
                return TrustRootCertificate(certificateChain, certDir);
            }
        }

        private static IDisposable TrustPrimaryTimestampRootCertificate(PrimarySignature signature)
        {
            var timestamp = signature.Timestamps.FirstOrDefault();

            if (timestamp == null)
            {
                return null;
            }

            using (var certificateChain = SignatureUtility.GetTimestampCertificateChain(signature))
            using (var certDir = TestDirectory.Create())
            {
                return TrustRootCertificate(certificateChain, certDir);
            }
        }

        private static IDisposable TrustRootCertificate(IX509CertificateChain certificateChain, TestDirectory certDir)
        {
            var rootCertificate = certificateChain.Last();

            return TrustedTestCert.Create(
                new X509Certificate2(rootCertificate),
                StoreName.Root,
                StoreLocation.LocalMachine,
                certDir,
                maximumValidityPeriod: TimeSpan.MaxValue);
        }
    }
}