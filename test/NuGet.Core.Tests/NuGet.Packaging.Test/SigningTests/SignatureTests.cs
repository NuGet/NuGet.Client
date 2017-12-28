// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET46
using System;
using System.Collections.Generic;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignatureTests
    {
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
            using (var certificate = SigningTestUtility.GenerateCertificate("test", generator => { }))
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
            using (var test = new LoadTest())
            {
                test.CmsSigner.SignedAttributes.Add(
                    AttributeUtility.GetCommitmentTypeIndication(SignatureType.Author));
                test.CmsSigner.SignedAttributes.Add(new Pkcs9SigningTime());

                test.SignedCms.ComputeSignature(test.CmsSigner);

                var exception = Assert.Throws<SignatureException>(
                    () => Signature.Load(test.SignedCms.Encode()));

                Assert.Equal(NuGetLogCode.NU3011, exception.Code);
                Assert.Equal("The author signature is invalid.", exception.Message);
            }
        }

        [Fact]
        public void Load_WithAuthorSignatureWithoutPkcs9SigningTimeAttribute_Throws()
        {
            using (var test = new LoadTest())
            {
                var chain = new List<X509Certificate2>() { test.Certificate };

                test.CmsSigner.SignedAttributes.Add(
                    AttributeUtility.GetCommitmentTypeIndication(SignatureType.Author));
                test.CmsSigner.SignedAttributes.Add(
                    AttributeUtility.GetSigningCertificateV2(chain, HashAlgorithmName.SHA256));

                test.SignedCms.ComputeSignature(test.CmsSigner);

                var exception = Assert.Throws<SignatureException>(
                    () => Signature.Load(test.SignedCms.Encode()));

                Assert.Equal(NuGetLogCode.NU3011, exception.Code);
                Assert.Equal("The author signature is invalid.", exception.Message);
            }
        }

        [Fact]
        public void Load_WithAuthorSignature_ReturnSignature()
        {
            using (var test = new LoadTest())
            {
                var chain = new List<X509Certificate2>() { test.Certificate };

                test.CmsSigner.SignedAttributes.Add(
                    AttributeUtility.GetCommitmentTypeIndication(SignatureType.Author));
                test.CmsSigner.SignedAttributes.Add(new Pkcs9SigningTime());
                test.CmsSigner.SignedAttributes.Add(
                    AttributeUtility.GetSigningCertificateV2(chain, HashAlgorithmName.SHA256));

                test.SignedCms.ComputeSignature(test.CmsSigner);

                var signature = Signature.Load(test.SignedCms.Encode());

                Assert.Equal(SignatureType.Author, signature.Type);
            }
        }

        private sealed class LoadTest : IDisposable
        {
            private bool _isDisposed;

            internal X509Certificate2 Certificate { get; }
            internal CmsSigner CmsSigner { get; }
            internal SignedCms SignedCms { get; }

            internal LoadTest()
            {
                Certificate = SigningTestUtility.GenerateCertificate("test", generator => { });

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