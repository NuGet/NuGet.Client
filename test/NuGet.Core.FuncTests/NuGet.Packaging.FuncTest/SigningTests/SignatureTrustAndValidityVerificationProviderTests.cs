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
    [Collection(SigningTestCollection.Name)]
    public class SignatureTrustAndValidityVerificationProviderTests
    {
        private SignedPackageVerifierSettings _verifyCommandSettings => SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();
        private SignedPackageVerifierSettings _vsClientAcceptModeSettings => SignedPackageVerifierSettings.GetAcceptModeDefaultPolicy();
        private SignedPackageVerifierSettings _defaultSettings => SignedPackageVerifierSettings.GetDefault();
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
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);
                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
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
            var issueOptions = new IssueCertificateOptions()
            {
                KeyPair = keyPair,
                NotAfter = now.AddSeconds(10),
                NotBefore = now.AddSeconds(-2),
                SubjectName = new X509Name("CN=NuGet Test Expired Certificate")
            };
            var bcCertificate = ca.IssueCertificate(issueOptions);

            using (var certificate = new X509Certificate2(bcCertificate.GetEncoded()))
            using (var directory = TestDirectory.Create())
            {
                certificate.PrivateKey = DotNetUtilities.ToRSA(keyPair.Private as RsaPrivateCrtKeyParameters);
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

                    Assert.True(result.Valid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
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
            var issueOptions = new IssueCertificateOptions()
            {
                KeyPair = keyPair,
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

                    Assert.False(results.Valid);
                    Assert.Equal(SignatureVerificationStatus.Illegal, result.Trust);
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
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: false,
                allowNoClientCertificateList: true,
                allowNoRepositoryCertificateList: true,
                allowAlwaysVerifyingCountersignature: false);

            using (var directory = TestDirectory.Create())
            using (var certificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var packageContext = new SimpleTestPackageContext();
                var unsignedPackageFile = packageContext.CreateAsFile(directory, "Package.nupkg");
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
        public async Task VerifySignaturesAsync_SettingsRequireTimestamp_NoTimestamp_Fails()
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
                allowAlwaysVerifyingCountersignature: false,
                allowNoClientCertificateList: true,
                allowNoRepositoryCertificateList: true);

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
                    Assert.Equal("Primary signature validation failed.", issue.Message);
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
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: false,
                allowNoClientCertificateList: true,
                allowNoRepositoryCertificateList: true,
                allowAlwaysVerifyingCountersignature: false);
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var verificationProvider = new SignatureTrustAndValidityVerificationProvider();

            using (var package = new PackageArchiveReader(nupkg.CreateAsStream(), leaveStreamOpen: false))
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var signatureRequest = new AuthorSignPackageRequest(testCertificate, HashAlgorithmName.SHA256))
            {
                var signature = await SignedArchiveTestUtility.CreatePrimarySignatureForPackageAsync(package, signatureRequest);
                var timestampedSignature = await SignedArchiveTestUtility.TimestampSignature(timestampProvider, signature, signatureRequest.TimestampHashAlgorithm, SignaturePlacement.PrimarySignature, testLogger);
                var reTimestampedSignature = await SignedArchiveTestUtility.TimestampSignature(timestampProvider, timestampedSignature, signatureRequest.TimestampHashAlgorithm, SignaturePlacement.PrimarySignature, testLogger);

                timestampedSignature.Timestamps.Count.Should().Be(1);
                reTimestampedSignature.Timestamps.Count.Should().Be(2);

                // Act
                var result = await verificationProvider.GetTrustResultAsync(package, reTimestampedSignature, setting, CancellationToken.None);
                var totalErrorIssues = result.GetErrorIssues();

                // Assert
                result.Trust.Should().Be(SignatureVerificationStatus.Illegal);
                totalErrorIssues.Count().Should().Be(1);
                totalErrorIssues.First().Code.Should().Be(NuGetLogCode.NU3000);
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithSignedAndCountersignedPackage_Succeeds()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var trusted = SigningTestUtility.GenerateTrustedTestCertificate())
            using (var counterCertificate = new X509Certificate2(trusted.Source.Cert))
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
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithSignedTimestampedCountersignedAndCountersignatureTimestampedPackage_Succeeds()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var trusted = SigningTestUtility.GenerateTrustedTestCertificate())
            using (var counterCertificate = new X509Certificate2(trusted.Source.Cert))
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
                    result.Valid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationInAcceptMode_DoesNotWarn()
        {
            // Arrange
            var setting = SignedPackageVerifierSettings.GetAcceptModeDefaultPolicy();

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfo(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                setting);

            Assert.Empty(matchingIssues);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationInRequireMode_Warns()
        {
            // Arrange
            var setting = SignedPackageVerifierSettings.GetRequireModeDefaultPolicy();

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfo(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                setting);

            Assert.Equal(2, matchingIssues.Count);

            AssertOfflineRevocation(matchingIssues, LogLevel.Warning);
            AssertRevocationStatusUnknown(matchingIssues, LogLevel.Warning);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationInVerify_Warns()
        {
            // Arrange
            var setting = _verifyCommandSettings;

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfo(
                SignatureVerificationStatus.Valid,
                LogLevel.Warning,
                setting);

            Assert.Equal(2, matchingIssues.Count);

            AssertOfflineRevocation(matchingIssues, LogLevel.Warning);
            AssertRevocationStatusUnknown(matchingIssues, LogLevel.Warning);
        }

        [CIOnlyFact]
        public async Task GetTrustResultAsync_WithUnavailableRevocationInformationAndAllowIllegal_Warns()
        {
            // Arrange
            var setting = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: true,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                allowAlwaysVerifyingCountersignature: false,
                allowNoClientCertificateList: true,
                allowNoRepositoryCertificateList: true);

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfo(
                SignatureVerificationStatus.Valid,
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
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                allowNoClientCertificateList: true,
                allowNoRepositoryCertificateList: true,
                allowAlwaysVerifyingCountersignature: false);

            // Act & Assert
            var matchingIssues = await VerifyUnavailableRevocationInfo(
                SignatureVerificationStatus.Valid,
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
                var signature = await package.GetPrimarySignatureAsync(CancellationToken.None);

                using (var certificateChain = SignatureUtility.GetCertificateChain(signature))
                {
                    var rootCertificate = certificateChain.Last();

                    // Trust the root CA of the signing certificate.
                    using (var testCertificate = TrustedTestCert.Create(
                        new X509Certificate2(rootCertificate),
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
            internal PrimarySignature PrimarySignature { get; }

            private GetTrustResultAsyncTest(
                TestDirectory directory,
                SignedPackageArchive package,
                PrimarySignature primarySignature,
                SignedPackageVerifierSettings settings)
            {
                _directory = directory;
                Package = package;
                PrimarySignature = primarySignature;
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
                    var primarySignature = await package.GetPrimarySignatureAsync(CancellationToken.None);

                    return new GetTrustResultAsyncTest(directory, package, primarySignature, settings);
                }
            }
        }
    }
}
#endif