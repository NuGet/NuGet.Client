// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#if NET46

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Moq;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Packaging.Signing.DerEncoding;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignedAttributeTests
    {
        [Fact]
        public void GetCommitmentTypeIndication_CommitmentTypeIndicationWithAuthorSignature()
        {
            var attribute = AttributeUtility.GetCommitmentTypeIndication(SignatureType.Author);
            AttributeUtility.GetCommitmentTypeIndication(attribute).Should().Be(SignatureType.Author);
        }

        [Fact]
        public void GetCommitmentTypeIndication_CommitmentTypeIndicationWithRepositorySignature()
        {
            var attribute = AttributeUtility.GetCommitmentTypeIndication(SignatureType.Repository);
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
        public void IsValidSigningCertificateV2_VerifySuccess()
        {
            var cert = TestCertificate.Generate();
            var attribute = AttributeUtility.GetSigningCertificateV2(new[] { cert.PublicCert }, Common.HashAlgorithmName.SHA512);
            AttributeUtility.IsValidSigningCertificateV2(cert.PublicCert, new[] { cert.PublicCert }, attribute, SigningSpecifications.V1).Should().BeTrue();
        }

        [Fact]
        public void IsValidSigningCertificateV2_VerifyFailure()
        {
            var cert = TestCertificate.Generate();
            var cert2 = TestCertificate.Generate();
            var attribute = AttributeUtility.GetSigningCertificateV2(new[] { cert.PublicCert }, Common.HashAlgorithmName.SHA512);
            AttributeUtility.IsValidSigningCertificateV2(cert2.PublicCert, new[] { cert2.PublicCert }, attribute, SigningSpecifications.V1).Should().BeFalse();
        }

        [Fact]
        public void IsValidSigningCertificateV2_HashNotAllowedVerifyFailure()
        {
            var cert = TestCertificate.Generate();

            // Allow only 512
            var spec = new Mock<SigningSpecifications>();
            spec.SetupGet(e => e.AllowedHashAlgorithmOids).Returns(new string[] { Common.HashAlgorithmName.SHA512.ConvertToOidString() });

            // Hash with 256
            var attribute = AttributeUtility.GetSigningCertificateV2(new[] { cert.PublicCert }, Common.HashAlgorithmName.SHA256);

            // Verify failure
            AttributeUtility.IsValidSigningCertificateV2(cert.PublicCert, new[] { cert.PublicCert }, attribute, spec.Object).Should().BeFalse();
        }

        [Fact]
        public void SignedAttribute_IsValidSigningCertificateV2_SignatureCertIsNotFirstInListVerifyFailure()
        {
            var signerLeaf = TestCertificate.Generate().PublicCert;
            var rootCert = TestCertificate.Generate().PublicCert;

            var localChain = new List<X509Certificate2>() { rootCert, signerLeaf };
            var attributeChain = new List<KeyValuePair<Common.HashAlgorithmName, byte[]>>()
            {
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, Common.HashAlgorithmName.SHA512.GetHashProvider().ComputeHash(rootCert.RawData)),
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, Common.HashAlgorithmName.SHA512.GetHashProvider().ComputeHash(signerLeaf.RawData))
            };

            // Verify leaf node is first in the list
            AttributeUtility.IsValidSigningCertificateV2(signerLeaf, localChain, attributeChain, SigningSpecifications.V1)
                .Should()
                .BeFalse();
        }

        [Fact]
        public void IsValidSigningCertificateV2_OrderChangeVerifyFailure()
        {
            var child = TestCertificate.Generate().PublicCert;
            var parent = TestCertificate.Generate().PublicCert;

            var localChain = new List<X509Certificate2>() { child, parent };
            var attributeChain = new List<KeyValuePair<Common.HashAlgorithmName, byte[]>>()
            {
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, Common.HashAlgorithmName.SHA512.GetHashProvider().ComputeHash(parent.RawData)),
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, Common.HashAlgorithmName.SHA512.GetHashProvider().ComputeHash(child.RawData))
            };

            AttributeUtility.IsValidSigningCertificateV2(child, localChain, attributeChain, SigningSpecifications.V1)
                .Should()
                .BeFalse();

            AttributeUtility.IsValidSigningCertificateV2(parent, localChain, attributeChain, SigningSpecifications.V1)
                .Should()
                .BeFalse();
        }

        [Fact]
        public void IsValidSigningCertificateV2_AttributeCountChangeVerifyFailure()
        {
            var child = TestCertificate.Generate().PublicCert;
            var parent = TestCertificate.Generate().PublicCert;
            var extra = TestCertificate.Generate().PublicCert;

            var localChain = new List<X509Certificate2>() { child, parent };
            var attributeChain = new List<KeyValuePair<Common.HashAlgorithmName, byte[]>>()
            {
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, Common.HashAlgorithmName.SHA512.GetHashProvider().ComputeHash(child.RawData)),
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, Common.HashAlgorithmName.SHA512.GetHashProvider().ComputeHash(parent.RawData)),
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, Common.HashAlgorithmName.SHA512.GetHashProvider().ComputeHash(extra.RawData))
            };

            AttributeUtility.IsValidSigningCertificateV2(child, localChain, attributeChain, SigningSpecifications.V1)
                .Should()
                .BeFalse();
        }

        [Fact]
        public void IsValidSigningCertificateV2_LocalCountChangeVerifyFailure()
        {
            var child = TestCertificate.Generate().PublicCert;
            var parent = TestCertificate.Generate().PublicCert;
            var extra = TestCertificate.Generate().PublicCert;

            var localChain = new List<X509Certificate2>() { child, parent, extra };
            var attributeChain = new List<KeyValuePair<Common.HashAlgorithmName, byte[]>>()
            {
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, Common.HashAlgorithmName.SHA512.GetHashProvider().ComputeHash(child.RawData)),
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, Common.HashAlgorithmName.SHA512.GetHashProvider().ComputeHash(parent.RawData))
            };

            AttributeUtility.IsValidSigningCertificateV2(child, localChain, attributeChain, SigningSpecifications.V1)
                .Should()
                .BeFalse();
        }

        [Fact]
        public void IsValidSigningCertificateV2_HashChangeVerifyFailure()
        {
            var child = TestCertificate.Generate().PublicCert;
            var parent = TestCertificate.Generate().PublicCert;

            var localChain = new List<X509Certificate2>() { child, parent };
            var attributeChain = new List<KeyValuePair<Common.HashAlgorithmName, byte[]>>()
            {
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, Common.HashAlgorithmName.SHA512.GetHashProvider().ComputeHash(child.RawData)),
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, new byte[] { 1, 1, 1 })
            };

            AttributeUtility.IsValidSigningCertificateV2(child, localChain, attributeChain, SigningSpecifications.V1)
                .Should()
                .BeFalse();
        }

        [Fact]
        public void IsValidSigningCertificateV2_EmptyChainVerifyFailure()
        {
            var child = TestCertificate.Generate().PublicCert;
            var parent = TestCertificate.Generate().PublicCert;

            var localChain = new List<X509Certificate2>() { child, parent };
            var attributeChain = new List<KeyValuePair<Common.HashAlgorithmName, byte[]>>();

            AttributeUtility.IsValidSigningCertificateV2(child, localChain, attributeChain, SigningSpecifications.V1)
                .Should()
                .BeFalse();
        }

        [Fact]
        public void IsValidSigningCertificateV2_DirectHashVerifySuccess()
        {
            var child = TestCertificate.Generate().PublicCert;
            var parent = TestCertificate.Generate().PublicCert;

            var localChain = new List<X509Certificate2>() { child, parent };
            var attributeChain = new List<KeyValuePair<Common.HashAlgorithmName, byte[]>>()
            {
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, Common.HashAlgorithmName.SHA512.GetHashProvider().ComputeHash(child.RawData)),
                new KeyValuePair<Common.HashAlgorithmName, byte[]>(Common.HashAlgorithmName.SHA512, Common.HashAlgorithmName.SHA512.GetHashProvider().ComputeHash(parent.RawData)),
            };

            AttributeUtility.IsValidSigningCertificateV2(child, localChain, attributeChain, SigningSpecifications.V1)
                .Should()
                .BeTrue();
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

            // Create an attribute
            return new CryptographicAttributeObject(
                oid: new Oid(Oids.CommitmentTypeIndication),
                values: new AsnEncodedDataCollection(data));
        }
    }
}

#endif