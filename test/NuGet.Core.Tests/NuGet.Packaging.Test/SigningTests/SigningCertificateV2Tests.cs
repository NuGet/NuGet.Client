// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SigningCertificateV2Tests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public SigningCertificateV2Tests(CertificatesFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
        }

        [Fact]
        public void Create_WhenCertificateNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => SigningCertificateV2.Create(certificate: null, hashAlgorithmName: HashAlgorithmName.SHA256));

            Assert.Equal("certificate", exception.ParamName);
        }

        [Theory]
        [InlineData(HashAlgorithmName.SHA256)]
        [InlineData(HashAlgorithmName.SHA384)]
        [InlineData(HashAlgorithmName.SHA512)]
        public void Create_WithValidInput_ReturnsSigningCertificateV2(HashAlgorithmName hashAlgorithmName)
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var signingCertificateV2 = SigningCertificateV2.Create(certificate, hashAlgorithmName);

                Assert.Equal(1, signingCertificateV2.Certificates.Count);

                var essCertIdV2 = signingCertificateV2.Certificates[0];

                Assert.Equal(hashAlgorithmName, CryptoHashUtility.OidToHashAlgorithmName(essCertIdV2.HashAlgorithm.Algorithm.Value));
                Assert.Equal(SignTestUtility.GetHash(certificate, hashAlgorithmName), essCertIdV2.CertificateHash);
                Assert.Equal(1, essCertIdV2.IssuerSerial.GeneralNames.Count);
                Assert.Equal(certificate.IssuerName.Name, essCertIdV2.IssuerSerial.GeneralNames[0].DirectoryName.Name);
                SignTestUtility.VerifyByteArrays(certificate.GetSerialNumber(), essCertIdV2.IssuerSerial.SerialNumber);
            }
        }

        [Fact]
        public void Read_WithInvalidAsn1_Throws()
        {
            Assert.Throws<System.Security.Cryptography.CryptographicException>(
                () => SigningCertificateV2.Read(new byte[] { 0x30, 0x0b }));
        }

        [Theory]
        [InlineData(HashAlgorithmName.SHA256)]
        [InlineData(HashAlgorithmName.SHA384)]
        [InlineData(HashAlgorithmName.SHA512)]
        public void Read_WithValidInput_ReturnsSigningCertificateV2(HashAlgorithmName hashAlgorithmName)
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var expectedSigningCertificateV2 = SigningCertificateV2.Create(certificate, hashAlgorithmName);
                var bytes = expectedSigningCertificateV2.Encode();

                var actualSigningCertificateV2 = SigningCertificateV2.Read(bytes);

                Assert.Equal(
                    expectedSigningCertificateV2.Certificates.Count,
                    actualSigningCertificateV2.Certificates.Count);

                for (var i = 0; i < expectedSigningCertificateV2.Certificates.Count; ++i)
                {
                    var expectedEssCertIdV2 = expectedSigningCertificateV2.Certificates[i];
                    var actualEssCertIdV2 = actualSigningCertificateV2.Certificates[i];

                    Assert.Equal(
                        expectedEssCertIdV2.HashAlgorithm.Algorithm.Value,
                        actualEssCertIdV2.HashAlgorithm.Algorithm.Value);
                    SignTestUtility.VerifyByteArrays(
                        expectedEssCertIdV2.CertificateHash,
                        actualEssCertIdV2.CertificateHash);
                    Assert.Equal(
                        expectedEssCertIdV2.IssuerSerial.GeneralNames[0].DirectoryName.Name,
                        actualEssCertIdV2.IssuerSerial.GeneralNames[0].DirectoryName.Name);
                    SignTestUtility.VerifyByteArrays(expectedEssCertIdV2.IssuerSerial.SerialNumber,
                        actualEssCertIdV2.IssuerSerial.SerialNumber);
                }
            }
        }

        [Fact]
        public void Read_WithOnlyCertificateHash_ReturnsSigningCertificateV2()
        {
            var bytes = Asn1TestData.SigningCertificateV2OnlyCertificateHash;
            var expectedCertificateHash = bytes.Skip(8).Take(32).ToArray();

            var signingCertificateV2 = SigningCertificateV2.Read(bytes);

            Assert.Equal(1, signingCertificateV2.Certificates.Count);

            var essCertIdV2 = signingCertificateV2.Certificates[0];

            Assert.Equal(Oids.Sha256, essCertIdV2.HashAlgorithm.Algorithm.Value);
            SignTestUtility.VerifyByteArrays(expectedCertificateHash, essCertIdV2.CertificateHash);
            Assert.Null(essCertIdV2.IssuerSerial);
        }
    }
}