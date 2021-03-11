// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_SIGNING_SUPPORTED

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Test.Utility;
using Test.Utility.Signing;
using Xunit;
using BcAccuracy = Org.BouncyCastle.Asn1.Tsp.Accuracy;
using BcX509Certificate = Org.BouncyCastle.X509.X509Certificate;
using HashAlgorithmName = NuGet.Common.HashAlgorithmName;

namespace NuGet.Packaging.FuncTest
{
    [Collection(SigningTestCollection.Name)]
    public class SignatureTrustAndValidityVerificationProviderTests
    {
        private static readonly Uri TestServiceIndexUrl = new Uri("https://v3serviceIndex.test/api/index.json");
        private const string UntrustedChainCertError = "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.";
        private readonly SignedPackageVerifierSettings _verifyCommandSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(TestEnvironmentVariableReader.EmptyInstance);
        private readonly SignedPackageVerifierSettings _defaultSettings = SignedPackageVerifierSettings.GetDefault(TestEnvironmentVariableReader.EmptyInstance);
        private readonly SigningTestFixture _testFixture;
        private readonly TrustedTestCert<TestCertificate> _trustedTestCert;
        private readonly TestCertificate _untrustedTestCertificate;
        private readonly IList<ISignatureVerificationProvider> _trustProviders;

        public SignatureTrustAndValidityVerificationProviderTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
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

            using (TestDirectory dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);
                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ValidCertificateAndTimestamp_SuccessAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (TestDirectory dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir,
                    timestampService.Url);
                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ValidCertificateAndTimestampWithDifferentHashAlgorithms_SuccessAsync()
        {
            var packageContext = new SimpleTestPackageContext();
            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (TestDirectory directory = TestDirectory.Create())
            using (var certificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url,
                    signatureHashAlgorithm: HashAlgorithmName.SHA512);
                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync()
        {
            CertificateAuthority ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            AsymmetricCipherKeyPair keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var issueOptions = new IssueCertificateOptions()
            {
                KeyPair = keyPair,
                NotAfter = now.AddSeconds(10),
                NotBefore = now.AddSeconds(-2),
                SubjectName = new X509Name("CN=NuGet Test Expired Certificate")
            };
            BcX509Certificate bcCertificate = ca.IssueCertificate(issueOptions);

            using (TestDirectory directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                await SignatureTestUtility.WaitForCertificateExpirationAsync(certificate);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    PackageVerificationResult trustProvider = result.Results.Single();

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.Equal(0, trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error));
                    Assert.Equal(0, trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync()
        {
            ISigningTestServer testServer = await _testFixture.GetSigningTestServerAsync();
            CertificateAuthority ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            TimestampService timestampService = TimestampService.Create(ca, serviceOptions);
            AsymmetricCipherKeyPair keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var issueOptions = new IssueCertificateOptions()
            {
                KeyPair = keyPair,
                NotAfter = now.AddSeconds(10),
                NotBefore = now.AddSeconds(-2),
                SubjectName = new X509Name("CN=NuGet Test Expired Certificate")
            };
            BcX509Certificate bcCertificate = ca.IssueCertificate(issueOptions);

            using (testServer.RegisterResponder(timestampService))
            using (TestDirectory directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                await SignatureTestUtility.WaitForCertificateExpirationAsync(certificate);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    VerifySignaturesResult results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    PackageVerificationResult result = results.Results.Single();

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
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: RevocationMode.Online);

            using (TestDirectory directory = TestDirectory.Create())
            using (var certificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var packageContext = new SimpleTestPackageContext();
                FileInfo unsignedPackageFile = await packageContext.CreateAsFileAsync(directory, "Package.nupkg");
                FileInfo signedPackageFile = await SignedArchiveTestUtility.SignPackageFileWithBasicSignedCmsAsync(
                    directory,
                    unsignedPackageFile,
                    certificate);
                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackageFile.FullName))
                {
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);

                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    IEnumerable<ILogMessage> totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                    Assert.Equal(1, result.Results.Count);

                    var signedPackageVerificationResult = (SignedPackageVerificationResult)result.Results[0];
                    SignerInfo signer = signedPackageVerificationResult.Signature.SignedCms.SignerInfos[0];

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

            using (TestDirectory dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);
                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                    IEnumerable<ILogMessage> totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

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
            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
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

            using (TestDirectory dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_untrustedTestCertificate.Cert))
            {
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir,
                    timestampService.Url);
                string countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    repoTestCertificate,
                    signedPackagePath,
                    dir,
                    TestServiceIndexUrl,
                    timestampService.Url);
                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_SettingsRequireCheckCountersignature_WithValidPrimarySignatureAndInvalidCountersignature_FailsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
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

            using (TestDirectory dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_untrustedTestCertificate.Cert))
            {
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir,
                    timestampService.Url);
                string countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    repoTestCertificate,
                    signedPackagePath,
                    dir,
                    TestServiceIndexUrl,
                    timestampService.Url);
                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

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
            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
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

            using (TestDirectory dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir,
                    timestampService.Url);
                string countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    repoTestCertificate,
                    signedPackagePath,
                    dir,
                    TestServiceIndexUrl,
                    timestampService.Url);
                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

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
            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
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

            using (TestDirectory dir = TestDirectory.Create())
            using (TrustedTestCert<TestCertificate> trustedCertificate = _testFixture.CreateTrustedTestCertificateThatWillExpireSoon())
            using (var willExpireCert = new X509Certificate2(trustedCertificate.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    willExpireCert,
                    nupkg,
                    dir);
                string countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    repoTestCertificate,
                    signedPackagePath,
                    dir,
                    TestServiceIndexUrl,
                    timestampService.Url);

                await SignatureTestUtility.WaitForCertificateExpirationAsync(willExpireCert);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

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
            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
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

            using (TestDirectory testDirectory = TestDirectory.Create())
            using (X509Certificate2 untrustedCertificate = _testFixture.CreateUntrustedTestCertificateThatWillExpireSoon().Cert)
            using (var repositoryCertificate = new X509Certificate2(_testFixture.TrustedRepositoryCertificate.Source.Cert))
            {
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    untrustedCertificate,
                    nupkg,
                    testDirectory);
                string countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    repositoryCertificate,
                    signedPackagePath,
                    testDirectory,
                    TestServiceIndexUrl,
                    timestampService.Url);

                await SignatureTestUtility.WaitForCertificateExpirationAsync(untrustedCertificate);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

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
            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
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

            using (TestDirectory dir = TestDirectory.Create())
            using (TrustedTestCert<TestCertificate> trustedCertificate = _testFixture.CreateTrustedTestCertificateThatWillExpireSoon())
            using (var willExpireCert = new X509Certificate2(trustedCertificate.Source.Cert))
            using (var repoTestCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    willExpireCert,
                    nupkg,
                    dir);

                await SignatureTestUtility.WaitForCertificateExpirationAsync(willExpireCert);

                string countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    repoTestCertificate,
                    signedPackagePath,
                    dir,
                    TestServiceIndexUrl,
                    timestampService.Url);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(countersignedPackagePath))
                {
                    // Act
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeFalse();
                    resultsWithErrors.Count().Should().Be(1);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithInvalidSignature_ThrowsAsync()
        {
            var package = new SimpleTestPackageContext();
            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (TestDirectory directory = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string packageFilePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    package,
                    directory,
                    timestampService.Url);

                using (var packageReader = new PackageArchiveReader(packageFilePath))
                {
                    PrimarySignature signature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PrimarySignature invalidSignature = SignatureTestUtility.GenerateInvalidPrimarySignature(signature);
                    var provider = new SignatureTrustAndValidityVerificationProvider();

                    PackageVerificationResult result = await provider.GetTrustResultAsync(
                        packageReader,
                        invalidSignature,
                        _defaultSettings,
                        CancellationToken.None);

                    SignatureLog issue = result.Issues.FirstOrDefault(log => log.Code == NuGetLogCode.NU3012);

                    Assert.NotNull(issue);
                    Assert.Contains("validation failed.", issue.Message);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_SettingsRequireExactlyOneTimestamp_MultipleTimestamps_FailsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();
            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
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
                PrimarySignature signature = await SignedArchiveTestUtility.CreatePrimarySignatureForPackageAsync(package, signatureRequest);
                PrimarySignature timestampedSignature = await SignedArchiveTestUtility.TimestampSignature(
                    timestampProvider,
                    signature,
                    signatureRequest.TimestampHashAlgorithm,
                    SignaturePlacement.PrimarySignature,
                    testLogger);
                PrimarySignature reTimestampedSignature = await SignedArchiveTestUtility.TimestampSignature(
                    timestampProvider,
                    timestampedSignature,
                    signatureRequest.TimestampHashAlgorithm,
                    SignaturePlacement.PrimarySignature,
                    testLogger);

                timestampedSignature.Timestamps.Count.Should().Be(1);
                reTimestampedSignature.Timestamps.Count.Should().Be(2);

                // Act
                PackageVerificationResult result = await verificationProvider.GetTrustResultAsync(package, reTimestampedSignature, settings, CancellationToken.None);
                IEnumerable<ILogMessage> totalErrorIssues = result.GetErrorIssues();

                // Assert
                result.Trust.Should().Be(SignatureVerificationStatus.Disallowed);
                totalErrorIssues.Count().Should().Be(1);
                totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3000);
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_WithSignedAndCountersignedPackage_SucceedsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (TestDirectory dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (TrustedTestCert<TestCertificate> trusted = SigningTestUtility.GenerateTrustedTestCertificate())
            using (var counterCertificate = new X509Certificate2(trusted.Source.Cert))
            {
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir);
                string repositorySignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    counterCertificate,
                    signedPackagePath,
                    dir,
                    TestServiceIndexUrl);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(repositorySignedPackagePath))
                {
                    // Act
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(
                        packageReader,
                        _verifyCommandSettings,
                        CancellationToken.None);
                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_WithSignedTimestampedCountersignedAndCountersignatureTimestampedPackage_SucceedsAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (TestDirectory dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (TrustedTestCert<TestCertificate> trusted = SigningTestUtility.GenerateTrustedTestCertificate())
            using (var counterCertificate = new X509Certificate2(trusted.Source.Cert))
            {
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    testCertificate,
                    nupkg,
                    dir,
                    timestampService.Url);
                string repositorySignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    counterCertificate,
                    signedPackagePath,
                    dir,
                    TestServiceIndexUrl,
                    timestampService.Url);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(repositorySignedPackagePath))
                {
                    // Act
                    VerifySignaturesResult result = await verifier.VerifySignaturesAsync(
                        packageReader,
                        _verifyCommandSettings,
                        CancellationToken.None);
                    IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_WithExpiredTimestamp_NotAllowIgnoreTimestamp_ShouldNotBeAnErrorAsync()
        {
            using (var nupkgStream = new MemoryStream(GetResource("UntrustedTimestampPackage.nupkg")))
            using (var package = new PackageArchiveReader(nupkgStream, leaveStreamOpen: false))
            {
                var verifier = new PackageSignatureVerifier(_trustProviders);

                // Act 
                VerifySignaturesResult result = await verifier.VerifySignaturesAsync(
                    package,
                    _verifyCommandSettings,
                    CancellationToken.None);
                IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                IEnumerable<ILogMessage> totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                // Assert 
                totalErrorIssues.Select(i => i.Message).Should().NotContain("A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file.");
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_PrimarySignatureWithUntrustedRoot_EmptyAllowedUntrustedRootList_AllowUntrustedFalse_ErrorAsync()
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

            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (Test test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                _testFixture.UntrustedTestCertificate.Cert,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList: null);

                PackageVerificationResult status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);
                SigningTestUtility.AssertUntrustedRoot(status.Issues, LogLevel.Error);
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
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Repository,
                signaturePlacement: SignaturePlacement.Countersignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Always,
                revocationMode: RevocationMode.Online);

            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (Test test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                _testFixture.TrustedTestCertificate.Source.Cert,
                _testFixture.UntrustedTestCertificate.Cert,
                timestampService.Url,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList: null);

                PackageVerificationResult status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);
                SigningTestUtility.AssertUntrustedRoot(status.Issues, LogLevel.Error);
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_PrimarySignatureWithUntrustedRoot_NotInAllowedUntrustedRootList_AllowUntrustedFalse_ErrorAsync()
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

            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (Test test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                _testFixture.UntrustedTestCertificate.Cert,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList:
                    new List<KeyValuePair<string, HashAlgorithmName>>() { new KeyValuePair<string, HashAlgorithmName>("abc", HashAlgorithmName.SHA256) });

                PackageVerificationResult status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);
                SigningTestUtility.AssertUntrustedRoot(status.Issues, LogLevel.Error);
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
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Repository,
                signaturePlacement: SignaturePlacement.Countersignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Always,
                revocationMode: RevocationMode.Online);

            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (Test test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                _testFixture.TrustedTestCertificate.Source.Cert,
                _testFixture.UntrustedTestCertificate.Cert,
                timestampService.Url,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList:
                    new List<KeyValuePair<string, HashAlgorithmName>>() { new KeyValuePair<string, HashAlgorithmName>("abc", HashAlgorithmName.SHA256) });

                PackageVerificationResult status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);
                SigningTestUtility.AssertUntrustedRoot(status.Issues, LogLevel.Error);
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_PrimarySignatureWithUntrustedRoot_InAllowedUntrustedRootList_AllowUntrustedFalse_SucceedsAsync()
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

            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            string untrustedCertFingerprint = SignatureTestUtility.GetFingerprint(_testFixture.UntrustedTestCertificate.Cert, HashAlgorithmName.SHA256);

            using (Test test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                _testFixture.UntrustedTestCertificate.Cert,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList:
                    new List<KeyValuePair<string, HashAlgorithmName>>() { new KeyValuePair<string, HashAlgorithmName>(untrustedCertFingerprint, HashAlgorithmName.SHA256) });

                PackageVerificationResult status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                Assert.False(status.Issues.Where(i => i.Level >= LogLevel.Warning)
                    .Any(i => i.Message.Contains(UntrustedChainCertError)));
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_RepositoryCountersignatureWithUntrustedRoot_InAllowedUntrustedRootList_AllowUntrustedFalse_SucceedsAsync()
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

            TimestampService timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            string untrustedCertFingerprint = SignatureTestUtility.GetFingerprint(_testFixture.UntrustedTestCertificate.Cert, HashAlgorithmName.SHA256);

            using (Test test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                _testFixture.TrustedTestCertificate.Source.Cert,
                _testFixture.UntrustedTestCertificate.Cert,
                timestampService.Url,
                timestampService.Url))
            using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
            {
                PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);

                var provider = new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList:
                    new List<KeyValuePair<string, HashAlgorithmName>>() { new KeyValuePair<string, HashAlgorithmName>(untrustedCertFingerprint, HashAlgorithmName.SHA256) });

                PackageVerificationResult status = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                Assert.False(status.Issues.Where(i => i.Level >= LogLevel.Warning)
                    .Any(i => i.Message.Contains(UntrustedChainCertError)));
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationInAcceptMode_DoesNotWarnAsync()
        {
            // Arrange
            SignedPackageVerifierSettings settings = SignedPackageVerifierSettings.GetAcceptModeDefaultPolicy(TestEnvironmentVariableReader.EmptyInstance);

            // Act & Assert
            List<SignatureLog> matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                settings);

            Assert.Empty(matchingIssues);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationInRequireMode_WarnsAsync()
        {
            // Arrange
            SignedPackageVerifierSettings settings = SignedPackageVerifierSettings.GetRequireModeDefaultPolicy(TestEnvironmentVariableReader.EmptyInstance);

            // Act & Assert
            List<SignatureLog> matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                settings);

            if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                Assert.Equal(1, matchingIssues.Count);
            }
            else
            {
                Assert.Equal(2, matchingIssues.Count);
                SigningTestUtility.AssertOfflineRevocationOnlineMode(matchingIssues, LogLevel.Warning);
            }

            SigningTestUtility.AssertRevocationStatusUnknown(matchingIssues, LogLevel.Warning, NuGetLogCode.NU3018);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationInVerify_WarnsAsync()
        {
            // Act & Assert
            List<SignatureLog> matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                _verifyCommandSettings);

            if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                Assert.Equal(1, matchingIssues.Count);
            }
            else
            {
                Assert.Equal(2, matchingIssues.Count);
                SigningTestUtility.AssertOfflineRevocationOnlineMode(matchingIssues, LogLevel.Warning);
            }

            SigningTestUtility.AssertRevocationStatusUnknown(matchingIssues, LogLevel.Warning, NuGetLogCode.NU3018);
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
            List<SignatureLog> matchingIssues = await VerifyUnavailableRevocationInfoAsync(
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
            List<SignatureLog> matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                settings);

            if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                Assert.Equal(1, matchingIssues.Count);
            }
            else
            {
                Assert.Equal(2, matchingIssues.Count);
                SigningTestUtility.AssertOfflineRevocationOnlineMode(matchingIssues, LogLevel.Warning);
            }

            SigningTestUtility.AssertRevocationStatusUnknown(matchingIssues, LogLevel.Warning, NuGetLogCode.NU3018);
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
            List<SignatureLog> matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Information,
                settings);

            if (!RuntimeEnvironmentHelper.IsMacOSX)
            {
                SigningTestUtility.AssertOfflineRevocationOfflineMode(matchingIssues);
            }

            SigningTestUtility.AssertRevocationStatusUnknown(matchingIssues, LogLevel.Information, NuGetLogCode.Undefined);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithTrustedButExpiredPrimaryAndTimestampCertificates_WithUnavailableRevocationInformationAndAllowUnknownRevocation_WarnsAsync()
        {
            List<SignatureLog> matchingIssues = await VerifyUnavailableRevocationInfoAsync(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                _verifyCommandSettings,
                "ExpiredPrimaryAndTimestampCertificatesWithUnavailableRevocationInfo.nupkg");

            if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                Assert.Equal(2, matchingIssues.Count);
            }
            else
            {
                Assert.Equal(4, matchingIssues.Count);
                SigningTestUtility.AssertOfflineRevocationOnlineMode(matchingIssues, LogLevel.Warning);
            }

            SigningTestUtility.AssertRevocationStatusUnknown(matchingIssues, LogLevel.Warning, NuGetLogCode.NU3018);
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_WithTimestampChainingToUntrustedRoot_NotAllowIgnoreTimestamp_FailAsync()
        {
            using (var nupkgStream = new MemoryStream(GetResource("UntrustedTimestampPackage.nupkg")))
            using (var package = new PackageArchiveReader(nupkgStream, leaveStreamOpen: false))
            {
                var verifier = new PackageSignatureVerifier(_trustProviders);

                // Act
                VerifySignaturesResult result = await verifier.VerifySignaturesAsync(
                    package,
                    _verifyCommandSettings,
                    CancellationToken.None);
                IEnumerable<PackageVerificationResult> resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                IEnumerable<ILogMessage> totalErrorIssues = resultsWithErrors.SelectMany(r => r.GetErrorIssues());

                // Assert
                result.IsValid.Should().BeFalse();
                resultsWithErrors.Count().Should().Be(1);
                totalErrorIssues.Count().Should().Be(1);
                SigningTestUtility.AssertUntrustedRoot(totalErrorIssues, NuGetLogCode.NU3028, LogLevel.Error);
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithNoIgnoringTimestamp_TimestampWithGeneralizedTimeOutsideCertificateValidity_FailAsync()
        {
            var verificationProvider = new SignatureTrustAndValidityVerificationProvider();

            using (var nupkgStream = new MemoryStream(GetResource("TimestampInvalidGenTimePackage.nupkg")))
            using (var package = new PackageArchiveReader(nupkgStream, leaveStreamOpen: false))
            {
                PrimarySignature signature = await package.GetPrimarySignatureAsync(CancellationToken.None);

                // Act
                PackageVerificationResult result = await verificationProvider.GetTrustResultAsync(
                    package,
                    signature,
                    _verifyCommandSettings,
                    CancellationToken.None);
                IEnumerable<SignatureLog> errorIssues = result.Issues.Where(r => r.Level >= LogLevel.Error);

                // Assert
                result.Trust.Should().Be(SignatureVerificationStatus.Disallowed);
                errorIssues.Count().Should().Be(1);
                errorIssues.First().Code.Should().Be(NuGetLogCode.NU3036);
                errorIssues.First().Message.Should().Contain("signature's timestamp's generalized time is outside the timestamping certificate's validity period.");
            }
        }

        [Collection(SigningTestCollection.Name)]
        public class AuthorPrimarySignatures
        {
            private readonly SigningTestFixture _fixture;
            private readonly SignatureTrustAndValidityVerificationProvider _provider;

            public AuthorPrimarySignatures(SigningTestFixture fixture)
            {
                if (fixture == null)
                {
                    throw new ArgumentNullException(nameof(fixture));
                }

                _fixture = fixture;
                _provider = new SignatureTrustAndValidityVerificationProvider();
            }

            [CIOnlyFact]
            public async Task GetTrustResultAsync_WithRepositorySignedPackage_ReturnsUnknownAsync()
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
                    verificationTarget: VerificationTarget.Author,
                    signaturePlacement: SignaturePlacement.PrimarySignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                    revocationMode: RevocationMode.Online);

                using (Test test = await Test.CreateRepositoryPrimarySignedPackageAsync(_fixture.TrustedTestCertificate.Source.Cert))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    var provider = new SignatureTrustAndValidityVerificationProvider();
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult result = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Unknown, result.Trust);
                }
            }

            [CIOnlyFact]
            public async Task GetTrustResultAsync_WithValidSignature_ReturnsValidAsync()
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
                    verificationTarget: VerificationTarget.Author,
                    signaturePlacement: SignaturePlacement.PrimarySignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                    revocationMode: RevocationMode.Online);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (Test test = await Test.CreateAuthorSignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                }
            }

            [CIOnlyTheory]
            [InlineData(true, SignatureVerificationStatus.Valid)]
            [InlineData(false, SignatureVerificationStatus.Disallowed)]
            public async Task GetTrustResultAsync_WithValidSignatureButNoTimestamp_ReturnsStatusAsync(
                bool allowNoTimestamp,
                SignatureVerificationStatus expectedStatus)
            {
                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowIllegal: false,
                    allowUntrusted: false,
                    allowIgnoreTimestamp: allowNoTimestamp,
                    allowMultipleTimestamps: false,
                    allowNoTimestamp: allowNoTimestamp,
                    allowUnknownRevocation: true,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.Author,
                    signaturePlacement: SignaturePlacement.PrimarySignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                    revocationMode: RevocationMode.Online);

                using (Test test = await Test.CreateAuthorSignedPackageAsync(_fixture.TrustedTestCertificate.Source.Cert))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(expectedStatus, status.Trust);
                }
            }

            [CIOnlyTheory]
            [InlineData(true, SignatureVerificationStatus.Valid)]
            [InlineData(false, SignatureVerificationStatus.Disallowed)]
            public async Task GetTrustResultAsync_WithUntrustedSignature_ReturnsStatusAsync(
                bool allowUntrusted,
                SignatureVerificationStatus expectedStatus)
            {
                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowIllegal: false,
                    allowUntrusted: allowUntrusted,
                    allowIgnoreTimestamp: false,
                    allowMultipleTimestamps: false,
                    allowNoTimestamp: false,
                    allowUnknownRevocation: true,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.Author,
                    signaturePlacement: SignaturePlacement.PrimarySignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                    revocationMode: RevocationMode.Online);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (Test test = await Test.CreateAuthorSignedPackageAsync(
                    _fixture.UntrustedTestCertificate.Cert,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(expectedStatus, status.Trust);
                }
            }

            [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/9501
            [InlineData(true)]
            [InlineData(false)]
            public async Task GetTrustResultAsync_WithRevokedPrimaryCertificate_ReturnsSuspectAsync(bool allowEverything)
            {
                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: allowEverything,
                    allowIllegal: allowEverything,
                    allowUntrusted: allowEverything,
                    allowIgnoreTimestamp: allowEverything,
                    allowMultipleTimestamps: allowEverything,
                    allowNoTimestamp: allowEverything,
                    allowUnknownRevocation: true,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.Author,
                    signaturePlacement: SignaturePlacement.PrimarySignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                    revocationMode: RevocationMode.Online);
                CertificateAuthority certificateAuthority = await _fixture.GetDefaultTrustedCertificateAuthorityAsync();
                IssueCertificateOptions issueCertificateOptions = IssueCertificateOptions.CreateDefaultForEndCertificate();
                BcX509Certificate bcCertificate = certificateAuthority.IssueCertificate(issueCertificateOptions);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, issueCertificateOptions.KeyPair))
                using (Test test = await Test.CreateAuthorSignedPackageAsync(
                    certificate,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    await certificateAuthority.OcspResponder.WaitForResponseExpirationAsync(bcCertificate);

                    certificateAuthority.Revoke(
                        bcCertificate,
                        RevocationReason.KeyCompromise,
                        DateTimeOffset.UtcNow.AddHours(-1));

                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Suspect, status.Trust);
                }
            }

            [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/9501
            [InlineData(true, SignatureVerificationStatus.Valid)]
            [InlineData(false, SignatureVerificationStatus.Disallowed)]
            public async Task GetTrustResultAsync_WithRevokedTimestampCertificate_ReturnsStatusAsync(
                bool allowIgnoreTimestamp,
                SignatureVerificationStatus expectedStatus)
            {
                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowIllegal: false,
                    allowUntrusted: false,
                    allowIgnoreTimestamp: allowIgnoreTimestamp,
                    allowMultipleTimestamps: false,
                    allowNoTimestamp: false,
                    allowUnknownRevocation: true,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.Author,
                    signaturePlacement: SignaturePlacement.PrimarySignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                    revocationMode: RevocationMode.Online);
                ISigningTestServer testServer = await _fixture.GetSigningTestServerAsync();
                CertificateAuthority certificateAuthority = await _fixture.GetDefaultTrustedCertificateAuthorityAsync();
                TimestampService timestampService = TimestampService.Create(certificateAuthority);

                using (testServer.RegisterResponder(timestampService))
                using (Test test = await Test.CreateAuthorSignedPackageAsync(
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    await certificateAuthority.OcspResponder.WaitForResponseExpirationAsync(timestampService.Certificate);

                    certificateAuthority.Revoke(
                        timestampService.Certificate,
                        RevocationReason.KeyCompromise,
                        DateTimeOffset.UtcNow.AddHours(-1));

                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(expectedStatus, status.Trust);
                }
            }

            [CIOnlyFact]
            public async Task GetTrustResultAsync_WithTamperedRepositoryPrimarySignedPackage_ReturnsValidAsync()
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
                    verificationTarget: VerificationTarget.Author,
                    signaturePlacement: SignaturePlacement.PrimarySignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                    revocationMode: RevocationMode.Online);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (Test test = await Test.CreateAuthorSignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    timestampService.Url))
                {
                    using (FileStream stream = test.PackageFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        stream.Position = 0;

                        stream.WriteByte(0x00);
                    }

                    using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                    {
                        PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                        PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                        Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                    }
                }
            }
        }

        [Collection(SigningTestCollection.Name)]
        public class RepositoryPrimarySignatures
        {
            private readonly SigningTestFixture _fixture;
            private readonly SignatureTrustAndValidityVerificationProvider _provider;

            public RepositoryPrimarySignatures(SigningTestFixture fixture)
            {
                if (fixture == null)
                {
                    throw new ArgumentNullException(nameof(fixture));
                }

                _fixture = fixture;
                _provider = new SignatureTrustAndValidityVerificationProvider();
            }

            [CIOnlyFact]
            public async Task GetTrustResultAsync_WithAuthorSignedPackage_ReturnsUnknownAsync()
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

                using (Test test = await Test.CreateAuthorSignedPackageAsync(_fixture.TrustedTestCertificate.Source.Cert))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    var provider = new SignatureTrustAndValidityVerificationProvider();
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult result = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Unknown, result.Trust);
                }
            }

            [CIOnlyFact]
            public async Task GetTrustResultAsync_WithValidSignature_ReturnsValidAsync()
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
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (Test test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                }
            }

            [CIOnlyTheory]
            [InlineData(true, SignatureVerificationStatus.Valid)]
            [InlineData(false, SignatureVerificationStatus.Disallowed)]
            public async Task GetTrustResultAsync_WithValidSignatureButNoTimestamp_ReturnsStatusAsync(
                bool allowNoTimestamp,
                SignatureVerificationStatus expectedStatus)
            {
                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowIllegal: false,
                    allowUntrusted: false,
                    allowIgnoreTimestamp: allowNoTimestamp,
                    allowMultipleTimestamps: false,
                    allowNoTimestamp: allowNoTimestamp,
                    allowUnknownRevocation: true,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.Repository,
                    signaturePlacement: SignaturePlacement.PrimarySignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                    revocationMode: RevocationMode.Online);

                using (Test test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                    _fixture.TrustedRepositoryCertificate.Source.Cert))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(expectedStatus, status.Trust);
                }
            }

            [CIOnlyTheory]
            [InlineData(true, SignatureVerificationStatus.Valid)]
            [InlineData(false, SignatureVerificationStatus.Disallowed)]
            public async Task GetTrustResultAsync_WithUntrustedSignature_ReturnsStatusAsync(
                bool allowUntrusted,
                SignatureVerificationStatus expectedStatus)
            {
                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowIllegal: false,
                    allowUntrusted: allowUntrusted,
                    allowIgnoreTimestamp: false,
                    allowMultipleTimestamps: false,
                    allowNoTimestamp: false,
                    allowUnknownRevocation: true,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.Repository,
                    signaturePlacement: SignaturePlacement.PrimarySignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                    revocationMode: RevocationMode.Online);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (Test test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                    _fixture.UntrustedTestCertificate.Cert,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(expectedStatus, status.Trust);
                }
            }

            [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/9501
            [InlineData(true)]
            [InlineData(false)]
            public async Task GetTrustResultAsync_WithRevokedPrimaryCertificate_ReturnsSuspectAsync(bool allowEverything)
            {
                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: allowEverything,
                    allowIllegal: allowEverything,
                    allowUntrusted: allowEverything,
                    allowIgnoreTimestamp: allowEverything,
                    allowMultipleTimestamps: allowEverything,
                    allowNoTimestamp: allowEverything,
                    allowUnknownRevocation: true,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.Repository,
                    signaturePlacement: SignaturePlacement.PrimarySignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                    revocationMode: RevocationMode.Online);
                CertificateAuthority certificateAuthority = await _fixture.GetDefaultTrustedCertificateAuthorityAsync();
                IssueCertificateOptions issueCertificateOptions = IssueCertificateOptions.CreateDefaultForEndCertificate();
                BcX509Certificate bcCertificate = certificateAuthority.IssueCertificate(issueCertificateOptions);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, issueCertificateOptions.KeyPair))
                using (Test test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                    certificate,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    await certificateAuthority.OcspResponder.WaitForResponseExpirationAsync(bcCertificate);

                    certificateAuthority.Revoke(
                        bcCertificate,
                        RevocationReason.KeyCompromise,
                        DateTimeOffset.UtcNow.AddHours(-1));

                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Suspect, status.Trust);
                }
            }

            [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/9501
            [InlineData(true, SignatureVerificationStatus.Valid)]
            [InlineData(false, SignatureVerificationStatus.Disallowed)]
            public async Task GetTrustResultAsync_WithRevokedTimestampCertificate_ReturnsStatusAsync(
                bool allowIgnoreTimestamp,
                SignatureVerificationStatus expectedStatus)
            {
                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowIllegal: false,
                    allowUntrusted: false,
                    allowIgnoreTimestamp: allowIgnoreTimestamp,
                    allowMultipleTimestamps: false,
                    allowNoTimestamp: false,
                    allowUnknownRevocation: true,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.Repository,
                    signaturePlacement: SignaturePlacement.PrimarySignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                    revocationMode: RevocationMode.Online);
                ISigningTestServer testServer = await _fixture.GetSigningTestServerAsync();
                CertificateAuthority certificateAuthority = await _fixture.GetDefaultTrustedCertificateAuthorityAsync();
                TimestampService timestampService = TimestampService.Create(certificateAuthority);

                using (testServer.RegisterResponder(timestampService))
                using (Test test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    await certificateAuthority.OcspResponder.WaitForResponseExpirationAsync(timestampService.Certificate);

                    certificateAuthority.Revoke(
                        timestampService.Certificate,
                        RevocationReason.KeyCompromise,
                        DateTimeOffset.UtcNow.AddHours(-1));

                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(expectedStatus, status.Trust);
                }
            }

            [CIOnlyFact]
            public async Task GetTrustResultAsync_WithTamperedRepositoryPrimarySignedPackage_ReturnsValidAsync()
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
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (Test test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url))
                {
                    using (FileStream stream = test.PackageFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        stream.Position = 0;

                        stream.WriteByte(0x00);
                    }

                    using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                    {
                        PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                        PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                        Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                    }
                }
            }

            [CIOnlyFact]
            public async Task GetTrustResultAsync_WithAlwaysVerifyCountersignatureBehavior_ReturnsDisallowedAsync()
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
                    signaturePlacement: SignaturePlacement.Any,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Always,
                    revocationMode: RevocationMode.Online);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (Test test = await Test.CreateRepositoryPrimarySignedPackageAsync(
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Disallowed, status.Trust);

                    IEnumerable<ILogMessage> errors = status.GetErrorIssues();

                    Assert.Equal(1, errors.Count());

                    ILogMessage error = errors.Single();

                    Assert.Equal(NuGetLogCode.NU3038, error.Code);
                    Assert.Equal("Verification settings require a repository countersignature, but the package does not have a repository countersignature.", error.Message);
                }
            }
        }

        [Collection(SigningTestCollection.Name)]
        public class RepositoryCountersignatures
        {
            private readonly SigningTestFixture _fixture;
            private readonly SignatureTrustAndValidityVerificationProvider _provider;

            public RepositoryCountersignatures(SigningTestFixture fixture)
            {
                if (fixture == null)
                {
                    throw new ArgumentNullException(nameof(fixture));
                }

                _fixture = fixture;
                _provider = new SignatureTrustAndValidityVerificationProvider();
            }

            [CIOnlyFact]
            public async Task GetTrustResultAsync_WithAuthorSignedPackage_ReturnsUnknownAsync()
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
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                    revocationMode: RevocationMode.Online);

                using (Test test = await Test.CreateAuthorSignedPackageAsync(_fixture.TrustedTestCertificate.Source.Cert))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    var provider = new SignatureTrustAndValidityVerificationProvider();
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult result = await provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Unknown, result.Trust);
                }
            }

            [CIOnlyFact]
            public async Task GetTrustResultAsync_WithValidCountersignature_ReturnsValidAsync()
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
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                    revocationMode: RevocationMode.Online);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (Test test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                }
            }

            [CIOnlyFact]
            public async Task GetTrustResultAsync_WithValidCountersignatureAndUntrustedPrimarySignature_ReturnsValidAsync()
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
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                    revocationMode: RevocationMode.Online);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (Test test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.UntrustedTestCertificate.Cert,
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                }
            }

            [CIOnlyTheory]
            [InlineData(true, SignatureVerificationStatus.Valid)]
            [InlineData(false, SignatureVerificationStatus.Disallowed)]
            public async Task GetTrustResultAsync_WithValidCountersignatureButNoTimestamp_ReturnsStatusAsync(
                bool allowNoTimestamp,
                SignatureVerificationStatus expectedStatus)
            {
                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowIllegal: false,
                    allowUntrusted: false,
                    allowIgnoreTimestamp: false,
                    allowMultipleTimestamps: false,
                    allowNoTimestamp: allowNoTimestamp,
                    allowUnknownRevocation: true,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.Repository,
                    signaturePlacement: SignaturePlacement.Countersignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                    revocationMode: RevocationMode.Online);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (Test test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url))
                using (PackageArchiveReader packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(expectedStatus, status.Trust);
                }
            }

            [CIOnlyTheory]
            [InlineData(true, SignatureVerificationStatus.Valid)]
            [InlineData(false, SignatureVerificationStatus.Disallowed)]
            public async Task GetTrustResultAsync_WithUntrustedCountersignature_ReturnsStatusAsync(
                bool allowUntrusted,
                SignatureVerificationStatus expectedStatus)
            {
                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowIllegal: false,
                    allowUntrusted: allowUntrusted,
                    allowIgnoreTimestamp: false,
                    allowMultipleTimestamps: false,
                    allowNoTimestamp: false,
                    allowUnknownRevocation: true,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.Repository,
                    signaturePlacement: SignaturePlacement.Countersignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                    revocationMode: RevocationMode.Online);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (Test test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    _fixture.UntrustedTestCertificate.Cert,
                    timestampService.Url,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(expectedStatus, status.Trust);
                }
            }

            [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/9501
            [InlineData(true)]
            [InlineData(false)]
            public async Task GetTrustResultAsync_WithRevokedCountersignatureCertificate_ReturnsSuspectAsync(bool allowEverything)
            {
                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: allowEverything,
                    allowIllegal: allowEverything,
                    allowUntrusted: allowEverything,
                    allowIgnoreTimestamp: allowEverything,
                    allowMultipleTimestamps: allowEverything,
                    allowNoTimestamp: allowEverything,
                    allowUnknownRevocation: true,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.Repository,
                    signaturePlacement: SignaturePlacement.Countersignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                    revocationMode: RevocationMode.Online);
                CertificateAuthority certificateAuthority = await _fixture.GetDefaultTrustedCertificateAuthorityAsync();
                IssueCertificateOptions issueCertificateOptions = IssueCertificateOptions.CreateDefaultForEndCertificate();
                BcX509Certificate bcCertificate = certificateAuthority.IssueCertificate(issueCertificateOptions);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, issueCertificateOptions.KeyPair))
                using (Test test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    certificate,
                    timestampService.Url,
                    timestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    await certificateAuthority.OcspResponder.WaitForResponseExpirationAsync(bcCertificate);

                    certificateAuthority.Revoke(
                        bcCertificate,
                        RevocationReason.KeyCompromise,
                        DateTimeOffset.UtcNow.AddHours(-1));

                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(SignatureVerificationStatus.Suspect, status.Trust);
                }
            }

            [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/9501
            [InlineData(true, SignatureVerificationStatus.Valid)]
            [InlineData(false, SignatureVerificationStatus.Disallowed)]
            public async Task GetTrustResultAsync_WithRevokedTimestampCertificate_ReturnsStatusAsync(
                bool allowIgnoreTimestamp,
                SignatureVerificationStatus expectedStatus)
            {
                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowIllegal: false,
                    allowUntrusted: false,
                    allowIgnoreTimestamp: allowIgnoreTimestamp,
                    allowMultipleTimestamps: false,
                    allowNoTimestamp: false,
                    allowUnknownRevocation: true,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.Repository,
                    signaturePlacement: SignaturePlacement.Countersignature,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                    revocationMode: RevocationMode.Online);
                ISigningTestServer testServer = await _fixture.GetSigningTestServerAsync();
                CertificateAuthority certificateAuthority = await _fixture.GetDefaultTrustedCertificateAuthorityAsync();
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();
                TimestampService revokedTimestampService = TimestampService.Create(certificateAuthority);

                using (testServer.RegisterResponder(revokedTimestampService))
                using (Test test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url,
                    revokedTimestampService.Url))
                using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                {
                    await certificateAuthority.OcspResponder.WaitForResponseExpirationAsync(revokedTimestampService.Certificate);

                    certificateAuthority.Revoke(
                        revokedTimestampService.Certificate,
                        RevocationReason.KeyCompromise,
                        DateTimeOffset.UtcNow.AddHours(-1));

                    PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                    Assert.Equal(expectedStatus, status.Trust);
                }
            }

            [CIOnlyFact]
            public async Task GetTrustResultAsync_WithTamperedRepositoryCountersignedPackage_ReturnsValidAsync()
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
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                    revocationMode: RevocationMode.Online);
                TimestampService timestampService = await _fixture.GetDefaultTrustedTimestampServiceAsync();

                using (Test test = await Test.CreateAuthorSignedRepositoryCountersignedPackageAsync(
                    _fixture.TrustedTestCertificate.Source.Cert,
                    _fixture.TrustedRepositoryCertificate.Source.Cert,
                    timestampService.Url,
                    timestampService.Url))
                {
                    using (FileStream stream = test.PackageFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        stream.Position = 0;

                        stream.WriteByte(0x00);
                    }

                    using (var packageReader = new PackageArchiveReader(test.PackageFile.FullName))
                    {
                        PrimarySignature primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                        PackageVerificationResult status = await _provider.GetTrustResultAsync(packageReader, primarySignature, settings, CancellationToken.None);

                        Assert.Equal(SignatureVerificationStatus.Valid, status.Trust);
                    }
                }
            }
        }

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
                TestDirectory directory = TestDirectory.Create();
                string signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
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
                TestDirectory directory = TestDirectory.Create();
                string signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
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
                TestDirectory directory = TestDirectory.Create();

                using (Test test = await CreateAuthorSignedPackageAsync(authorCertificate, authorTimestampServiceUrl))
                {
                    string signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
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
                PrimarySignature signature = await package.GetPrimarySignatureAsync(CancellationToken.None);

                using (TrustPrimaryRootCertificate(signature))
                using (TrustPrimaryTimestampRootCertificate(signature))
                {
                    // Act
                    PackageVerificationResult result = await verificationProvider.GetTrustResultAsync(package, signature, settings, CancellationToken.None);

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

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"NuGet.Packaging.FuncTest.compiler.resources.{name}",
                typeof(SignatureTrustAndValidityVerificationProviderTests));
        }

        private static IDisposable TrustPrimaryRootCertificate(PrimarySignature signature)
        {
            using (IX509CertificateChain certificateChain = SignatureUtility.GetCertificateChain(signature))
            {
                return TrustRootCertificate(certificateChain);
            }
        }

        private static IDisposable TrustPrimaryTimestampRootCertificate(PrimarySignature signature)
        {
            Timestamp timestamp = signature.Timestamps.FirstOrDefault();

            if (timestamp == null)
            {
                return null;
            }

            using (IX509CertificateChain certificateChain = SignatureUtility.GetTimestampCertificateChain(signature))
            {
                return TrustRootCertificate(certificateChain);
            }
        }

        private static IDisposable TrustRootCertificate(IX509CertificateChain certificateChain)
        {
            X509Certificate2 rootCertificate = certificateChain.Last();
            StoreLocation storeLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocation();

            return TrustedTestCert.Create(
                new X509Certificate2(rootCertificate),
                StoreName.Root,
                storeLocation,
                maximumValidityPeriod: TimeSpan.MaxValue);
        }
    }
}
#endif
