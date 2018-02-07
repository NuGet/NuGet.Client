// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Test.Utility.Signing;
using Xunit;
using BcAccuracy = Org.BouncyCastle.Asn1.Tsp.Accuracy;

namespace NuGet.Packaging.FuncTest
{
    [Collection("Signing Functional Test Collection")]
    public class SignatureTrustAndValidityVerificationProviderTests
    {
        private SigningSpecifications _specification => SigningSpecifications.V1;

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private TestCertificate _untrustedTestCertificate;
        private IList<ISignatureVerificationProvider> _trustProviders;

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
        public async Task VerifySignaturesAsync_ValidCertificate_Success()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, nupkg, dir);
                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.VerifyCommandDefaultPolicy);
                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ValidCertificateAndTimestamp_Success()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedAndTimeStampedPackageAsync(
                    testCertificate,
                    nupkg,
                    dir,
                    timestampService.Url);
                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.VerifyCommandDefaultPolicy);
                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ValidCertificateAndTimestampWithDifferentHashAlgorithms_Success()
        {
            var packageContext = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var directory = TestDirectory.Create())
            using (var certificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var request = new SignPackageRequest(certificate, HashAlgorithmName.SHA512, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedAndTimeStampedPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url,
                    request);
                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.VerifyCommandDefaultPolicy);
                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_Success()
        {
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
            var now = DateTimeOffset.UtcNow;
            var issueOptions = new IssueCertificateOptions(keyPair.Public)
                {
                    NotAfter = now.AddSeconds(10),
                    NotBefore = now.AddSeconds(-2),
                    SubjectName = new X509Name("CN=NuGet Test Expired Certificate")
                };
            var bcCertificate = ca.IssueCertificate(issueOptions);

            using (var certificate = new X509Certificate2(bcCertificate.GetEncoded()))
            using (var directory = TestDirectory.Create())
            {
                certificate.PrivateKey = DotNetUtilities.ToRSA(keyPair.Private as RsaPrivateCrtKeyParameters);

                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedAndTimeStampedPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var notAfter = certificate.NotAfter.ToUniversalTime();
                var waitDuration = (notAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                await Task.Delay(waitDuration);

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.VerifyCommandDefaultPolicy);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    Assert.True(result.Valid);
                    Assert.Equal(SignatureVerificationStatus.Trusted, trustProvider.Trust);
                    Assert.Equal(0, trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error));
                    Assert.Equal(0, trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_Fails()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
            var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
            var now = DateTimeOffset.UtcNow;
            var issueOptions = new IssueCertificateOptions(keyPair.Public)
            {
                NotAfter = now.AddSeconds(10),
                NotBefore = now.AddSeconds(-2),
                SubjectName = new X509Name("CN=NuGet Test Expired Certificate")
            };
            var bcCertificate = ca.IssueCertificate(issueOptions);

            using (testServer.RegisterResponder(timestampService))
            using (var certificate = new X509Certificate2(bcCertificate.GetEncoded()))
            using (var directory = TestDirectory.Create())
            {
                certificate.PrivateKey = DotNetUtilities.ToRSA(keyPair.Private as RsaPrivateCrtKeyParameters);

                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedAndTimeStampedPackageAsync(
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

                var verifier = new PackageSignatureVerifier(_trustProviders, SignedPackageVerifierSettings.VerifyCommandDefaultPolicy);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);
                    var result = results.Results.Single();

                    Assert.False(results.Valid);
                    Assert.Equal(SignatureVerificationStatus.Untrusted, result.Trust);
                    Assert.Equal(1, result.Issues.Count(issue => issue.Level == LogLevel.Error));
                    Assert.Equal(0, result.Issues.Count(issue => issue.Level == LogLevel.Warning));

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3011 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message == "The primary signature validity period has expired.");
                }
            }
        }

        // Verify a package meeting minimum signature requirements.
        // This signature is neither an author nor repository signature.
        [CIOnlyFact]
        public async Task VerifySignaturesAsync_WithBasicSignedCms_Succeeds()
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowUntrusted: false,
                allowUntrustedSelfSignedCertificate: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: false);

            using (var directory = TestDirectory.Create())
            using (var certificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var packageContext = new SimpleTestPackageContext();
                var unsignedPackageFile = packageContext.CreateAsFile(directory, "Package.nupkg");
                var signedPackageFile = await SignedArchiveTestUtility.SignPackageFileWithBasicSignedCmsAsync(
                    directory,
                    unsignedPackageFile,
                    certificate);
                var verifier = new PackageSignatureVerifier(_trustProviders, settings);

                using (var packageReader = new PackageArchiveReader(signedPackageFile.FullName))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);

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
        public async Task VerifySignaturesAsync_SettingsRequireTimestamp_NoTimestamp_Fails()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var setting = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowUntrusted: false,
                allowUntrustedSelfSignedCertificate: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: true,
                allowNoTimestamp: false,
                allowUnknownRevocation: false);

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, nupkg, dir);
                var verifier = new PackageSignatureVerifier(_trustProviders, setting);
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
                    totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3027);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithInvalidSignature_Throws()
        {
            var package = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var directory = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var packageFilePath = await SignedArchiveTestUtility.CreateSignedAndTimeStampedPackageAsync(
                    testCertificate,
                    package,
                    directory,
                    timestampService.Url);

                using (var packageReader = new PackageArchiveReader(packageFilePath))
                {
                    var signature = await packageReader.GetSignatureAsync(CancellationToken.None);
                    var invalidSignature = SignedArchiveTestUtility.GenerateInvalidSignature(signature);
                    var provider = new SignatureTrustAndValidityVerificationProvider();

                    var result = await provider.GetTrustResultAsync(
                        packageReader,
                        invalidSignature,
                        SignedPackageVerifierSettings.Default,
                        CancellationToken.None);

                    var issue = result.Issues.FirstOrDefault(log => log.Code == NuGetLogCode.NU3012);

                    Assert.NotNull(issue);
                    Assert.Equal("Primary signature validation failed.", issue.Message);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationInVSClient_Warns()
        {
            // Arrange
            var setting = SignedPackageVerifierSettings.VSClientDefaultPolicy;

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfo(
                SignatureVerificationStatus.Trusted,
                LogLevel.Warning,
                setting);

            Assert.Empty(matchingIssues);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationInVerify_Warns()
        {
            // Arrange
            var setting = SignedPackageVerifierSettings.VerifyCommandDefaultPolicy;

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfo(
                SignatureVerificationStatus.Trusted,
                LogLevel.Warning,
                setting);

            Assert.Equal(2, matchingIssues.Count);

            AssertOfflineRevocation(matchingIssues, LogLevel.Warning);
            AssertRevocationStatusUnknown(matchingIssues, LogLevel.Warning);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationAndAllowUntrusted_Warns()
        {
            // Arrange
            var setting = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowUntrusted: true,
                allowUntrustedSelfSignedCertificate: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true);

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfo(
                SignatureVerificationStatus.Trusted,
                LogLevel.Warning,
                setting);

            Assert.Empty(matchingIssues);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationAndAllowUnknownRevocation_Warns()
        {
            // Arrange
            var setting = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowUntrusted: false,
                allowUntrustedSelfSignedCertificate: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true);

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfo(
                SignatureVerificationStatus.Trusted,
                LogLevel.Warning,
                setting);

            Assert.Equal(2, matchingIssues.Count);

            AssertOfflineRevocation(matchingIssues, LogLevel.Warning);
            AssertRevocationStatusUnknown(matchingIssues, LogLevel.Warning);
        }

        private static async Task<List<SignatureLog>> VerifyUnavailableRevocationInfo(
            SignatureVerificationStatus expectedStatus,
            LogLevel expectedLogLevel,
            SignedPackageVerifierSettings setting)
        {
            var verificationProvider = new SignatureTrustAndValidityVerificationProvider();

            using (var nupkgStream = new MemoryStream(GetResource("UnavailableCrlPackage.nupkg")))
            using (var package = new PackageArchiveReader(nupkgStream, leaveStreamOpen: false))
            {
                // Read a signature that is valid in every way except that the CRL information is unavailable.
                var signature = await package.GetSignatureAsync(CancellationToken.None);
                var rootCertificate = SignatureUtility.GetPrimarySignatureCertificates(signature).Last();

                // Trust the root CA of the signing certificate.
                using (var testCertificate = TrustedTestCert.Create(
                    rootCertificate,
                    StoreName.Root,
                    StoreLocation.LocalMachine,
                    maximumValidityPeriod: TimeSpan.MaxValue))
                {
                    // Act
                    var result = await verificationProvider.GetTrustResultAsync(package, signature, setting, CancellationToken.None);

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

        [CIOnlyFact]
        public async Task GetTrustResultAsync_SettingsRequireExactlyOneTimestamp_MultipleTimestamps_Fails()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var setting = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowUntrusted: false,
                allowUntrustedSelfSignedCertificate: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: false);
            var signatureProvider = new X509SignatureProvider(timestampProvider: null);
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var verificationProvider = new SignatureTrustAndValidityVerificationProvider();

            using (var package = new PackageArchiveReader(nupkg.CreateAsStream(), leaveStreamOpen: false))
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var signatureRequest = new SignPackageRequest(testCertificate, HashAlgorithmName.SHA256))
            {
                var signature = await SignedArchiveTestUtility.CreateSignatureForPackageAsync(signatureProvider, package, signatureRequest, testLogger);
                var timestampedSignature = await SignedArchiveTestUtility.TimestampSignature(timestampProvider, signatureRequest, signature, testLogger);
                var reTimestampedSignature = await SignedArchiveTestUtility.TimestampSignature(timestampProvider, signatureRequest, timestampedSignature, testLogger);

                timestampedSignature.Timestamps.Count.Should().Be(1);
                reTimestampedSignature.Timestamps.Count.Should().Be(2);

                // Act
                var result = await verificationProvider.GetTrustResultAsync(package, reTimestampedSignature, setting, CancellationToken.None);
                var totalErrorIssues = result.GetErrorIssues();

                // Assert
                result.Trust.Should().Be(SignatureVerificationStatus.Invalid);
                totalErrorIssues.Count().Should().Be(1);
                totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3000);
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUntrustedSelfSignedCertificateAndNotAllowUntrustedSelfSignedCertificate_Fails()
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowUntrusted: false,
                allowUntrustedSelfSignedCertificate: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: true,
                allowUnknownRevocation: false);

            using (var test = await GetTrustResultAsyncTest.CreateAsync(settings, _untrustedTestCertificate.Cert))
            {
                var result = await test.Provider.GetTrustResultAsync(test.Package, test.Signature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Untrusted, result.Trust);
                Assert.Equal(1, result.Issues.Count(issue => issue.Level == LogLevel.Error));
                Assert.Equal(1, result.Issues.Count(issue => issue.Level == LogLevel.Warning));

                AssertUntrustedRoot(result.Issues, LogLevel.Error);
                AssertTimestampMissing(result.Issues, LogLevel.Warning);
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUntrustedSelfSignedCertificateAndAllowUntrustedSelfSignedCertificate_Warns()
        {
            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowUntrusted: false,
                allowUntrustedSelfSignedCertificate: true,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: true,
                allowUnknownRevocation: false);

            using (var test = await GetTrustResultAsyncTest.CreateAsync(settings, _untrustedTestCertificate.Cert))
            {
                var result = await test.Provider.GetTrustResultAsync(test.Package, test.Signature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Trusted, result.Trust);
                Assert.Equal(0, result.Issues.Count(issue => issue.Level == LogLevel.Error));
                Assert.Equal(2, result.Issues.Count(issue => issue.Level == LogLevel.Warning));

                AssertUntrustedRoot(result.Issues, LogLevel.Warning);
                AssertTimestampMissing(result.Issues, LogLevel.Warning);
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithTrustedSelfSignedCertificateAndNotAllowUntrustedSelfSignedCertificate_Warns()
        {
            var settings = new SignedPackageVerifierSettings(
               allowUnsigned: false,
               allowUntrusted: false,
               allowUntrustedSelfSignedCertificate: false,
               allowIgnoreTimestamp: false,
               allowMultipleTimestamps: false,
               allowNoTimestamp: true,
               allowUnknownRevocation: false);

            using (var test = await GetTrustResultAsyncTest.CreateAsync(settings, _trustedTestCert.Source.Cert))
            {
                var result = await test.Provider.GetTrustResultAsync(test.Package, test.Signature, settings, CancellationToken.None);

                Assert.Equal(SignatureVerificationStatus.Trusted, result.Trust);
                Assert.Equal(0, result.Issues.Count(issue => issue.Level == LogLevel.Error));
                Assert.Equal(1, result.Issues.Count(issue => issue.Level == LogLevel.Warning));

                AssertTimestampMissing(result.Issues, LogLevel.Warning);
            }
        }

        private static void AssertOfflineRevocation(IEnumerable<SignatureLog> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == "The revocation function was unable to check revocation because the revocation server was offline.");
        }

        private static void AssertRevocationStatusUnknown(IEnumerable<SignatureLog> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == "The revocation function was unable to check revocation for the certificate.");
        }

        private static void AssertTimestampMissing(IEnumerable<SignatureLog> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3027 &&
                issue.Level == logLevel &&
                issue.Message == "The primary signature should be timestamped to enable long-term signature validity after the certificate has expired.");
        }

        private static void AssertUntrustedRoot(IEnumerable<SignatureLog> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.");
        }

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"NuGet.Packaging.FuncTest.compiler.resources.{name}",
                typeof(SignatureTrustAndValidityVerificationProviderTests));
        }

        private sealed class GetTrustResultAsyncTest : IDisposable
        {
            private readonly TestDirectory _directory;

            private bool _isDisposed;

            internal SignedPackageArchive Package { get; }
            internal SignatureTrustAndValidityVerificationProvider Provider { get; }
            internal SignedPackageVerifierSettings Settings { get; }
            internal Signature Signature { get; }

            private GetTrustResultAsyncTest(
                TestDirectory directory,
                SignedPackageArchive package,
                Signature signature,
                SignedPackageVerifierSettings settings)
            {
                _directory = directory;
                Package = package;
                Signature = signature;
                Settings = settings;
                Provider = new SignatureTrustAndValidityVerificationProvider();
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _directory.Dispose();
                    Package.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }

            internal static async Task<GetTrustResultAsyncTest> CreateAsync(SignedPackageVerifierSettings settings, X509Certificate2 certificate)
            {
                using (var certificateClone = new X509Certificate2(certificate))
                {
                    var directory = TestDirectory.Create();
                    var packageContext = new SimpleTestPackageContext();
                    var unsignedPackageFile = packageContext.CreateAsFile(directory, "package.nupkg");
                    var signedPackageFile = await SignedArchiveTestUtility.SignPackageFileWithBasicSignedCmsAsync(
                        directory,
                        unsignedPackageFile,
                        certificateClone);
                    var package = new SignedPackageArchive(signedPackageFile.OpenRead(), new MemoryStream());
                    var signature = await package.GetSignatureAsync(CancellationToken.None);

                    return new GetTrustResultAsyncTest(directory, package, signature, settings);
                }
            }
        }
    }
}
#endif