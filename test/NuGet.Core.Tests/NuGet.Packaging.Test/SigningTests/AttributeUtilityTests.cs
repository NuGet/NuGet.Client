// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET46
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Packaging.Signing.DerEncoding;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class AttributeUtilityTests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public AttributeUtilityTests(CertificatesFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
        }

        [Fact]
        public void CreateCommitmentTypeIndication_CommitmentTypeIndicationWithAuthorSignature()
        {
            var attribute = AttributeUtility.CreateCommitmentTypeIndication(SignatureType.Author);
            AttributeUtility.GetCommitmentTypeIndication(attribute).Should().Be(SignatureType.Author);
        }

        [Fact]
        public void CreateCommitmentTypeIndication_CommitmentTypeIndicationWithRepositorySignature()
        {
            var attribute = AttributeUtility.CreateCommitmentTypeIndication(SignatureType.Repository);
            AttributeUtility.GetCommitmentTypeIndication(attribute).Should().Be(SignatureType.Repository);
        }

        [Fact]
        public void GetCommitmentTypeIndication_CommitmentTypeIndicationWithAuthorSignatureAndUnknownVerifyAuthor()
        {
            var attribute = GetCommitmentTypeTestAttribute(Oids.CommitmentTypeIdentifierProofOfOrigin, "1.3.6.1.5.5.7.3.3");
            AttributeUtility.GetCommitmentTypeIndication(attribute).Should().Be(SignatureType.Author);
        }

        [Fact]
        public void GetCommitmentTypeIndication_CommitmentTypeIndicationWithUnknownSignatureType()
        {
            var attribute = GetCommitmentTypeTestAttribute("1.3.6.1.5.5.7.3.3");
            AttributeUtility.GetCommitmentTypeIndication(attribute).Should().Be(SignatureType.Unknown);
        }

        [Fact]
        public void IsValidCommitmentTypeIndication_CommitmentTypeIndicationWithBothRepoAndAuthorVerifyFailure()
        {
            var attribute = GetCommitmentTypeTestAttribute(Oids.CommitmentTypeIdentifierProofOfReceipt, Oids.CommitmentTypeIdentifierProofOfOrigin);
            AttributeUtility.IsValidCommitmentTypeIndication(attribute).Should().BeFalse();
        }

        [Fact]
        public void IsValidCommitmentTypeIndication_CommitmentTypeIndicationWithDuplicateValuesVerifyFailure()
        {
            var attribute = GetCommitmentTypeTestAttribute(Oids.CommitmentTypeIdentifierProofOfOrigin, Oids.CommitmentTypeIdentifierProofOfOrigin);
            AttributeUtility.IsValidCommitmentTypeIndication(attribute).Should().BeFalse();
        }

        [Fact]
        public void IsValidCommitmentTypeIndication_CommitmentTypeIndicationWithNoValueVerifyFailure()
        {
            var attribute = GetCommitmentTypeTestAttribute();
            AttributeUtility.IsValidCommitmentTypeIndication(attribute).Should().BeFalse();
        }

        [Fact]
        public void GetCommitmentTypeIndication_CommitmentTypeIndicationWithBothRepoAndAuthorVerifyThrows()
        {
            var attribute = GetCommitmentTypeTestAttribute(Oids.CommitmentTypeIdentifierProofOfReceipt, Oids.CommitmentTypeIdentifierProofOfOrigin);
            Assert.Throws<SignatureException>(() => AttributeUtility.GetCommitmentTypeIndication(attribute));
        }

        [Fact]
        public void GetCommitmentTypeIndication_CommitmentTypeIndicationWithDuplicateValuesVerifyThrows()
        {
            var attribute = GetCommitmentTypeTestAttribute(Oids.CommitmentTypeIdentifierProofOfOrigin, Oids.CommitmentTypeIdentifierProofOfOrigin);
            Assert.Throws<SignatureException>(() => AttributeUtility.GetCommitmentTypeIndication(attribute));
        }

        [Fact]
        public void GetCommitmentTypeIndication_CommitmentTypeIndicationWithNoValueVerifyThrows()
        {
            var attribute = GetCommitmentTypeTestAttribute();
            Assert.Throws<SignatureException>(() => AttributeUtility.GetCommitmentTypeIndication(attribute));
        }

        [Fact]
        public void CreateSigningCertificateV2_WhenCertificateNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => AttributeUtility.CreateSigningCertificateV2(
                    certificate: null,
                    hashAlgorithm: Common.HashAlgorithmName.SHA256));

            Assert.Equal("certificate", exception.ParamName);
        }

        [Fact]
        public void CreateSigningCertificateV2_WhenHashAlgorithmUnknown_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => AttributeUtility.CreateSigningCertificateV2(
                        certificate,
                        Common.HashAlgorithmName.Unknown));

                Assert.Equal("hashAlgorithmName", exception.ParamName);
            }
        }

        [Theory]
        [InlineData(Common.HashAlgorithmName.SHA256)]
        [InlineData(Common.HashAlgorithmName.SHA384)]
        [InlineData(Common.HashAlgorithmName.SHA512)]
        public void CreateSigningCertificateV2_WithValidInput_ReturnsAttribute(Common.HashAlgorithmName hashAlgorithmName)
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var attribute = AttributeUtility.CreateSigningCertificateV2(certificate, hashAlgorithmName);

                Assert.Equal(Oids.SigningCertificateV2, attribute.Oid.Value);
                Assert.Equal(1, attribute.Values.Count);

                var signingCertificateV2 = SigningCertificateV2.Read(attribute.Values[0].RawData);

                Assert.Equal(1, signingCertificateV2.Certificates.Count);

                var essCertIdV2 = signingCertificateV2.Certificates[0];
                var expectedHash = SignTestUtility.GetHash(certificate, hashAlgorithmName);

                SignTestUtility.VerifyByteArrays(expectedHash, essCertIdV2.CertificateHash);
                Assert.Equal(
                    hashAlgorithmName,
                    CryptoHashUtility.OidToHashAlgorithmName(essCertIdV2.HashAlgorithm.Algorithm.Value));
                Assert.Equal(certificate.IssuerName.Name, essCertIdV2.IssuerSerial.GeneralNames[0].DirectoryName.Name);

                var serialNumber = certificate.GetSerialNumber();

                // Convert from little endian to big endian.
                Array.Reverse(serialNumber);

                SignTestUtility.VerifyByteArrays(
                    serialNumber,
                    essCertIdV2.IssuerSerial.SerialNumber);
            }
        }

        /// <summary>
        /// Allows encoding bad data that the production helper does not.
        /// </summary>
        private static CryptographicAttributeObject GetCommitmentTypeTestAttribute(params string[] oids)
        {
            var values = new List<byte[][]>();

            foreach (var oid in oids)
            {
                values.Add(DerEncoder.SegmentedEncodeOid(oid));
            }

            var commitmentTypeData = DerEncoder.ConstructSequence(values);
            var data = new AsnEncodedData(Oids.CommitmentTypeIndication, commitmentTypeData);

            return new CryptographicAttributeObject(
                new Oid(Oids.CommitmentTypeIndication),
                new AsnEncodedDataCollection(data));
        }
    }
}
#endif