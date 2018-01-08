// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET46
using System;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignatureTests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public SignatureTests(CertificatesFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
        }

        [Fact]
        public void Load_WhenDataNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => Signature.Load(data: null));

            Assert.Equal("data", exception.ParamName);
        }

        [Fact]
        public void Load_WithMultiplePrimarySignatures_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var content = new SignatureContent(
                    SigningSpecifications.V1,
                    HashAlgorithmName.SHA256,
                    hashValue: "hash");
                var contentInfo = new ContentInfo(content.GetBytes());
                var signedCms = new SignedCms(contentInfo);

                var cmsSigner = new CmsSigner(certificate);
                signedCms.ComputeSignature(cmsSigner);

                cmsSigner = new CmsSigner(certificate);
                signedCms.ComputeSignature(cmsSigner);

                var exception = Assert.Throws<SignatureException>(
                    () => Signature.Load(signedCms.Encode()));

                Assert.Equal(NuGetLogCode.NU3009, exception.Code);
                Assert.Equal("The package signature contains multiple primary signatures.", exception.Message);
            }
        }

        [Fact]
        public void Load_WithAuthorSignatureWithoutSigningCertificateV2Attribute_Throws()
        {
            using (var test = new LoadTest(_fixture))
            {
                test.CmsSigner.SignedAttributes.Add(
                    AttributeUtility.CreateCommitmentTypeIndication(SignatureType.Author));
                test.CmsSigner.SignedAttributes.Add(new Pkcs9SigningTime());

                test.SignedCms.ComputeSignature(test.CmsSigner);

                var exception = Assert.Throws<SignatureException>(
                    () => Signature.Load(test.SignedCms.Encode()));

                Assert.Equal(NuGetLogCode.NU3011, exception.Code);
                Assert.Equal("The signing-certificate-v2 attribute must be present.", exception.Message);
            }
        }

        [Fact]
        public void Load_WithAuthorSignatureWithMultipleSigningCertificateV2AttributeValues_Throws()
        {
            using (var test = new LoadTest(_fixture))
            {
                test.CmsSigner.SignedAttributes.Add(
                    AttributeUtility.CreateCommitmentTypeIndication(SignatureType.Author));
                test.CmsSigner.SignedAttributes.Add(new Pkcs9SigningTime());

                var attribute = AttributeUtility.CreateSigningCertificateV2(test.Certificate, HashAlgorithmName.SHA256);

                attribute.Values.Add(attribute.Values[0]);
                
                test.CmsSigner.SignedAttributes.Add(attribute);

                test.SignedCms.ComputeSignature(test.CmsSigner);

                var exception = Assert.Throws<SignatureException>(
                    () => Signature.Load(test.SignedCms.Encode()));

                Assert.Equal(NuGetLogCode.NU3011, exception.Code);
                Assert.Equal("Multiple signing-certificate-v2 attribute values are not allowed.", exception.Message);
            }
        }

        [Fact]
        public void Load_WithAuthorSignatureWithoutPkcs9SigningTimeAttribute_Throws()
        {
            using (var test = new LoadTest(_fixture))
            {
                test.CmsSigner.SignedAttributes.Add(
                    AttributeUtility.CreateCommitmentTypeIndication(SignatureType.Author));
                test.CmsSigner.SignedAttributes.Add(
                    AttributeUtility.CreateSigningCertificateV2(test.Certificate, HashAlgorithmName.SHA256));

                test.SignedCms.ComputeSignature(test.CmsSigner);

                var exception = Assert.Throws<SignatureException>(
                    () => Signature.Load(test.SignedCms.Encode()));

                Assert.Equal(NuGetLogCode.NU3011, exception.Code);
                Assert.Equal("The primary signature is invalid.", exception.Message);
            }
        }

        [Fact]
        public void Load_WithAuthorSignature_ReturnsSignature()
        {
            using (var test = new LoadTest(_fixture))
            {
                test.CmsSigner.SignedAttributes.Add(
                    AttributeUtility.CreateCommitmentTypeIndication(SignatureType.Author));
                test.CmsSigner.SignedAttributes.Add(new Pkcs9SigningTime());
                test.CmsSigner.SignedAttributes.Add(
                    AttributeUtility.CreateSigningCertificateV2(test.Certificate, HashAlgorithmName.SHA256));

                test.SignedCms.ComputeSignature(test.CmsSigner);

                var signature = Signature.Load(test.SignedCms.Encode());

                Assert.Equal(SignatureType.Author, signature.Type);
            }
        }

        [Fact]
        public void Load_WithGenericSignature_ReturnsSignatureWithUnknownType()
        {
            using (var test = new LoadTest(_fixture))
            {
                test.SignedCms.ComputeSignature(test.CmsSigner);

                var signature = Signature.Load(test.SignedCms.Encode());

                Assert.Equal(SignatureType.Unknown, signature.Type);
            }
        }

        private sealed class LoadTest : IDisposable
        {
            private bool _isDisposed;

            internal X509Certificate2 Certificate { get; }
            internal CmsSigner CmsSigner { get; }
            internal SignedCms SignedCms { get; }

            internal LoadTest(CertificatesFixture fixture)
            {
                Certificate = fixture.GetDefaultCertificate();

                var content = new SignatureContent(
                    SigningSpecifications.V1,
                    HashAlgorithmName.SHA256,
                    hashValue: "hash");
                var contentInfo = new ContentInfo(content.GetBytes());

                SignedCms = new SignedCms(contentInfo);
                CmsSigner = new CmsSigner(Certificate);
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    Certificate.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }
        }
    }
}
#endif