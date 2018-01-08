// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class EssCertIdV2Tests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public EssCertIdV2Tests(CertificatesFixture fixture)
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
                () => EssCertIdV2.Create(certificate: null, hashAlgorithmName: HashAlgorithmName.SHA256));

            Assert.Equal("certificate", exception.ParamName);
        }

        [Fact]
        public void Create_WithUnknownHashAlgorithmName_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => EssCertIdV2.Create(certificate, Common.HashAlgorithmName.Unknown));

                Assert.Equal("hashAlgorithmName", exception.ParamName);
            }
        }

        [Fact]
        public void Create_WithSha256_ReturnsEssCertIdV2()
        {
            var hashAlgorithmName = HashAlgorithmName.SHA256;

            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var essCertIdV2 = EssCertIdV2.Create(certificate, hashAlgorithmName);

                Assert.Equal(SignTestUtility.GetHash(certificate, hashAlgorithmName), essCertIdV2.CertificateHash);
                Assert.Equal(Oids.Sha256, essCertIdV2.HashAlgorithm.Algorithm.Value);
                Assert.Equal(1, essCertIdV2.IssuerSerial.GeneralNames.Count);
                Assert.Equal(certificate.IssuerName.Name, essCertIdV2.IssuerSerial.GeneralNames[0].DirectoryName.Name);
                SignTestUtility.VerifyByteArrays(certificate.GetSerialNumber(), essCertIdV2.IssuerSerial.SerialNumber);
            }
        }

        [Fact]
        public void Create_WithSha384_ReturnsEssCertIdV2()
        {
            var hashAlgorithmName = HashAlgorithmName.SHA384;

            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var essCertIdV2 = EssCertIdV2.Create(certificate, hashAlgorithmName);

                Assert.Equal(SignTestUtility.GetHash(certificate, hashAlgorithmName), essCertIdV2.CertificateHash);
                Assert.Equal(Oids.Sha384, essCertIdV2.HashAlgorithm.Algorithm.Value);
                Assert.Equal(1, essCertIdV2.IssuerSerial.GeneralNames.Count);
                Assert.Equal(certificate.IssuerName.Name, essCertIdV2.IssuerSerial.GeneralNames[0].DirectoryName.Name);
                SignTestUtility.VerifyByteArrays(certificate.GetSerialNumber(), essCertIdV2.IssuerSerial.SerialNumber);
            }
        }

        [Fact]
        public void Create_WithSha512_ReturnsEssCertIdV2()
        {
            var hashAlgorithmName = HashAlgorithmName.SHA512;

            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var essCertIdV2 = EssCertIdV2.Create(certificate, hashAlgorithmName);

                Assert.Equal(SignTestUtility.GetHash(certificate, hashAlgorithmName), essCertIdV2.CertificateHash);
                Assert.Equal(Oids.Sha512, essCertIdV2.HashAlgorithm.Algorithm.Value);
                Assert.Equal(1, essCertIdV2.IssuerSerial.GeneralNames.Count);
                Assert.Equal(certificate.IssuerName.Name, essCertIdV2.IssuerSerial.GeneralNames[0].DirectoryName.Name);
                SignTestUtility.VerifyByteArrays(certificate.GetSerialNumber(), essCertIdV2.IssuerSerial.SerialNumber);
            }
        }

        [Fact]
        public void Read_WithInvalidAsn1_Throws()
        {
            Assert.Throws<System.Security.Cryptography.CryptographicException>(
                () => EssCertIdV2.Read(new byte[] { 0x30, 0x0b }));
        }

        [Fact]
        public void Read_WithOnlyCertificateHash_ReturnsEssCertIdV2()
        {
            var essCertIdV2Bytes = Asn1TestData.SigningCertificateV2OnlyCertificateHash.Skip(4).ToArray();
            var expectedCertificateHash = essCertIdV2Bytes.Skip(4).Take(32).ToArray();

            var essCertIdV2 = EssCertIdV2.Read(essCertIdV2Bytes);

            Assert.Equal(Oids.Sha256, essCertIdV2.HashAlgorithm.Algorithm.Value);
            SignTestUtility.VerifyByteArrays(expectedCertificateHash, essCertIdV2.CertificateHash);
            Assert.Null(essCertIdV2.IssuerSerial);
        }
    }
}