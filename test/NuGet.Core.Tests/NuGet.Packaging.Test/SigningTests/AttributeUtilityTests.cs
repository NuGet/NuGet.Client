// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET46
using System;
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
        private const string CommitmentTypeIdentifierProofOfDelivery = "1.2.840.113549.1.9.16.6.3";
        private const string CommitmentTypeIdentifierProofOfSender = "1.2.840.113549.1.9.16.6.4";

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
        public void CreateCommitmentTypeIndication_WithUnknownSignature_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => AttributeUtility.CreateCommitmentTypeIndication(SignatureType.Unknown));
        }

        [Fact]
        public void CreateCommitmentTypeIndication_WithAuthorSignature_ReturnsOriginType()
        {
            var attribute = AttributeUtility.CreateCommitmentTypeIndication(SignatureType.Author);
            AttributeUtility.GetCommitmentTypeIndication(attribute).Should().Be(SignatureType.Author);
        }

        [Fact]
        public void CreateCommitmentTypeIndication_WithRepositorySignature_ReturnsReceiptType()
        {
            var attribute = AttributeUtility.CreateCommitmentTypeIndication(SignatureType.Repository);
            AttributeUtility.GetCommitmentTypeIndication(attribute).Should().Be(SignatureType.Repository);
        }

        [Fact]
        public void GetCommitmentTypeIndication_WhenAttributeNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => AttributeUtility.GetCommitmentTypeIndication(attribute: null));

            Assert.Equal("attribute", exception.ParamName);
        }

        [Fact]
        public void GetCommitmentTypeIndication_WhenAttributeNotCommitmentTypeIndication_Throws()
        {
            var attribute = new CryptographicAttributeObject(new Oid(Oids.SigningCertificateV2));

            var exception = Assert.Throws<SignatureException>(
                () => AttributeUtility.GetCommitmentTypeIndication(attribute));

            Assert.Equal("The attribute is not a valid commitment-type-indication attribute.", exception.Message);
        }

        [Fact]
        public void GetCommitmentTypeIndication_WithOriginType_ReturnsAuthor()
        {
            var attribute = GetCommitmentTypeTestAttribute(Oids.CommitmentTypeIdentifierProofOfOrigin);
            AttributeUtility.GetCommitmentTypeIndication(attribute).Should().Be(SignatureType.Author);
        }

        [Fact]
        public void GetCommitmentTypeIndication_WithReceiptType_ReturnsRepository()
        {
            var attribute = GetCommitmentTypeTestAttribute(Oids.CommitmentTypeIdentifierProofOfReceipt);
            AttributeUtility.GetCommitmentTypeIndication(attribute).Should().Be(SignatureType.Repository);
        }

        [Fact]
        public void GetCommitmentTypeIndication_WithUnknownType_ReturnsUnknown()
        {
            var attribute = GetCommitmentTypeTestAttribute(CommitmentTypeIdentifierProofOfDelivery);
            AttributeUtility.GetCommitmentTypeIndication(attribute).Should().Be(SignatureType.Unknown);
        }

        [Fact]
        public void GetCommitmentTypeIndication_WithMultipleUnknownTypes_ReturnsUnknown()
        {
            var attribute = GetCommitmentTypeTestAttribute(
                CommitmentTypeIdentifierProofOfDelivery, CommitmentTypeIdentifierProofOfSender);
            AttributeUtility.GetCommitmentTypeIndication(attribute).Should().Be(SignatureType.Unknown);
        }

        [Fact]
        public void GetCommitmentTypeIndication_WithNoType_ReturnsUnknown()
        {
            var attribute = GetCommitmentTypeTestAttribute();

            Assert.Equal(SignatureType.Unknown, AttributeUtility.GetCommitmentTypeIndication(attribute));
        }

        [Fact]
        public void GetCommitmentTypeIndication_WithBothOriginAndReceiptTypes_Throws()
        {
            var attribute = GetCommitmentTypeTestAttribute(
                Oids.CommitmentTypeIdentifierProofOfOrigin, Oids.CommitmentTypeIdentifierProofOfReceipt);

            var exception = Assert.Throws<SignatureException>(
                () => AttributeUtility.GetCommitmentTypeIndication(attribute));

            Assert.Equal("The commitment-type-indication attribute contains an invalid combination of values.", exception.Message);
        }

        [Fact]
        public void GetCommitmentTypeIndication_WithDuplicateOriginTypes_ReturnsAuthor()
        {
            var attribute = GetCommitmentTypeTestAttribute(
                Oids.CommitmentTypeIdentifierProofOfOrigin, Oids.CommitmentTypeIdentifierProofOfOrigin);

            Assert.Equal(SignatureType.Author, AttributeUtility.GetCommitmentTypeIndication(attribute));
        }

        [Fact]
        public void GetCommitmentTypeIndication_WithOriginAndUnknownType_ReturnsAuthor()
        {
            var attribute = GetCommitmentTypeTestAttribute(
                Oids.CommitmentTypeIdentifierProofOfOrigin, CommitmentTypeIdentifierProofOfSender);

            Assert.Equal(SignatureType.Author, AttributeUtility.GetCommitmentTypeIndication(attribute));
        }

        [Fact]
        public void GetCommitmentTypeIndication_CommitmentTypeIndicationWithBothRepoAndAuthorVerifyThrows()
        {
            var attribute = GetCommitmentTypeTestAttribute(Oids.CommitmentTypeIdentifierProofOfReceipt, Oids.CommitmentTypeIdentifierProofOfOrigin);
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
            var commitmentTypeIndication = new Oid(Oids.CommitmentTypeIndication);
            var values = new AsnEncodedDataCollection();

            foreach (var oid in oids)
            {
                var value = DerEncoder.ConstructSequence(DerEncoder.SegmentedEncodeOid(oid));

                values.Add(new AsnEncodedData(commitmentTypeIndication, value));
            }

            return new CryptographicAttributeObject(commitmentTypeIndication, values);
        }
    }
}
#endif