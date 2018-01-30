// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection("Signing Functional Test Collection")]
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
        public async Task TimestampData_WithValidInput_ReturnsTimestamp()
        {
            var logger = new TestLogger();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var data = "Test data to be signed and timestamped";

            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, Encoding.ASCII.GetBytes(data));
                var signatureValue = signedCms.Encode();

                var request = new TimestampRequest
                {
                    SigningSpec = SigningSpecifications.V1,
                    TimestampHashAlgorithm = Common.HashAlgorithmName.SHA256,
                    SignatureValue = signatureValue
                };

                // Act
                var timestampedData = timestampProvider.TimestampData(request, logger, CancellationToken.None);
                var timestampedCms = new SignedCms();
                timestampedCms.Decode(timestampedData);

                // Assert
                timestampedData.Should().NotBeNull();
                timestampedCms.Should().NotBeNull();
                timestampedCms.Detached.Should().BeFalse();
                timestampedCms.ContentInfo.Should().NotBeNull();
                timestampedCms.SignerInfos.Count.Should().Be(1);
                timestampedCms.SignerInfos[0].UnsignedAttributes.Count.Should().Be(1);
                timestampedCms.SignerInfos[0].UnsignedAttributes[0].Oid.Value.Should().Be(Oids.SignatureTimeStampTokenAttribute);
            }
        }

        [CIOnlyFact]
        public async Task TimestampData_AssertCompleteChain_Success()
        {
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var nupkg = new SimpleTestPackageContext();

            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            using (var packageStream = nupkg.CreateAsStream())
            {
                // Act
                var signature = await SignedArchiveTestUtility.CreateSignatureForPackageAsync(authorCert, packageStream, timestampProvider);
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
        public async Task TimestampData_WhenRequestNull_Throws()
        {
            var logger = new TestLogger();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var authorCertName = "author@nuget.func.test";
            var data = "Test data to be signed and timestamped";

            Action<X509V3CertificateGenerator> modifyGenerator = delegate (X509V3CertificateGenerator gen)
            {
                gen.SetNotBefore(DateTime.MinValue);
                gen.SetNotBefore(DateTime.UtcNow.Subtract(TimeSpan.FromDays(1))); // cert has expired
            };

            using (var authorCert = SigningTestUtility.GenerateCertificate(authorCertName, modifyGenerator: modifyGenerator))
            {
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, Encoding.ASCII.GetBytes(data));
                var signatureValue = signedCms.Encode();

                var request = new TimestampRequest
                {
                    SigningSpec = SigningSpecifications.V1,
                    TimestampHashAlgorithm = Common.HashAlgorithmName.SHA256,
                    SignatureValue = signatureValue
                };

                // Act
                Action timestampAction = () => timestampProvider.TimestampData(null, logger, CancellationToken.None);

                // Assert
                timestampAction.ShouldThrow<ArgumentNullException>()
                    .WithMessage(string.Format(_argumentNullExceptionMessage, nameof(request)));
            }
        }

        [CIOnlyFact]
        public async Task TimestampData_WhenLoggerNull_Throws()
        {
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var data = "Test data to be signed and timestamped";

            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, Encoding.ASCII.GetBytes(data));
                var signatureValue = signedCms.Encode();

                var request = new TimestampRequest
                {
                    SigningSpec = SigningSpecifications.V1,
                    TimestampHashAlgorithm = Common.HashAlgorithmName.SHA256,
                    SignatureValue = signatureValue
                };

                // Act
                Action timestampAction = () => timestampProvider.TimestampData(request, null, CancellationToken.None);

                // Assert
                timestampAction.ShouldThrow<ArgumentNullException>()
                    .WithMessage(string.Format(_argumentNullExceptionMessage, "logger"));
            }
        }

        [CIOnlyFact]
        public async Task TimestampData_WhenCancelled_Throws()
        {
            var logger = new TestLogger();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
            var data = "Test data to be signed and timestamped";

            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, Encoding.ASCII.GetBytes(data));
                var signatureValue = signedCms.Encode();

                var request = new TimestampRequest
                {
                    SigningSpec = SigningSpecifications.V1,
                    TimestampHashAlgorithm = Common.HashAlgorithmName.SHA256,
                    SignatureValue = signatureValue
                };

                // Act
                Action timestampAction = () => timestampProvider.TimestampData(request, logger, new CancellationToken(canceled: true));

                // Assert
                timestampAction.ShouldThrow<OperationCanceledException>()
                    .WithMessage(_operationCancelledExceptionMessage);
            }
        }

        [CIOnlyFact]
        public async Task TimestampData_WhenTimestampSigningCertificateRevoked_Throws()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var certificateAuthority = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var timestampService = TimestampService.Create(certificateAuthority);

            certificateAuthority.Revoke(timestampService.Certificate, CrlReason.KeyCompromise, DateTimeOffset.UtcNow);

            using (testServer.RegisterResponder(timestampService))
            {
                var logger = new TestLogger();
                var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);
                var data = "Test data to be signed and timestamped";

                using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
                {
                    var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, Encoding.ASCII.GetBytes(data));
                    var signatureValue = signedCms.Encode();

                    var request = new TimestampRequest
                    {
                        SigningSpec = SigningSpecifications.V1,
                        TimestampHashAlgorithm = Common.HashAlgorithmName.SHA256,
                        SignatureValue = signatureValue
                    };

                    var exception = Assert.Throws<TimestampException>(
                        () => timestampProvider.TimestampData(request, logger, CancellationToken.None));

                    Assert.Equal(
                        "The timestamp service's certificate chain could not be built: The certificate is revoked.",
                        exception.Message);
                }
            }
        }
    }
}
#endif