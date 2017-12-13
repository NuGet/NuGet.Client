// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.Asn1.X509;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SigningUtilityTests
    {
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
                Assert.True(SigningUtility.IsSignatureAlgorithmSupported(certificate));
            }
        }

        [Fact]
        public void IsSignatureAlgorithmSupported_WhenUnsupported_ReturnsFalse()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate(
                "test",
                generator => { },
                "SHA256WITHRSAANDMGF1"))
            {
                // RSASSA-PSS
                Assert.Equal("1.2.840.113549.1.1.10", certificate.SignatureAlgorithm.Value);
                Assert.False(SigningUtility.IsSignatureAlgorithmSupported(certificate));
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
                Assert.True(SigningUtility.IsCertificatePublicKeyValid(certificate));
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
                Assert.False(SigningUtility.IsCertificatePublicKeyValid(certificate));
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
                Assert.True(SigningUtility.IsCertificatePublicKeyValid(certificate));
            }
        }

        [Fact]
        public void GetCertificateChain_ReturnsCertificatesInOrder()
        {
            using (var chain = new X509Chain())
            using (var rootCertificate = GetCertificate("root.crt"))
            using (var intermediateCertificate = GetCertificate("intermediate.crt"))
            using (var leafCertificate = GetCertificate("leaf.crt"))
            {
                chain.ChainPolicy.ExtraStore.Add(rootCertificate);
                chain.ChainPolicy.ExtraStore.Add(intermediateCertificate);

                chain.Build(leafCertificate);

                var certificateChain = SigningUtility.GetCertificateChain(chain);

                Assert.Equal(3, certificateChain.Count);
                Assert.Equal(leafCertificate.Thumbprint, certificateChain[0].Thumbprint);
                Assert.Equal(intermediateCertificate.Thumbprint, certificateChain[1].Thumbprint);
                Assert.Equal(rootCertificate.Thumbprint, certificateChain[2].Thumbprint);
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
                Assert.True(SigningUtility.HasExtendedKeyUsage(certificate, Oids.CodeSigningEkuOid));
            }
        }

        [Fact]
        public void HasExtendedKeyUsage_WithoutEku_ReturnsFalseForEku()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate("test", generator => { }))
            {
                Assert.Equal(0, GetExtendedKeyUsageCount(certificate));
                Assert.False(SigningUtility.HasExtendedKeyUsage(certificate, Oids.CodeSigningEkuOid));
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
                Assert.True(SigningUtility.IsValidForPurposeFast(certificate, Oids.CodeSigningEkuOid));
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
                Assert.False(SigningUtility.IsValidForPurposeFast(certificate, Oids.CodeSigningEkuOid));
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
                Assert.False(SigningUtility.IsValidForPurposeFast(certificate, Oids.CodeSigningEkuOid));
            }
        }

        [Fact]
        public void IsValidForPurposeFast_WithNoEku_ReturnsTrueForCodeSigningEku()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate("test", generator => { }))
            {
                Assert.Equal(0, GetExtendedKeyUsageCount(certificate));
                Assert.True(SigningUtility.IsValidForPurposeFast(certificate, Oids.CodeSigningEkuOid));
            }
        }

        private static int GetExtendedKeyUsageCount(X509Certificate2 certificate)
        {
            foreach (var extension in certificate.Extensions)
            {
                if (string.Equals(extension.Oid.Value, Oids.EnhancedKeyUsageOid))
                {
                    return ((X509EnhancedKeyUsageExtension)extension).EnhancedKeyUsages.Count;
                }
            }

            return 0;
        }

        private static X509Certificate2 GetCertificate(string name)
        {
            var bytes = ResourceTestUtility.GetResourceBytes($"NuGet.Packaging.Test.compiler.resources.{name}", typeof(SigningUtilityTests));

            return new X509Certificate2(bytes);
        }
    }
}