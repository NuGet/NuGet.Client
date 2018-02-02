// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SigningUtilityTests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public SigningUtilityTests(CertificatesFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
        }

        [Fact]
        public void Verify_WhenRequestNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => SigningUtility.Verify(request: null, logger: NullLogger.Instance));

            Assert.Equal("request", exception.ParamName);
        }

        [Fact]
        public void Verify_WhenLoggerNull_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => SigningUtility.Verify(request, logger: null));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public void Verify_WithCertificateWithUnsupportedSignatureAlgorithm_Throws()
        {
            using (var certificate = _fixture.GetRsaSsaPssCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<SignatureException>(
                    () => SigningUtility.Verify(request, NullLogger.Instance));

                Assert.Equal(NuGetLogCode.NU3013, exception.Code);
                Assert.Equal("The signing certificate has an unsupported signature algorithm.", exception.Message);
            }
        }

        [Fact]
        public void Verify_WithCertificateWithLifetimeSigningEku_Throws()
        {
            using (var certificate = _fixture.GetLifetimeSigningCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<SignatureException>(
                    () => SigningUtility.Verify(request, NullLogger.Instance));

                Assert.Equal(NuGetLogCode.NU3015, exception.Code);
                Assert.Equal("The lifetime signing EKU is not supported.", exception.Message);
            }
        }

        [Fact]
        public void Verify_WithNotYetValidCertificate_Throws()
        {
            using (var certificate = _fixture.GetNotYetValidCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<SignatureException>(
                    () => SigningUtility.Verify(request, NullLogger.Instance));

                Assert.Equal(NuGetLogCode.NU3017, exception.Code);
                Assert.Equal("The signing certificate is not yet valid.", exception.Message);
            }
        }

        [Fact]
        public void Verify_WhenChainBuildingFails_Throws()
        {
            using (var certificate = _fixture.GetExpiredCertificate())
            using (var request = CreateRequest(certificate))
            {
                var logger = new TestLogger();

                var exception = Assert.Throws<SignatureException>(
                    () => SigningUtility.Verify(request, logger));

                Assert.Equal(NuGetLogCode.NU3018, exception.Code);
                Assert.Equal("Certificate chain validation failed.", exception.Message);

                Assert.Equal(RuntimeEnvironmentHelper.IsWindows ? 1 : 2, logger.Errors);
                Assert.Equal(1, logger.Warnings);

                if (RuntimeEnvironmentHelper.IsWindows)
                {
                    AssertUntrustedRoot(logger.LogMessages, LogLevel.Warning);
                    AssertNotTimeValid(logger.LogMessages, LogLevel.Error);
                }
                else
                {
                    AssertUntrustedRoot(logger.LogMessages, LogLevel.Error);
                    AssertPartialChain(logger.LogMessages, LogLevel.Error);
                    AssertOfflineRevocation(logger.LogMessages, LogLevel.Warning);
                }
            }
        }

        [Fact]
        public void Verify_WithUntrustedSelfSignedCertificate_Succeeds()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var logger = new TestLogger();

                SigningUtility.Verify(request, logger);

                Assert.Equal(0, logger.Errors);
                Assert.Equal(RuntimeEnvironmentHelper.IsWindows ? 1 : 2, logger.Warnings);

                AssertUntrustedRoot(logger.LogMessages, LogLevel.Warning);

                if (!RuntimeEnvironmentHelper.IsWindows)
                {
                    AssertOfflineRevocation(logger.LogMessages, LogLevel.Warning);
                }
            }
        }

#if IS_DESKTOP
        [Fact]
        public void CreateSignedAttributes_WhenRequestNull_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => SigningUtility.CreateSignedAttributes(
                        request: null,
                        chainList: new List<X509Certificate2>() { certificate }));

                Assert.Equal("request", exception.ParamName);
            }
        }

        [Fact]
        public void CreateSignedAttributes_WhenChainListNull_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => SigningUtility.CreateSignedAttributes(request, chainList: null));

                Assert.Equal("chainList", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }
        }

        [Fact]
        public void CreateSignedAttributes_WhenChainListEmpty_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => SigningUtility.CreateSignedAttributes(request, new List<X509Certificate2>()));

                Assert.Equal("chainList", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }
        }

        [Fact]
        public void CreateSignedAttributes_WithValidInput_ReturnsAttributes()
        {
            using (var rootCertificate = SignTestUtility.GetCertificate("root.crt"))
            using (var intermediateCertificate = SignTestUtility.GetCertificate("intermediate.crt"))
            using (var leafCertificate = SignTestUtility.GetCertificate("leaf.crt"))
            using (var request = CreateRequest(leafCertificate))
            {
                var certList = new List<X509Certificate2>() { leafCertificate, intermediateCertificate, rootCertificate };
                var attributes = SigningUtility.CreateSignedAttributes(request, certList);

                Assert.Equal(3, attributes.Count);

                var pkcs9SigningTimeAttributeFound = false;
                var commitmentTypeIndicationAttributeFound = false;
                var signingCertificateV2AttributeFound = false;

                foreach (var attribute in attributes)
                {
                    Assert.Equal(1, attribute.Values.Count);

                    switch (attribute.Oid.Value)
                    {
                        case "1.2.840.113549.1.9.5": // PKCS #9 signing time
                            Assert.IsType<Pkcs9SigningTime>(attribute.Values[0]);

                            pkcs9SigningTimeAttributeFound = true;
                            break;

                        case Oids.CommitmentTypeIndication:
                            var qualifier = CommitmentTypeQualifier.Read(attribute.Values[0].RawData);

                            Assert.Equal(Oids.CommitmentTypeIdentifierProofOfOrigin, qualifier.CommitmentTypeIdentifier.Value);

                            commitmentTypeIndicationAttributeFound = true;
                            break;

                        case Oids.SigningCertificateV2:
                            var signingCertificateV2 = SigningCertificateV2.Read(attribute.Values[0].RawData);

                            Assert.Equal(1, signingCertificateV2.Certificates.Count);

                            var essCertIdV2 = signingCertificateV2.Certificates[0];

                            Assert.Equal(SignTestUtility.GetHash(leafCertificate, request.SignatureHashAlgorithm), essCertIdV2.CertificateHash);
                            Assert.Equal(request.SignatureHashAlgorithm.ConvertToOidString(), essCertIdV2.HashAlgorithm.Algorithm.Value);
                            Assert.Equal(leafCertificate.IssuerName.Name, essCertIdV2.IssuerSerial.GeneralNames[0].DirectoryName.Name);
                            SignTestUtility.VerifySerialNumber(leafCertificate, essCertIdV2.IssuerSerial);
                            Assert.Null(signingCertificateV2.Policies);

                            signingCertificateV2AttributeFound = true;
                            break;

                        default:
                            break;
                    }
                }

                Assert.True(pkcs9SigningTimeAttributeFound);
                Assert.True(commitmentTypeIndicationAttributeFound);
                Assert.True(signingCertificateV2AttributeFound);
            }
        }
#endif

        private static void AssertUntrustedRoot(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            string untrustedRoot;

            if (RuntimeEnvironmentHelper.IsWindows)
            {
                untrustedRoot = "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.";
            }
            else
            {
                untrustedRoot = "certificate not trusted";
            }

            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == untrustedRoot);
        }

        private static void AssertNotTimeValid(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == "A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file.");
        }

        private static void AssertOfflineRevocation(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == "unable to get certificate CRL");
        }

        private static void AssertPartialChain(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == "unable to get local issuer certificate");
        }

        private static SignPackageRequest CreateRequest(X509Certificate2 certificate)
        {
            return new SignPackageRequest(certificate, HashAlgorithmName.SHA256, HashAlgorithmName.SHA256);
        }
    }
}