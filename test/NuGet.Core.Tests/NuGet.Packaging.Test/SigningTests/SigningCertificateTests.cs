// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Org.BouncyCastle.Asn1;
using Xunit;
using BcEssCertId = Org.BouncyCastle.Asn1.Ess.EssCertID;
using BcPolicyInformation = Org.BouncyCastle.Asn1.X509.PolicyInformation;
using BcSigningCertificate = Org.BouncyCastle.Asn1.Ess.SigningCertificate;
using SigningCertificate = NuGet.Packaging.Signing.SigningCertificate;

namespace NuGet.Packaging.Test
{
    public class SigningCertificateTests
    {
        [Fact]
        public void Read_WithInvalidAsn1_Throws()
        {
            Assert.Throws<CryptographicException>(
                () => SigningCertificate.Read(new byte[] { 0x30, 0x0b }));
        }

        [Fact]
        public void Read_WithPolicyInformation_ReturnsSigningCertificate()
        {
            var bcEssCertId = CreateBcEssCertId("1");
            var bcPolicyInfo = new BcPolicyInformation(new DerObjectIdentifier(Oids.AnyPolicy));
            var bcSigningCertificate = new BcSigningCertificate(
                new DerSequence(new DerSequence(bcEssCertId), new DerSequence(bcPolicyInfo)));
            var bytes = bcSigningCertificate.GetDerEncoded();

            var signingCertificate = SigningCertificate.Read(bytes);

            Assert.Equal(1, signingCertificate.Certificates.Count);
            Assert.Equal(1, signingCertificate.Policies.Count);

            var policyInfo = signingCertificate.Policies[0];

            Assert.Equal(bcPolicyInfo.PolicyIdentifier.ToString(), policyInfo.PolicyIdentifier.Value);
            Assert.Null(policyInfo.PolicyQualifiers);
        }

        [Fact]
        public void Read_WithMultipleEssCertIds_ReturnsSigningCertificate()
        {
            var bcEssCertId1 = CreateBcEssCertId("1");
            var bcEssCertId2 = CreateBcEssCertId("2");
            var bcEssCertId3 = CreateBcEssCertId("3");
            var bcSigningCertificate = new BcSigningCertificate(
                new DerSequence(new DerSequence(bcEssCertId1, bcEssCertId2, bcEssCertId3)));
            var bytes = bcSigningCertificate.GetDerEncoded();

            var signingCertificate = SigningCertificate.Read(bytes);

            Assert.Equal(3, signingCertificate.Certificates.Count);
            Assert.Null(signingCertificate.Policies);

            SignTestUtility.VerifyByteArrays(
                bcEssCertId1.GetCertHash(),
                signingCertificate.Certificates[0].CertificateHash);
            Assert.Null(signingCertificate.Certificates[0].IssuerSerial);

            SignTestUtility.VerifyByteArrays(
                bcEssCertId2.GetCertHash(),
                signingCertificate.Certificates[1].CertificateHash);
            Assert.Null(signingCertificate.Certificates[1].IssuerSerial);

            SignTestUtility.VerifyByteArrays(
                bcEssCertId3.GetCertHash(),
                signingCertificate.Certificates[2].CertificateHash);
            Assert.Null(signingCertificate.Certificates[2].IssuerSerial);
        }

        private static BcEssCertId CreateBcEssCertId(string text)
        {
            using (var hashAlgorithm = CryptoHashUtility.GetSha1HashProvider())
            {
                var hash = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(text));

                return new BcEssCertId(hash);
            }
        }
    }
}