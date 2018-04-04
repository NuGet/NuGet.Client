// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;
using Org.BouncyCastle.Asn1.X509;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class CertificateUtilityTests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public CertificateUtilityTests(CertificatesFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
        }

        [Theory]
        [InlineData("SHA256WITHRSAENCRYPTION", Oids.Sha256WithRSAEncryption)]
        [InlineData("SHA384WITHRSAENCRYPTION", Oids.Sha384WithRSAEncryption)]
        [InlineData("SHA512WITHRSAENCRYPTION", Oids.Sha512WithRSAEncryption)]
        public void IsSignatureAlgorithmSupported_WhenSupported_ReturnsTrue(string signatureAlgorithm, string expectedSignatureAlgorithmOid)
        {
            using (var certificate = SigningTestUtility.GenerateCertificate(
                "test",
                generator => { },
                signatureAlgorithm))
            {
                Assert.Equal(expectedSignatureAlgorithmOid, certificate.SignatureAlgorithm.Value);
                Assert.True(CertificateUtility.IsSignatureAlgorithmSupported(certificate));
            }
        }

        [Fact]
        public void IsSignatureAlgorithmSupported_WhenUnsupported_ReturnsFalse()
        {
            using (var certificate = _fixture.GetRsaSsaPssCertificate())
            {
                // RSASSA-PSS
                Assert.Equal("1.2.840.113549.1.1.10", certificate.SignatureAlgorithm.Value);
                Assert.False(CertificateUtility.IsSignatureAlgorithmSupported(certificate));
            }
        }

        [Fact]
        public void IsCertificatePublicKeyValid_WhenNotRsaSsaPkcsV1_5_ReturnsTrue()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate(
                "test",
                generator => { },
                "SHA256WITHRSAANDMGF1",
                publicKeyLength: 2048))
            {
                Assert.True(CertificateUtility.IsCertificatePublicKeyValid(certificate));
            }
        }

        [Fact]
        public void IsCertificatePublicKeyValid_WhenRsaSsaPkcsV1_5_1024Bits_ReturnsFalse()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate(
                "test",
                generator => { },
                publicKeyLength: 1024))
            {
                Assert.False(CertificateUtility.IsCertificatePublicKeyValid(certificate));
            }
        }

        [Fact]
        public void IsCertificatePublicKeyValid_WhenRsaSsaPkcsV1_5_2048Bits_ReturnsTrue()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate(
                "test",
                generator => { },
                publicKeyLength: 2048))
            {
                Assert.True(CertificateUtility.IsCertificatePublicKeyValid(certificate));
            }
        }

        [Fact]
        public void IsCertificateValidityPeriodInTheFuture_WithValidityStartInTheFuture_ReturnsTrue()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate(
                "test",
                modifyGenerator: SigningTestUtility.CertificateModificationGeneratorNotYetValidCert))
            {
                Assert.True(CertificateUtility.IsCertificateValidityPeriodInTheFuture(certificate));
            }
        }

        [Fact]
        public void IsCertificateValidityPeriodInTheFuture_WithValidityStartInThePast_ReturnsFalse()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate(
                "test",
                generator => { }))
            {
                Assert.False(CertificateUtility.IsCertificateValidityPeriodInTheFuture(certificate));
            }
        }

        [Fact]
        public void GetCertificateChain_ReturnsCertificatesInOrder()
        {
            using (var chainHolder = new X509ChainHolder())
            using (var rootCertificate = SignTestUtility.GetCertificate("root.crt"))
            using (var intermediateCertificate = SignTestUtility.GetCertificate("intermediate.crt"))
            using (var leafCertificate = SignTestUtility.GetCertificate("leaf.crt"))
            {
                var chain = chainHolder.Chain;

                chain.ChainPolicy.ExtraStore.Add(rootCertificate);
                chain.ChainPolicy.ExtraStore.Add(intermediateCertificate);

                chain.Build(leafCertificate);

                using (var certificateChain = CertificateChainUtility.GetCertificateChain(chain))
                {
                    Assert.Equal(3, certificateChain.Count);
                    Assert.Equal(leafCertificate.Thumbprint, certificateChain[0].Thumbprint);
                    Assert.Equal(intermediateCertificate.Thumbprint, certificateChain[1].Thumbprint);
                    Assert.Equal(rootCertificate.Thumbprint, certificateChain[2].Thumbprint);
                }
            }
        }

        [Fact]
        public void HasLifetimeSigningEku_WithLifetimeSignerEku_ReturnsTrue()
        {
            using (var certificate = _fixture.GetLifetimeSigningCertificate())
            {
                Assert.Equal(1, GetExtendedKeyUsageCount(certificate));
                Assert.True(CertificateUtility.HasLifetimeSigningEku(certificate));
            }
        }

        [Fact]
        public void HasLifetimeSigningEku_WithoutLifetimeSignerEku_ReturnsFalse()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate("test", generator => { }))
            {
                Assert.Equal(0, GetExtendedKeyUsageCount(certificate));
                Assert.False(CertificateUtility.HasLifetimeSigningEku(certificate));
            }
        }

        [Fact]
        public void HasExtendedKeyUsage_WithEku_ReturnsTrueForEku()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate("test",
                generator =>
                {
                    generator.AddExtension(
                        X509Extensions.ExtendedKeyUsage.Id,
                        critical: false,
                        extensionValue: new ExtendedKeyUsage(KeyPurposeID.IdKPCodeSigning));
                }))
            {
                Assert.Equal(1, GetExtendedKeyUsageCount(certificate));
                Assert.True(CertificateUtility.HasExtendedKeyUsage(certificate, Oids.CodeSigningEku));
            }
        }

        [Fact]
        public void HasExtendedKeyUsage_WithoutEku_ReturnsFalseForEku()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate("test", generator => { }))
            {
                Assert.Equal(0, GetExtendedKeyUsageCount(certificate));
                Assert.False(CertificateUtility.HasExtendedKeyUsage(certificate, Oids.CodeSigningEku));
            }
        }

        [Fact]
        public void IsValidForPurposeFast_WithCodeSigningEku_ReturnsTrueForCodeSigningEku()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate("test",
                generator =>
                {
                    generator.AddExtension(
                        X509Extensions.ExtendedKeyUsage.Id,
                        critical: false,
                        extensionValue: new ExtendedKeyUsage(KeyPurposeID.IdKPCodeSigning));
                }))
            {
                Assert.Equal(1, GetExtendedKeyUsageCount(certificate));
                Assert.True(CertificateUtility.IsValidForPurposeFast(certificate, Oids.CodeSigningEku));
            }
        }

        [Fact]
        public void IsValidForPurposeFast_WithoutCodeSigningEku_ReturnsFalseForCodeSigningEku()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate("test",
                generator =>
                {
                    generator.AddExtension(
                        X509Extensions.ExtendedKeyUsage.Id,
                        critical: false,
                        extensionValue: new ExtendedKeyUsage(KeyPurposeID.IdKPEmailProtection));
                }))
            {
                Assert.Equal(1, GetExtendedKeyUsageCount(certificate));
                Assert.False(CertificateUtility.IsValidForPurposeFast(certificate, Oids.CodeSigningEku));
            }
        }

        [Fact]
        public void IsValidForPurposeFast_WithAnyExtendedKeyUsage_ReturnsFalseForCodeSigningEku()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate("test",
                generator =>
                {
                    generator.AddExtension(
                        X509Extensions.ExtendedKeyUsage.Id,
                        critical: false,
                        extensionValue: new ExtendedKeyUsage(KeyPurposeID.IdKPEmailProtection, KeyPurposeID.AnyExtendedKeyUsage));
                }))
            {
                Assert.Equal(2, GetExtendedKeyUsageCount(certificate));
                Assert.False(CertificateUtility.IsValidForPurposeFast(certificate, Oids.CodeSigningEku));
            }
        }

        [Fact]
        public void IsValidForPurposeFast_WithNoEku_ReturnsTrueForCodeSigningEku()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate("test", generator => { }))
            {
                Assert.Equal(0, GetExtendedKeyUsageCount(certificate));
                Assert.True(CertificateUtility.IsValidForPurposeFast(certificate, Oids.CodeSigningEku));
            }
        }

        [Fact]
        public void IsSelfIssued_WhenCertificateNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CertificateUtility.IsSelfIssued(certificate: null));

            Assert.Equal("certificate", exception.ParamName);
        }

        [Fact]
        public void IsSelfIssued_WithPartialChain_ReturnsFalse()
        {
            using (var certificate = SignTestUtility.GetCertificate("leaf.crt"))
            {
                Assert.False(CertificateUtility.IsSelfIssued(certificate));
            }
        }

        [Fact]
        public void IsSelfIssued_WithNonSelfSignedCertificate_ReturnsFalse()
        {
            using (var certificate = _fixture.GetNonSelfSignedCertificate())
            {
                Assert.False(CertificateUtility.IsSelfIssued(certificate));
            }
        }

        [Fact]
        public void IsSelfIssued_WithSelfSignedCertificate_ReturnsTrue()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                Assert.True(CertificateUtility.IsSelfIssued(certificate));
            }
        }

        [Fact]
        public void IsSelfIssued_WithSelfIssuedCertificate_ReturnsTrue()
        {
            using (var certificate = _fixture.GetSelfIssuedCertificate())
            {
                Assert.True(CertificateUtility.IsSelfIssued(certificate));
            }
        }

        [Fact]
        public void IsSelfIssued_WithRootCertificate_ReturnsTrue()
        {
            using (var certificate = _fixture.GetRootCertificate())
            {
                Assert.True(CertificateUtility.IsSelfIssued(certificate));
            }
        }

        private static int GetExtendedKeyUsageCount(X509Certificate2 certificate)
        {
            foreach (var extension in certificate.Extensions)
            {
                if (string.Equals(extension.Oid.Value, Oids.EnhancedKeyUsage))
                {
                    return ((X509EnhancedKeyUsageExtension)extension).EnhancedKeyUsages.Count;
                }
            }

            return 0;
        }
    }
}