// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignPackageRequestTests
    {
        [Fact]
        public void Constructor_WhenCertificateNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SignPackageRequest(
                    certificate: null,
                    signatureHashAlgorithm: HashAlgorithmName.SHA256,
                    timestampHashAlgorithm: HashAlgorithmName.SHA256,
                    signatureType: SignatureType.Author));

            Assert.Equal("certificate", exception.ParamName);
        }

        [Theory]
        [InlineData(HashAlgorithmName.Unknown)]
        [InlineData((HashAlgorithmName)int.MinValue)]
        public void Constructor_WithInvalidSignatureHashAlgorithm_Throws(HashAlgorithmName signatureHashAlgorithm)
        {
            using (var certificate = new X509Certificate2())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => new SignPackageRequest(
                        certificate,
                        signatureHashAlgorithm,
                        HashAlgorithmName.SHA256,
                        SignatureType.Author));

                Assert.Equal("signatureHashAlgorithm", exception.ParamName);
            }
        }

        [Theory]
        [InlineData(HashAlgorithmName.Unknown)]
        [InlineData((HashAlgorithmName)int.MinValue)]
        public void Constructor_WithInvalidTimestampHashAlgorithm_Throws(HashAlgorithmName timestampHashAlgorithm)
        {
            using (var certificate = new X509Certificate2())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => new SignPackageRequest(
                        certificate,
                        HashAlgorithmName.SHA256,
                        timestampHashAlgorithm,
                        SignatureType.Author));

                Assert.Equal("timestampHashAlgorithm", exception.ParamName);
            }
        }

        [Theory]
        [InlineData(SignatureType.Unknown)]
        [InlineData((SignatureType)int.MinValue)]
        public void Constructor_WithInvalidSignatureType_Throws(SignatureType signatureType)
        {
            using (var certificate = new X509Certificate2())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => new SignPackageRequest(
                        certificate,
                        HashAlgorithmName.SHA256,
                        HashAlgorithmName.SHA256,
                        signatureType));

                Assert.Equal("signatureType", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_WithValidInput_InitializesProperties()
        {
            using (var certificate = new X509Certificate2())
            using (var request = new SignPackageRequest(
                certificate,
                HashAlgorithmName.SHA256,
                HashAlgorithmName.SHA384,
                SignatureType.Author))
            {
                Assert.Same(certificate, request.Certificate);
                Assert.Equal(HashAlgorithmName.SHA256, request.SignatureHashAlgorithm);
                Assert.Equal(HashAlgorithmName.SHA384, request.TimestampHashAlgorithm);
                Assert.Equal(SignatureType.Author, request.SignatureType);
            }
        }
    }
}