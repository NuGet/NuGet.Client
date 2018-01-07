// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.X509.Store;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection("Signing Functional Test Collection")]
    public class SignatureTrustAndValidityVerificationProviderTests
    {
        private SigningSpecifications _specification => SigningSpecifications.V1;

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private IList<ISignatureVerificationProvider> _trustProviders;

        public SignatureTrustAndValidityVerificationProviderTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new SignatureTrustAndValidityVerificationProvider()
            };
        }

        [CIOnlyFact]
        public async Task VerifySignedPackage_ValidCertificate_Success()
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
        public async Task VerifySignedPackage_ValidCertificateAndTimestamp_Success()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedAndTimeStampedPackageAsync(testCertificate, nupkg, dir);
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
        public async Task GetTrustResultAsync_WithInvalidSignature_Throws()
        {
            var package = new SimpleTestPackageContext();

            using (var directory = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var packageFilePath = await SignedArchiveTestUtility.CreateSignedAndTimeStampedPackageAsync(testCertificate, package, directory);

                using (var packageReader = new PackageArchiveReader(packageFilePath))
                {
                    var signature = (await packageReader.GetSignaturesAsync(CancellationToken.None)).Single();
                    var invalidSignature = GenerateInvalidSignature(signature);
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
        public async Task VerifySignedPackage_ValidCertificateChain_Success()
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
        public async Task GetTrustResultAsync_WithNoSigningCertificate_Throws()
        {
            var package = new SimpleTestPackageContext();

            using (var directory = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var packageFilePath = await SignedArchiveTestUtility.CreateSignedAndTimeStampedPackageAsync(testCertificate, package, directory);

                using (var packageReader = new PackageArchiveReader(packageFilePath))
                {
                    var signature = (await packageReader.GetSignaturesAsync(CancellationToken.None)).Single();
                    var signatureWithNoCertificates = GenerateSignatureWithNoCertificates(signature);
                    var provider = new SignatureTrustAndValidityVerificationProvider();

                    var result = await provider.GetTrustResultAsync(
                        packageReader,
                        signatureWithNoCertificates,
                        SignedPackageVerifierSettings.Default,
                        CancellationToken.None);

                    var issue = result.Issues.FirstOrDefault(log => log.Code == NuGetLogCode.NU3010);

                    Assert.NotNull(issue);
                    Assert.Equal("The primary signature does not have a signing certificate.", issue.Message);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignedPackage_SettingsRequireExactlyOneTimestamp_MultipleTimestamps_Fails()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();
            var setting = new SignedPackageVerifierSettings(allowUnsigned: false, allowUntrusted: false, allowIgnoreTimestamp: false, failWithMultupleTimestamps: true, allowNoTimestamp: false);
            var signatureProvider = new X509SignatureProvider(timestampProvider: null);
            var timestampProvider = new Rfc3161TimestampProvider(new Uri(_testFixture.Timestamper));
            var verificationProvider = new SignatureTrustAndValidityVerificationProvider();

            using (var package = new PackageArchiveReader(nupkg.CreateAsStream(), leaveStreamOpen: false))
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var signatureRequest = new SignPackageRequest() { Certificate = testCertificate, SignatureType = SignatureType.Author })
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
        public async Task VerifySignedPackage_SettingsRequireTimestamp_NoTimestamp_Fails()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var setting = new SignedPackageVerifierSettings(allowUnsigned: false, allowUntrusted: false, allowIgnoreTimestamp: false, failWithMultupleTimestamps: false, allowNoTimestamp: false);

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

        private static Signature GenerateSignatureWithNoCertificates(Signature signature)
        {
            var certificateStore = X509StoreFactory.Create(
                "Certificate/Collection",
                new X509CollectionStoreParameters(Array.Empty<Org.BouncyCastle.X509.X509Certificate>()));
            var crlStore = X509StoreFactory.Create(
                "CRL/Collection",
                new X509CollectionStoreParameters(Array.Empty<Org.BouncyCastle.X509.X509Crl>()));
            var bytes = signature.SignedCms.Encode();

            using (var readStream = new MemoryStream(bytes))
            using (var writeStream = new MemoryStream())
            {
                CmsSignedDataParser.ReplaceCertificatesAndCrls(
                    readStream,
                    certificateStore,
                    crlStore,
                    certificateStore,
                    writeStream);

                return Signature.Load(writeStream.ToArray());
            }
        }

        private static Signature GenerateInvalidSignature(Signature signature)
        {
            var hash = Encoding.UTF8.GetBytes(signature.SignatureContent.HashValue);
            var newHash = Encoding.UTF8.GetBytes(new string('0', hash.Length));

            var bytes = signature.SignedCms.Encode();
            var newBytes = FindAndReplaceSequence(bytes, hash, newHash);

            return Signature.Load(newBytes);
        }

        private static byte[] FindAndReplaceSequence(byte[] bytes, byte[] find, byte[] replace)
        {
            var found = false;
            var from = -1;

            for (var i = 0; !found && i < bytes.Length - find.Length; ++i)
            {
                for (var j = 0; j < find.Length; ++j)
                {
                    if (bytes[i + j] != find[j])
                    {
                        break;
                    }

                    if (j == find.Length - 1)
                    {
                        from = i;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                throw new Exception("Byte sequence not found.");
            }

            var byteList = new List<byte>(bytes);

            byteList.RemoveRange(from, find.Length);
            byteList.InsertRange(from, replace);

            return byteList.ToArray();
        }
    }
}
#endif