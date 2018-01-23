// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignPackageRequestTests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;
        private static readonly Uri _validV3ServiceIndexUrl = new Uri("https://test.test", UriKind.Absolute);
        private static readonly IReadOnlyList<string> _validPackageOwners = new[] { "a", "b", "c" };

        public SignPackageRequestTests(CertificatesFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
        }

        [Fact]
        public void Constructor_CertificateSignatureHashAlgorithm_WhenCertificateNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SignPackageRequest(certificate: null, signatureHashAlgorithm: HashAlgorithmName.SHA256));

            Assert.Equal("certificate", exception.ParamName);
        }

        [Fact]
        public void Constructor_CertificateSignatureHashAlgorithm_WhenSignatureHashAlgorithmInvalid_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => new SignPackageRequest(certificate, HashAlgorithmName.Unknown));

                Assert.Equal("signatureHashAlgorithm", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_CertificateSignatureHashAlgorithm_WithValidInput_InitializesProperties()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var request = new SignPackageRequest(certificate, HashAlgorithmName.SHA512);

                Assert.Equal(SignatureType.Author, request.SignatureType);
                Assert.Equal(SignaturePlacement.PrimarySignature, request.SignaturePlacement);
                Assert.Same(certificate, request.Certificate);
                Assert.Equal(HashAlgorithmName.SHA512, request.SignatureHashAlgorithm);
                Assert.Equal(HashAlgorithmName.SHA512, request.TimestampHashAlgorithm);
                Assert.Null(request.V3ServiceIndexUrl);
                Assert.Null(request.PackageOwners);
            }
        }

        [Fact]
        public void Constructor_CertificateSignatureHashAlgorithmTimestampHashAlgorithm_WhenCertificateNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SignPackageRequest(
                    certificate: null,
                    signatureHashAlgorithm: HashAlgorithmName.SHA256,
                    timestampHashAlgorithm: HashAlgorithmName.SHA256));

            Assert.Equal("certificate", exception.ParamName);
        }

        [Fact]
        public void Constructor_CertificateSignatureHashAlgorithmTimestampHashAlgorithm_WhenSignatureHashAlgorithmInvalid_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => new SignPackageRequest(
                        certificate,
                        HashAlgorithmName.Unknown,
                        HashAlgorithmName.SHA256));

                Assert.Equal("signatureHashAlgorithm", exception.ParamName);
                Assert.StartsWith("The argument is invalid.", exception.Message);
            }
        }

        [Fact]
        public void Constructor_CertificateSignatureHashAlgorithmTimestampHashAlgorithm_WhenTimestampHashAlgorithmInvalid_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => new SignPackageRequest(
                        certificate,
                        HashAlgorithmName.SHA256,
                        HashAlgorithmName.Unknown));

                Assert.Equal("timestampHashAlgorithm", exception.ParamName);
                Assert.StartsWith("The argument is invalid.", exception.Message);
            }
        }

        [Fact]
        public void Constructor_CertificateSignatureHashAlgorithmTimestampHashAlgorithm_WithValidInput_InitializesProperties()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var request = new SignPackageRequest(certificate, HashAlgorithmName.SHA512, HashAlgorithmName.SHA256);

                Assert.Equal(SignatureType.Author, request.SignatureType);
                Assert.Equal(SignaturePlacement.PrimarySignature, request.SignaturePlacement);
                Assert.Same(certificate, request.Certificate);
                Assert.Equal(HashAlgorithmName.SHA512, request.SignatureHashAlgorithm);
                Assert.Equal(HashAlgorithmName.SHA256, request.TimestampHashAlgorithm);
                Assert.Null(request.V3ServiceIndexUrl);
                Assert.Null(request.PackageOwners);
            }
        }

        [Fact]
        public void Constructor_RepositoryOverload_WhenCertificateNull_Throws()
        {
            X509Certificate2 certificate = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new SignPackageRequest(
                    certificate,
                    HashAlgorithmName.SHA256,
                    HashAlgorithmName.SHA256,
                    SignaturePlacement.PrimarySignature,
                    new Uri("https://test.test"),
                    packageOwners: null));

            Assert.Equal("certificate", exception.ParamName);
        }

        [Theory]
        [InlineData(HashAlgorithmName.Unknown)]
        [InlineData((HashAlgorithmName)int.MinValue)]
        public void Constructor_RepositoryOverload_WithInvalidSignatureHashAlgorithm_Throws(HashAlgorithmName signatureHashAlgorithm)
        {
            using (var certificate = new X509Certificate2())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => new SignPackageRequest(
                        certificate,
                        signatureHashAlgorithm,
                        HashAlgorithmName.SHA256,
                        SignaturePlacement.PrimarySignature,
                        _validV3ServiceIndexUrl,
                        _validPackageOwners));

                Assert.Equal("signatureHashAlgorithm", exception.ParamName);
            }
        }

        [Theory]
        [InlineData(HashAlgorithmName.Unknown)]
        [InlineData((HashAlgorithmName)int.MinValue)]
        public void Constructor_RepositoryOverload_WithInvalidTimestampHashAlgorithm_Throws(HashAlgorithmName timestampHashAlgorithm)
        {
            using (var certificate = new X509Certificate2())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => new SignPackageRequest(
                        certificate,
                        HashAlgorithmName.SHA256,
                        timestampHashAlgorithm,
                        SignaturePlacement.PrimarySignature,
                        _validV3ServiceIndexUrl,
                        _validPackageOwners));

                Assert.Equal("timestampHashAlgorithm", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_RepositoryOverload_WithInvalidSignaturePlacement_Throws()
        {
            using (var certificate = new X509Certificate2())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => new SignPackageRequest(
                        certificate,
                        HashAlgorithmName.SHA256,
                        HashAlgorithmName.SHA256,
                        (SignaturePlacement)int.MinValue,
                        _validV3ServiceIndexUrl,
                        _validPackageOwners));

                Assert.Equal("signaturePlacement", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_RepositoryOverload_WhenV3ServiceIndexUrlNull_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new SignPackageRequest(
                        certificate,
                        HashAlgorithmName.SHA256,
                        HashAlgorithmName.SHA256,
                        SignaturePlacement.PrimarySignature,
                        v3ServiceIndexUrl: null,
                        packageOwners: _validPackageOwners));

                Assert.Equal("v3ServiceIndexUrl", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_RepositoryOverload__WhenV3ServiceIndexUrlNotAbsolute_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => new SignPackageRequest(
                        certificate,
                        HashAlgorithmName.SHA256,
                        HashAlgorithmName.SHA256,
                        SignaturePlacement.PrimarySignature,
                        new Uri("/", UriKind.Relative),
                        _validPackageOwners));

                Assert.Equal("v3ServiceIndexUrl", exception.ParamName);
                Assert.StartsWith("The URL value is invalid.", exception.Message);
            }
        }

        [Fact]
        public void Constructor_RepositoryOverload__WhenV3ServiceIndexUrlNotHttps_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => new SignPackageRequest(
                        certificate,
                        HashAlgorithmName.SHA256,
                        HashAlgorithmName.SHA256,
                        SignaturePlacement.PrimarySignature,
                        new Uri("http://test.test", UriKind.Absolute),
                        _validPackageOwners));

                Assert.Equal("v3ServiceIndexUrl", exception.ParamName);
                Assert.StartsWith("The URL value is invalid.", exception.Message);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_RepositoryOverload_WhenPackageOwnersContainsInvalidValue_Throws(string packageOwner)
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => new SignPackageRequest(
                        certificate,
                        HashAlgorithmName.SHA256,
                        HashAlgorithmName.SHA256,
                        SignaturePlacement.PrimarySignature,
                        _validV3ServiceIndexUrl,
                        new string[] { packageOwner }));

                Assert.Equal("packageOwners", exception.ParamName);
                Assert.StartsWith("One or more package owner values are invalid.", exception.Message);
            }
        }

        [Fact]
        public void Constructor_RepositoryOverload_WithValidInput_InitializesProperties()
        {
            using (var certificate = new X509Certificate2())
            using (var request = new SignPackageRequest(
                certificate,
                HashAlgorithmName.SHA256,
                HashAlgorithmName.SHA384,
                SignaturePlacement.Countersignature,
                _validV3ServiceIndexUrl,
                _validPackageOwners))
            {
                Assert.Equal(SignatureType.Repository, request.SignatureType);
                Assert.Equal(SignaturePlacement.Countersignature, request.SignaturePlacement);
                Assert.Same(certificate, request.Certificate);
                Assert.Equal(HashAlgorithmName.SHA256, request.SignatureHashAlgorithm);
                Assert.Equal(HashAlgorithmName.SHA384, request.TimestampHashAlgorithm);
                Assert.Equal(_validV3ServiceIndexUrl.OriginalString, request.V3ServiceIndexUrl.OriginalString);
                Assert.Equal(_validPackageOwners, request.PackageOwners);
            }
        }
    }
}