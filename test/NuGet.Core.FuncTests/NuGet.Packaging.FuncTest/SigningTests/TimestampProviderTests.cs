// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using FluentAssertions;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.X509;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection("Signing Functional Test Collection")]
    public class TimestampProviderTests
    {
        private static readonly string _testTimestampServer = Environment.GetEnvironmentVariable("TIMESTAMP_SERVER_URL");
        private const string _authorCertExpiredExceptionMessage = "Author certificate was not valid when it was timestamped.";
        private const string _argumentNullExceptionMessage = "Value cannot be null.\r\nParameter name: {0}";
        private const string _operationCancelledExceptionMessage = "The operation was canceled.";

        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private SigningSpecifications _signingSpecifications;

        public TimestampProviderTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _signingSpecifications = _testFixture.SigningSpecifications;
        }

        [CIOnlyFact]
        public void Rfc3161TimestampProvider_Success()
        {
            // Arrange
            var logger = new TestLogger();
            var timestampProvider = new Rfc3161TimestampProvider(new Uri(_testTimestampServer));
            var data = "Test data to be signed and timestamped";

            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, Encoding.ASCII.GetBytes(data));
                var signatureValue = signedCms.Encode();

                var request = new TimestampRequest
                {
                    Certificate = authorCert,
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
                timestampedCms.Certificates.Count.Should().Be(1);
                timestampedCms.SignerInfos.Count.Should().Be(1);
                timestampedCms.SignerInfos[0].UnsignedAttributes.Count.Should().Be(1);
                timestampedCms.SignerInfos[0].UnsignedAttributes[0].Oid.Value.Should().Be(Oids.SignatureTimeStampTokenAttribute);

            }
        }

        [CIOnlyFact]
        public void Rfc3161TimestampProvider_Failure_NullRequest()
        {
            // Arrange
            var logger = new TestLogger();
            var timestampProvider = new Rfc3161TimestampProvider(new Uri(_testTimestampServer));
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
                    Certificate = authorCert,
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
        public void Rfc3161TimestampProvider_Failure_NullLogger()
        {
            // Arrange
            var logger = new TestLogger();
            var timestampProvider = new Rfc3161TimestampProvider(new Uri(_testTimestampServer));
            var data = "Test data to be signed and timestamped";

            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, Encoding.ASCII.GetBytes(data));
                var signatureValue = signedCms.Encode();

                var request = new TimestampRequest
                {
                    Certificate = authorCert,
                    SigningSpec = SigningSpecifications.V1,
                    TimestampHashAlgorithm = Common.HashAlgorithmName.SHA256,
                    SignatureValue = signatureValue
                };

                // Act
                Action timestampAction = () => timestampProvider.TimestampData(request, null, CancellationToken.None);

                // Assert
                timestampAction.ShouldThrow<ArgumentNullException>()
                    .WithMessage(string.Format(_argumentNullExceptionMessage, nameof(logger)));
            }
        }

        [CIOnlyFact]
        public void Rfc3161TimestampProvider_Failure_Cancelled()
        {
            // Arrange
            var logger = new TestLogger();
            var timestampProvider = new Rfc3161TimestampProvider(new Uri(_testTimestampServer));
            var data = "Test data to be signed and timestamped";

            using (var authorCert = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, Encoding.ASCII.GetBytes(data));
                var signatureValue = signedCms.Encode();

                var request = new TimestampRequest
                {
                    Certificate = authorCert,
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
    }
}
#endif