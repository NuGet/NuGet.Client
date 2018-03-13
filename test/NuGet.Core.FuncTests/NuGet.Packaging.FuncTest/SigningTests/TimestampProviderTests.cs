// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.X509;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection(SigningTestCollection.Name)]
    public class TimestampProviderTests
    {
        private const string _argumentNullExceptionMessage = "Value cannot be null.\r\nParameter name: {0}";
        private const string _operationCancelledExceptionMessage = "The operation was canceled.";

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;

        public TimestampProviderTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
        }

        [CIOnlyFact]
        public async Task GetEncodedTimestampCmsForData_WithValidInput_ReturnsTimestamp()
        {
            var logger = new TestLogger();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var content = new SignatureContent(SigningSpecifications.V1, Common.HashAlgorithmName.SHA256, "Test data to be signed and timestamped");
            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, content.GetBytes());
                var primarySignature = PrimarySignature.Load(signedCms.Encode());
                var timestampHashAlgorithm = Common.HashAlgorithmName.SHA256;
                var signatureValue = primarySignature.GetSignatureValue();
                var messageHash = timestampHashAlgorithm.ComputeHash(signatureValue);

                var request = new TimestampRequest(
                    signingSpec: SigningSpecifications.V1,
                    signatureMessageHash: messageHash,
                    timestampHashAlgorithm: timestampHashAlgorithm,
                    timestampSignaturePlacement: SignaturePlacement.PrimarySignature
                );

                // Act
                var timestampedData = timestampProvider.GetEncodedTimestampCmsForData(request, logger, CancellationToken.None);
                var timestampedCms = new SignedCms();
                timestampedCms.Decode(timestampedData);

                // Assert
                timestampedData.Should().NotBeNull();
                timestampedCms.Should().NotBeNull();
                timestampedCms.Detached.Should().BeFalse();
                timestampedCms.ContentInfo.Should().NotBeNull();
                timestampedCms.SignerInfos.Count.Should().Be(1);
            }
        }

        [CIOnlyFact]
        public async Task GetEncodedTimestampCmsForData_AssertCompleteChain_Success()
        {
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var nupkg = new SimpleTestPackageContext();

            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var packageStream = nupkg.CreateAsStream())
            {
                // Act
                var signature = await SignedArchiveTestUtility.CreatePrimarySignatureForPackageAsync(authorCert, packageStream, timestampProvider);
                var authorSignedCms = signature.SignedCms;
                var timestamp = signature.Timestamps.First();
                var timestampCms = timestamp.SignedCms;
                IReadOnlyList<X509Certificate2> chainCertificates = new List<X509Certificate2>();
                var chainBuildSuccess = true;

                // rebuild the chain to get the list of certificates
                using (var chainHolder = new X509ChainHolder())
                {
                    var chain = chainHolder.Chain;
                    var policy = chain.ChainPolicy;

                    policy.ApplicationPolicy.Add(new Oid(Oids.TimeStampingEku));
                    policy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid | X509VerificationFlags.IgnoreCtlNotTimeValid;
                    policy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                    policy.RevocationMode = X509RevocationMode.Online;

                    var timestampSignerCertificate = timestampCms.SignerInfos[0].Certificate;
                    chainBuildSuccess = chain.Build(timestampSignerCertificate);
                    chainCertificates = CertificateChainUtility.GetCertificateListFromChain(chain);
                }

                // Assert
                authorSignedCms.Should().NotBeNull();
                authorSignedCms.Detached.Should().BeFalse();
                authorSignedCms.ContentInfo.Should().NotBeNull();
                authorSignedCms.SignerInfos.Count.Should().Be(1);
                authorSignedCms.SignerInfos[0].UnsignedAttributes.Count.Should().Be(1);
                authorSignedCms.SignerInfos[0].UnsignedAttributes[0].Oid.Value.Should().Be(Oids.SignatureTimeStampTokenAttribute);

                timestampCms.Should().NotBeNull();
                timestampCms.Detached.Should().BeFalse();
                timestampCms.ContentInfo.Should().NotBeNull();

                chainBuildSuccess.Should().BeTrue();
                chainCertificates.Count.Should().Be(timestampCms.Certificates.Count);
                foreach (var cert in chainCertificates)
                {
                    timestampCms.Certificates.Contains(cert).Should().BeTrue();
                }
            }
        }

        [CIOnlyFact]
        public async Task GetEncodedTimestampCmsForData_WhenRequestNull_Throws()
        {
            var logger = new TestLogger();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var authorCertName = "author@nuget.func.test";
            var content = new SignatureContent(SigningSpecifications.V1, Common.HashAlgorithmName.SHA256, "Test data to be signed and timestamped");

            Action<X509V3CertificateGenerator> modifyGenerator = delegate (X509V3CertificateGenerator gen)
            {
                gen.SetNotBefore(DateTime.MinValue);
                gen.SetNotBefore(DateTime.UtcNow.Subtract(TimeSpan.FromDays(1))); // cert has expired
            };

            using (var authorCert = SigningTestUtility.GenerateCertificate(authorCertName, modifyGenerator: modifyGenerator))
            {
                var timestampHashAlgorithm = Common.HashAlgorithmName.SHA256;
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, content.GetBytes());
                var signature = PrimarySignature.Load(signedCms.Encode());
                var signatureValue = signature.GetSignatureValue();
                var messageHash = timestampHashAlgorithm.ComputeHash(signatureValue);

                var request = new TimestampRequest(
                    signingSpec: SigningSpecifications.V1,
                    signatureMessageHash: messageHash,
                    timestampHashAlgorithm: timestampHashAlgorithm,
                    timestampSignaturePlacement: SignaturePlacement.PrimarySignature
                );

                // Act
                Action timestampAction = () => timestampProvider.GetEncodedTimestampCmsForData(null, logger, CancellationToken.None);

                // Assert
                timestampAction.ShouldThrow<ArgumentNullException>()
                    .WithMessage(string.Format(_argumentNullExceptionMessage, nameof(request)));
            }
        }

        [CIOnlyFact]
        public async Task GetEncodedTimestampCmsForData_WhenLoggerNull_Throws()
        {
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var content = new SignatureContent(SigningSpecifications.V1, Common.HashAlgorithmName.SHA256, "Test data to be signed and timestamped");

            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var timestampHashAlgorithm = Common.HashAlgorithmName.SHA256;
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, content.GetBytes());
                var signature = PrimarySignature.Load(signedCms.Encode());
                var signatureValue = signature.GetSignatureValue();
                var messageHash = timestampHashAlgorithm.ComputeHash(signatureValue);

                var request = new TimestampRequest(
                    signingSpec: SigningSpecifications.V1,
                    signatureMessageHash: messageHash,
                    timestampHashAlgorithm: timestampHashAlgorithm,
                    timestampSignaturePlacement: SignaturePlacement.PrimarySignature
                );

                // Act
                Action timestampAction = () => timestampProvider.GetEncodedTimestampCmsForData(request, null, CancellationToken.None);

                // Assert
                timestampAction.ShouldThrow<ArgumentNullException>()
                    .WithMessage(string.Format(_argumentNullExceptionMessage, "logger"));
            }
        }

        [CIOnlyFact]
        public async Task GetEncodedTimestampCmsForData_WhenCancelled_Throws()
        {
            var logger = new TestLogger();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var content = new SignatureContent(SigningSpecifications.V1, Common.HashAlgorithmName.SHA256, "Test data to be signed and timestamped");

            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var timestampHashAlgorithm = Common.HashAlgorithmName.SHA256;
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, content.GetBytes());
                var signature = PrimarySignature.Load(signedCms.Encode());
                var signatureValue = signature.GetSignatureValue();
                var messageHash = timestampHashAlgorithm.ComputeHash(signatureValue);

                var request = new TimestampRequest(
                   signingSpec: SigningSpecifications.V1,
                   signatureMessageHash: messageHash,
                   timestampHashAlgorithm: timestampHashAlgorithm,
                   timestampSignaturePlacement: SignaturePlacement.PrimarySignature
               );

                // Act
                Action timestampAction = () => timestampProvider.GetEncodedTimestampCmsForData(request, logger, new CancellationToken(canceled: true));

                // Assert
                timestampAction.ShouldThrow<OperationCanceledException>()
                    .WithMessage(_operationCancelledExceptionMessage);
            }
        }

        [CIOnlyFact]
        public async Task GetEncodedTimestampCmsForData_WhenRevocationInformationUnavailable_Success()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var ca2 = ca.CreateIntermediateCertificateAuthority();
            var timestampService = TimestampService.Create(ca2);

            // Only register the AIA responder.  Do not register the OCSP responder.
            using (testServer.RegisterResponder(ca2))
            {
                VerifyTimestampData(
                    testServer,
                    timestampService,
                    (timestampProvider, request) =>
                    {
                        var logger = new TestLogger();
                        var timestamp = timestampProvider.GetEncodedTimestampCmsForData(request, logger, CancellationToken.None);

                        Assert.NotEmpty(timestamp);

                        Assert.Equal(0, logger.Errors);
                        Assert.Equal(2, logger.Warnings);

                        AssertOfflineRevocation(logger.LogMessages, LogLevel.Warning);
                        AssertRevocationStatusUnknown(logger.LogMessages, LogLevel.Warning);
                    });
            }
        }

        [CIOnlyFact]
        public async Task GetEncodedTimestampCmsForData_WhenTimestampSigningCertificateRevoked_Throws()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var certificateAuthority = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var timestampService = TimestampService.Create(certificateAuthority);

            certificateAuthority.Revoke(
                timestampService.Certificate,
                RevocationReason.KeyCompromise,
                DateTimeOffset.UtcNow);

            VerifyTimestampData(
                testServer,
                timestampService,
                (timestampProvider, request) =>
                {
                    var exception = Assert.Throws<TimestampException>(
                        () => timestampProvider.GetEncodedTimestampCmsForData(request, NullLogger.Instance, CancellationToken.None));

                    Assert.Equal("Certificate chain validation failed.", exception.Message);
                });
        }

        [CIOnlyFact]
        public async Task GetEncodedTimestampCmsForData_WithFailureReponse_Throws()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var certificateAuthority = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var options = new TimestampServiceOptions() { ReturnFailure = true };
            var timestampService = TimestampService.Create(certificateAuthority, options);

            VerifyTimestampData(
                testServer,
                timestampService,
                (timestampProvider, request) =>
                {
                    var exception = Assert.Throws<CryptographicException>(
                        () => timestampProvider.GetEncodedTimestampCmsForData(request, NullLogger.Instance, CancellationToken.None));

                    Assert.StartsWith(
                        "The timestamp signature and/or certificate could not be verified or is malformed.",
                        exception.Message);
                });
        }

        [CIOnlyFact]
        public async Task GetEncodedTimestampCmsForData_WhenSigningCertificateNotReturned_Throws()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var certificateAuthority = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var options = new TimestampServiceOptions() { ReturnSigningCertificate = false };
            var timestampService = TimestampService.Create(certificateAuthority, options);

            VerifyTimestampData(
                testServer,
                timestampService,
                (timestampProvider, request) =>
                {
                    var exception = Assert.Throws<CryptographicException>(
                        () => timestampProvider.GetEncodedTimestampCmsForData(request, NullLogger.Instance, CancellationToken.None));

                    Assert.StartsWith("Cannot find object or property.", exception.Message);
                });
        }

        [CIOnlyFact]
        public async Task GetEncodedTimestampCmsForData_WhenSignatureHashAlgorithmIsSha1_Throws()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var certificateAuthority = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var options = new TimestampServiceOptions() { SignatureHashAlgorithm = new Oid(Oids.Sha1) };
            var timestampService = TimestampService.Create(certificateAuthority, options);

            VerifyTimestampData(
                testServer,
                timestampService,
                (timestampProvider, request) =>
                {
                    var exception = Assert.Throws<TimestampException>(
                        () => timestampProvider.GetEncodedTimestampCmsForData(request, NullLogger.Instance, CancellationToken.None));

                    Assert.Equal(
                        "The timestamp certificate has an unsupported signature algorithm.",
                        exception.Message);
                });
        }

        [CIOnlyFact]
        public async Task TimestampSignatureAsync_TimestampingPrimarySignature_Succeds()
        {
            var logger = new TestLogger();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var signatureContent = new SignatureContent(SigningSpecifications.V1, Common.HashAlgorithmName.SHA256, "Test data to be signed and timestamped");

            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var timestampHashAlgorithm = Common.HashAlgorithmName.SHA256;
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, signatureContent.GetBytes());
                var signature = PrimarySignature.Load(signedCms.Encode());
                var signatureValue = signature.GetSignatureValue();
                var messageHash = timestampHashAlgorithm.ComputeHash(signatureValue);

                var request = new TimestampRequest(
                   signingSpec: SigningSpecifications.V1,
                   signatureMessageHash: messageHash,
                   timestampHashAlgorithm: timestampHashAlgorithm,
                   timestampSignaturePlacement: SignaturePlacement.PrimarySignature
               );

                // Act
                var primarySignature = await timestampProvider.TimestampSignatureAsync(signature, request, logger, CancellationToken.None);

                // Assert
                primarySignature.Should().NotBeNull();
                primarySignature.SignedCms.Should().NotBeNull();
                primarySignature.SignerInfo.Should().NotBeNull();
                primarySignature.SignerInfo.UnsignedAttributes.Count.Should().BeGreaterOrEqualTo(1);

                var hasTimestampUnsignedAttribute = false;
                foreach (var attr in primarySignature.SignerInfo.UnsignedAttributes)
                {
                    if (string.Compare(attr.Oid.Value, Oids.SignatureTimeStampTokenAttribute, CultureInfo.CurrentCulture, CompareOptions.Ordinal) == 0)
                    {
                        hasTimestampUnsignedAttribute = true;
                    }
                }
                hasTimestampUnsignedAttribute.Should().BeTrue();
            }
        }

        [CIOnlyFact]
        public async Task TimestampSignatureAsync_TimestampingCountersignature_Succeds()
        {
            var logger = new TestLogger();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var signatureContent = new SignatureContent(SigningSpecifications.V1, Common.HashAlgorithmName.SHA256, "Test data to be signed and timestamped");

            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var timestampHashAlgorithm = Common.HashAlgorithmName.SHA256;
                var signedCms = SigningTestUtility.GenerateRepositoryCountersignedSignedCms(authorCert, signatureContent.GetBytes());
                var signature = PrimarySignature.Load(signedCms.Encode());
                var signatureValue = signature.GetSignatureValue();
                var messageHash = timestampHashAlgorithm.ComputeHash(signatureValue);

                var request = new TimestampRequest(
                   signingSpec: SigningSpecifications.V1,
                   signatureMessageHash: messageHash,
                   timestampHashAlgorithm: timestampHashAlgorithm,
                   timestampSignaturePlacement: SignaturePlacement.Countersignature
               );

                // Act
                var primarySignature = await timestampProvider.TimestampSignatureAsync(signature, request, logger, CancellationToken.None);

                var repositoryCountersignature = RepositoryCountersignature.GetRepositoryCountersignature(primarySignature);

                // Assert
                repositoryCountersignature.Should().NotBeNull();
                repositoryCountersignature.SignerInfo.Should().NotBeNull();
                repositoryCountersignature.SignerInfo.UnsignedAttributes.Count.Should().BeGreaterOrEqualTo(1);

                var hasTimestampUnsignedAttribute = false;
                foreach (var attr in repositoryCountersignature.SignerInfo.UnsignedAttributes)
                {
                    if (string.Compare(attr.Oid.Value, Oids.SignatureTimeStampTokenAttribute, CultureInfo.CurrentCulture, CompareOptions.Ordinal) == 0)
                    {
                        hasTimestampUnsignedAttribute = true;
                    }
                }
                hasTimestampUnsignedAttribute.Should().BeTrue();
            }
        }


        private void VerifyTimestampData(
            ISigningTestServer testServer,
            TimestampService timestampService,
            Action<Rfc3161TimestampProvider, TimestampRequest> verifyTimestampData)
        {
            using (testServer.RegisterResponder(timestampService))
            {
                var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);

                using (var certificate = new X509Certificate2(_trustedTestCert.Source.Cert))
                {
                    var timestampHashAlgorithm = Common.HashAlgorithmName.SHA256;
                    var content = new SignatureContent(SigningSpecifications.V1, Common.HashAlgorithmName.SHA256, "peach");
                    var signedCms = SigningTestUtility.GenerateSignedCms(certificate, content.GetBytes());
                    var signature = PrimarySignature.Load(signedCms.Encode());
                    var signatureValue = signature.GetSignatureValue();
                    var messageHash = timestampHashAlgorithm.ComputeHash(signatureValue);

                    var request = new TimestampRequest(
                       signingSpec: SigningSpecifications.V1,
                       signatureMessageHash: messageHash,
                       timestampHashAlgorithm: timestampHashAlgorithm,
                       timestampSignaturePlacement: SignaturePlacement.PrimarySignature
                   );

                    verifyTimestampData(timestampProvider, request);
                }
            }
        }

        private static void AssertOfflineRevocation(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3028 &&
                issue.Level == logLevel &&
                issue.Message == "The revocation function was unable to check revocation because the revocation server was offline.");
        }

        private static void AssertRevocationStatusUnknown(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3028 &&
                issue.Level == logLevel &&
                issue.Message == "The revocation function was unable to check revocation for the certificate.");
        }
    }
}
#endif