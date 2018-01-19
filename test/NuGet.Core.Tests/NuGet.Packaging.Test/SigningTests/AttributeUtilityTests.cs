// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET46
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Packaging.Signing.DerEncoding;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Crypto;
using Xunit;
using BcCommitmentTypeIndication = Org.BouncyCastle.Asn1.Esf.CommitmentTypeIndication;
using DotNetUtilities = Org.BouncyCastle.Security.DotNetUtilities;

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
            var attributes = new CryptographicAttributeObjectCollection(attribute);

            AttributeUtility.GetSignatureType(attributes).Should().Be(SignatureType.Author);
        }

        [Fact]
        public void CreateCommitmentTypeIndication_WithRepositorySignature_ReturnsReceiptType()
        {
            var attribute = AttributeUtility.CreateCommitmentTypeIndication(SignatureType.Repository);
            var attributes = new CryptographicAttributeObjectCollection(attribute);

            AttributeUtility.GetSignatureType(attributes).Should().Be(SignatureType.Repository);
        }

        [Fact]
        public void GetSignatureType_WhenSignedAttributesNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => AttributeUtility.GetSignatureType(signedAttributes: null));

            Assert.Equal("signedAttributes", exception.ParamName);
        }

        [Fact]
        public void GetSignatureType_WhenCommitmentTypeIndicationAttributeNotFound_ReturnsUnknown()
        {
            var attribute = new CryptographicAttributeObject(new Oid(Oids.SigningCertificateV2));
            var attributes = new CryptographicAttributeObjectCollection(attribute);

            var signatureType = AttributeUtility.GetSignatureType(attributes);

            Assert.Equal(SignatureType.Unknown, signatureType);
        }

        [Fact]
        public void GetSignatureType_WithOriginType_ReturnsAuthor()
        {
            var attribute = GetCommitmentTypeTestAttribute(Oids.CommitmentTypeIdentifierProofOfOrigin);
            var attributes = new CryptographicAttributeObjectCollection(attribute);

            AttributeUtility.GetSignatureType(attributes).Should().Be(SignatureType.Author);
        }

        [Fact]
        public void GetSignatureType_WithReceiptType_ReturnsRepository()
        {
            var attribute = GetCommitmentTypeTestAttribute(Oids.CommitmentTypeIdentifierProofOfReceipt);
            var attributes = new CryptographicAttributeObjectCollection(attribute);

            AttributeUtility.GetSignatureType(attributes).Should().Be(SignatureType.Repository);
        }

        [Fact]
        public void GetSignatureType_WithUnknownType_ReturnsUnknown()
        {
            var attribute = GetCommitmentTypeTestAttribute(CommitmentTypeIdentifierProofOfDelivery);
            var attributes = new CryptographicAttributeObjectCollection(attribute);

            AttributeUtility.GetSignatureType(attributes).Should().Be(SignatureType.Unknown);
        }

        [Fact]
        public void GetSignatureType_WithMultipleUnknownTypes_ReturnsUnknown()
        {
            var attribute = GetCommitmentTypeTestAttribute(
                CommitmentTypeIdentifierProofOfDelivery, CommitmentTypeIdentifierProofOfSender);
            var attributes = new CryptographicAttributeObjectCollection(attribute);

            AttributeUtility.GetSignatureType(attributes).Should().Be(SignatureType.Unknown);
        }

        [Fact]
        public void GetSignatureType_WithNoType_ReturnsUnknown()
        {
            var attribute = GetCommitmentTypeTestAttribute();
            var attributes = new CryptographicAttributeObjectCollection(attribute);

            Assert.Equal(SignatureType.Unknown, AttributeUtility.GetSignatureType(attributes));
        }

        [Fact]
        public void GetSignatureType_WithBothOriginAndReceiptTypes_Throws()
        {
            var attribute = GetCommitmentTypeTestAttribute(
                Oids.CommitmentTypeIdentifierProofOfOrigin, Oids.CommitmentTypeIdentifierProofOfReceipt);
            var attributes = new CryptographicAttributeObjectCollection(attribute);

            var exception = Assert.Throws<SignatureException>(
                () => AttributeUtility.GetSignatureType(attributes));

            Assert.Equal("The commitment-type-indication attribute contains an invalid combination of values.", exception.Message);
        }

        [Fact]
        public void GetSignatureType_WithDuplicateOriginTypes_ReturnsAuthor()
        {
            var attribute = GetCommitmentTypeTestAttribute(
                Oids.CommitmentTypeIdentifierProofOfOrigin, Oids.CommitmentTypeIdentifierProofOfOrigin);
            var attributes = new CryptographicAttributeObjectCollection(attribute);

            Assert.Equal(SignatureType.Author, AttributeUtility.GetSignatureType(attributes));
        }

        [Fact]
        public void GetSignatureType_WithOriginAndUnknownType_ReturnsAuthor()
        {
            var attribute = GetCommitmentTypeTestAttribute(
                Oids.CommitmentTypeIdentifierProofOfOrigin, CommitmentTypeIdentifierProofOfSender);
            var attributes = new CryptographicAttributeObjectCollection(attribute);

            Assert.Equal(SignatureType.Author, AttributeUtility.GetSignatureType(attributes));
        }

        [Fact]
        public void GetSignatureType_WithOriginTypeInNonFirstAttributeInstance_ReturnsAuthor()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var attributes = CreateAttributeCollection(certificate, _fixture.DefaultKeyPair.Private,
                    vector =>
                    {
                        vector.Add(
                            new Org.BouncyCastle.Asn1.Cms.Attribute(
                                PkcsObjectIdentifiers.IdAAEtsCommitmentType,
                                new DerSet(
                                    new BcCommitmentTypeIndication(PkcsObjectIdentifiers.IdCtiEtsProofOfApproval))));

                        vector.Add(
                            new Org.BouncyCastle.Asn1.Cms.Attribute(
                                PkcsObjectIdentifiers.IdAAEtsCommitmentType,
                                new DerSet(
                                    new BcCommitmentTypeIndication(PkcsObjectIdentifiers.IdCtiEtsProofOfOrigin))));
                    });

                Assert.Equal(
                    2,
                    attributes.Cast<CryptographicAttributeObject>()
                        .Count(attribute => attribute.Oid.Value == Oids.CommitmentTypeIndication));

                Assert.Equal(SignatureType.Author, AttributeUtility.GetSignatureType(attributes));
            }
        }

        [Fact]
        public void GetSignatureType_WithOriginTypeInOneAttributeInstanceAndReceiptTypeInAnotherAttributeInstance_ReturnsAuthor()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var attributes = CreateAttributeCollection(certificate, _fixture.DefaultKeyPair.Private,
                    vector =>
                    {
                        vector.Add(
                            new Org.BouncyCastle.Asn1.Cms.Attribute(
                                PkcsObjectIdentifiers.IdAAEtsCommitmentType,
                                new DerSet(
                                    new BcCommitmentTypeIndication(PkcsObjectIdentifiers.IdCtiEtsProofOfOrigin))));

                        vector.Add(
                            new Org.BouncyCastle.Asn1.Cms.Attribute(
                                PkcsObjectIdentifiers.IdAAEtsCommitmentType,
                                new DerSet(
                                    new BcCommitmentTypeIndication(PkcsObjectIdentifiers.IdCtiEtsProofOfReceipt))));
                    });

                Assert.Equal(
                    2,
                    attributes.Cast<CryptographicAttributeObject>()
                        .Count(attribute => attribute.Oid.Value == Oids.CommitmentTypeIndication));

                var exception = Assert.Throws<SignatureException>(
                    () => AttributeUtility.GetSignatureType(attributes));

                Assert.Equal("The commitment-type-indication attribute contains an invalid combination of values.", exception.Message);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1.2.3")]
        public void GetSignatureType_WithUnknownOid_ReturnsUnknownType(string oid)
        {
            Assert.Equal(SignatureType.Unknown, AttributeUtility.GetSignatureType(oid));
        }

        [Theory]
        [InlineData(Oids.CommitmentTypeIdentifierProofOfOrigin, SignatureType.Author)]
        [InlineData(Oids.CommitmentTypeIdentifierProofOfReceipt, SignatureType.Repository)]
        public void GetSignatureType_WithKnownOid_ReturnsKnownType(string oid, SignatureType expectedResult)
        {
            Assert.Equal(expectedResult, AttributeUtility.GetSignatureType(oid));
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

        [Fact]
        public void GetAttribute_WhenAttributesNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => AttributeUtility.GetAttribute(attributes: null, oid: "1.2.3"));

            Assert.Equal("attributes", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetAttribute_WhenOidNullOrEmpty_Throws(string oid)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => AttributeUtility.GetAttribute(new CryptographicAttributeObjectCollection(), oid));

            Assert.Equal("oid", exception.ParamName);
        }

        [Fact]
        public void GetAttribute_WithNoMatches_ReturnsNull()
        {
            var attribute = AttributeUtility.GetAttribute(new CryptographicAttributeObjectCollection(), Oids.SigningTime);

            Assert.Null(attribute);
        }

        [Fact]
        public void GetAttribute_WithMultipleMatches_ReturnsMatches()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var attributes = CreateAttributeCollection(certificate, _fixture.DefaultKeyPair.Private,
                    vector =>
                    {
                        vector.Add(
                            new Org.BouncyCastle.Asn1.Cms.Attribute(
                                PkcsObjectIdentifiers.IdAAEtsCommitmentType,
                                new DerSet(
                                    new BcCommitmentTypeIndication(PkcsObjectIdentifiers.IdCtiEtsProofOfOrigin))));

                        vector.Add(
                            new Org.BouncyCastle.Asn1.Cms.Attribute(
                                PkcsObjectIdentifiers.IdAAEtsCommitmentType,
                                new DerSet(
                                    new BcCommitmentTypeIndication(PkcsObjectIdentifiers.IdCtiEtsProofOfReceipt))));
                    });

                var exception = Assert.Throws<CryptographicException>(
                    () => AttributeUtility.GetAttribute(attributes, Oids.CommitmentTypeIndication));

                Assert.Equal(
                    $"Multiple instances of attribute '{Oids.CommitmentTypeIndication}' were found.",
                    exception.Message);
            }
        }

        [Fact]
        public void GetAttributes_WhenAttributesNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => AttributeUtility.GetAttributes(attributes: null, oid: "1.2.3"));

            Assert.Equal("attributes", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetAttributes_WhenOidNullOrEmpty_Throws(string oid)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => AttributeUtility.GetAttributes(new CryptographicAttributeObjectCollection(), oid));

            Assert.Equal("oid", exception.ParamName);
        }

        [Fact]
        public void GetAttributes_WithNoMatches_ReturnsEmptyEnumerable()
        {
            var attributes = AttributeUtility.GetAttributes(new CryptographicAttributeObjectCollection(), Oids.SigningTime);

            Assert.Empty(attributes);
        }

        [Fact]
        public void GetAttributes_WithMultipleMatches_ReturnsMatches()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var attributes = CreateAttributeCollection(certificate, _fixture.DefaultKeyPair.Private,
                    vector =>
                    {
                        vector.Add(
                            new Org.BouncyCastle.Asn1.Cms.Attribute(
                                CmsAttributes.SigningTime,
                                new DerSet(new DerUtcTime(DateTime.UtcNow))));

                        vector.Add(
                            new Org.BouncyCastle.Asn1.Cms.Attribute(
                                PkcsObjectIdentifiers.IdAAEtsCommitmentType,
                                new DerSet(
                                    new BcCommitmentTypeIndication(PkcsObjectIdentifiers.IdCtiEtsProofOfOrigin))));

                        vector.Add(
                            new Org.BouncyCastle.Asn1.Cms.Attribute(
                                PkcsObjectIdentifiers.IdAAEtsCommitmentType,
                                new DerSet(
                                    new BcCommitmentTypeIndication(PkcsObjectIdentifiers.IdCtiEtsProofOfReceipt))));
                    });

                var matches = AttributeUtility.GetAttributes(attributes, Oids.CommitmentTypeIndication).ToArray();

                Assert.Equal(2, matches.Length);
                Assert.Equal(
                    PkcsObjectIdentifiers.IdCtiEtsProofOfOrigin.ToString(),
                    CommitmentTypeIndication.Read(matches[0].Values[0].RawData).CommitmentTypeId.Value);
                Assert.Equal(
                    PkcsObjectIdentifiers.IdCtiEtsProofOfReceipt.ToString(),
                    CommitmentTypeIndication.Read(matches[1].Values[0].RawData).CommitmentTypeId.Value);
            }
        }

        /// <summary>
        /// Allows encoding data that the production helper does not.
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

        private static CryptographicAttributeObjectCollection CreateAttributeCollection(
            X509Certificate2 certificate,
            AsymmetricKeyParameter privateKey,
            Action<Asn1EncodableVector> addAttributes)
        {
            var content = new CmsProcessableByteArray(new byte[0]);
            var attributes = new Asn1EncodableVector();

            addAttributes(attributes);

            var signedAttributes = new AttributeTable(attributes);
            var unsignedAttributes = new AttributeTable(DerSet.Empty);

            var generator = new CmsSignedDataGenerator();

            generator.AddSigner(
                privateKey,
                DotNetUtilities.FromX509Certificate(certificate),
                Oids.Sha256,
                signedAttributes,
                unsignedAttributes);

            var bcSignedCms = generator.Generate(content, encapsulate: true);
            var signedCms = new SignedCms();

            signedCms.Decode(bcSignedCms.GetEncoded());

            return signedCms.SignerInfos[0].SignedAttributes;
        }
    }
}
#endif