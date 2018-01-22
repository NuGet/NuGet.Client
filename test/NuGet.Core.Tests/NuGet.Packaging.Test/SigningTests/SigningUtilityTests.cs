// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SigningUtilityTests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public SigningUtilityTests(CertificatesFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
        }

        [Fact]
        public void Verify_WhenRequestNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => SigningUtility.Verify(request: null));

            Assert.Equal("request", exception.ParamName);
        }

        [Fact]
        public void Verify_WithCertificateWithUnsupportedSignatureAlgorithm_Throws()
        {
            using (var certificate = _fixture.GetRsaSsaPssCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<SignatureException>(() => SigningUtility.Verify(request));

                Assert.Equal(NuGetLogCode.NU3013, exception.Code);
                Assert.Equal("The signing certificate has an unsupported signature algorithm.", exception.Message);
            }
        }

        [Fact]
        public void Verify_WithCertificateWithLifetimeSigningEku_Throws()
        {
            using (var certificate = _fixture.GetLifetimeSigningCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<SignatureException>(() => SigningUtility.Verify(request));

                Assert.Equal(NuGetLogCode.NU3015, exception.Code);
                Assert.Equal("The lifetime signing EKU is not supported.", exception.Message);
            }
        }

        [Fact]
        public void Verify_WithNotYetValidCertificate_Throws()
        {
            using (var certificate = _fixture.GetNotYetValidCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<SignatureException>(() => SigningUtility.Verify(request));

                Assert.Equal(NuGetLogCode.NU3017, exception.Code);
                Assert.Equal("The signing certificate is not yet valid.", exception.Message);
            }
        }

        [Fact]
        public void Verify_WithUntrustedRoot_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<SignatureException>(
                    () => SigningUtility.Verify(request));

                Assert.Equal(NuGetLogCode.NU3018, exception.Code);
            }
        }

#if IS_DESKTOP
        [Fact]
        public void CreateSignedAttributes_WhenRequestNull_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => SigningUtility.CreateSignedAttributes(
                        request: null,
                        chainList: new[] { certificate }));

                Assert.Equal("request", exception.ParamName);
            }
        }

        [Fact]
        public void CreateSignedAttributes_WhenChainListNull_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => SigningUtility.CreateSignedAttributes(request, chainList: null));

                Assert.Equal("chainList", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }
        }

        [Fact]
        public void CreateSignedAttributes_WhenChainListEmpty_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => SigningUtility.CreateSignedAttributes(request, new X509Certificate2[0]));

                Assert.Equal("chainList", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }
        }

        [Fact]
        public void CreateSignedAttributes_WithValidInput_ReturnsAttributes()
        {
            using (var rootCertificate = SignTestUtility.GetCertificate("root.crt"))
            using (var intermediateCertificate = SignTestUtility.GetCertificate("intermediate.crt"))
            using (var leafCertificate = SignTestUtility.GetCertificate("leaf.crt"))
            using (var request = CreateRequest(leafCertificate))
            {
                var certList = new[] { leafCertificate, intermediateCertificate, rootCertificate };
                var attributes = SigningUtility.CreateSignedAttributes(request, certList);

                Assert.Equal(3, attributes.Count);

                VerifyAttributes(attributes, request);
            }
        }

        [Fact]
        public void CreateSignedAttributesForRepository_WhenRequestNull_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => SigningUtility.CreateSignedAttributesForRepository(
                        request: null,
                        chainList: new[] { certificate },
                        v3ServiceIndexUrl: new Uri("https://test.test"),
                        packageOwners: new[] { "a" }));

                Assert.Equal("request", exception.ParamName);
            }
        }

        [Fact]
        public void CreateSignedAttributesForRepository_WhenChainListNull_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => SigningUtility.CreateSignedAttributesForRepository(
                        request,
                        chainList: null,
                        v3ServiceIndexUrl: new Uri("https://test.test"),
                        packageOwners: new[] { "a" }));

                Assert.Equal("chainList", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }
        }

        [Fact]
        public void CreateSignedAttributesForRepository_WhenChainListEmpty_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => SigningUtility.CreateSignedAttributesForRepository(
                        request,
                        new X509Certificate2[0],
                        new Uri("https://test.test"),
                        new[] { "a" }));

                Assert.Equal("chainList", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }
        }

        [Fact]
        public void CreateSignedAttributesForRepository_WhenV3ServiceIndexUrlNull_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => SigningUtility.CreateSignedAttributesForRepository(
                        request,
                        new[] { certificate },
                        v3ServiceIndexUrl: null,
                        packageOwners: new[] { "a" }));

                Assert.Equal("v3ServiceIndexUrl", exception.ParamName);
            }
        }

        [Fact]
        public void CreateSignedAttributesForRepository_WheV3ServiceIndexUrlNotAbsolute_Throws()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => SigningUtility.CreateSignedAttributesForRepository(
                        request,
                        new[] { certificate },
                        new Uri("/", UriKind.Relative),
                        new[] { "a" }));

                Assert.Equal("v3ServiceIndexUrl", exception.ParamName);
                Assert.StartsWith("The URL value is invalid.", exception.Message);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void CreateSignedAttributesForRepository_WhenPackageOwnersContainsInvalidValue_Throws(string packageOwner)
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var packageOwners = new[] { packageOwner };

                var exception = Assert.Throws<ArgumentException>(
                    () => SigningUtility.CreateSignedAttributesForRepository(
                        request,
                        new[] { certificate },
                        new Uri("https://test.test", UriKind.Absolute),
                        packageOwners));

                Assert.Equal("packageOwners", exception.ParamName);
                Assert.StartsWith("One or more package owner values are invalid.", exception.Message);
            }
        }

        [Fact]
        public void CreateSignedAttributesForRepository_WhenPackageOwnersNull_ReturnsAttributes()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var v3ServiceIndexUrl = new Uri("https://test.test", UriKind.Absolute);
                IReadOnlyList<string> packageOwners = null;

                var attributes = SigningUtility.CreateSignedAttributesForRepository(
                    request,
                    new[] { certificate },
                    v3ServiceIndexUrl,
                    packageOwners);

                Assert.Equal(4, attributes.Count);

                VerifyAttributesRepository(attributes, request, v3ServiceIndexUrl, packageOwners);
            }
        }

        [Fact]
        public void CreateSignedAttributesForRepository_WhenPackageOwnersEmpty_ReturnsAttributes()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var v3ServiceIndexUrl = new Uri("https://test.test", UriKind.Absolute);
                var packageOwners = new string[0];

                var attributes = SigningUtility.CreateSignedAttributesForRepository(
                    request,
                    new[] { certificate },
                    v3ServiceIndexUrl,
                    new string[0]);

                Assert.Equal(4, attributes.Count);

                VerifyAttributesRepository(attributes, request, v3ServiceIndexUrl, packageOwners);
            }
        }

        [Fact]
        public void CreateSignedAttributesForRepository_WhenPackageOwnersNonEmpty_ReturnsAttributes()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            using (var request = CreateRequest(certificate))
            {
                var v3ServiceIndexUrl = new Uri("https://test.test", UriKind.Absolute);
                var packageOwners = new[] { "a" };

                var attributes = SigningUtility.CreateSignedAttributesForRepository(
                    request,
                    new[] { certificate },
                    v3ServiceIndexUrl,
                    packageOwners);

                Assert.Equal(5, attributes.Count);

                VerifyAttributesRepository(attributes, request, v3ServiceIndexUrl, packageOwners);
            }
        }

        private static void VerifyAttributes(
            CryptographicAttributeObjectCollection attributes,
            SignPackageRequest request)
        {
            var pkcs9SigningTimeAttributeFound = false;
            var commitmentTypeIndicationAttributeFound = false;
            var signingCertificateV2AttributeFound = false;

            foreach (var attribute in attributes)
            {
                Assert.Equal(1, attribute.Values.Count);

                switch (attribute.Oid.Value)
                {
                    case "1.2.840.113549.1.9.5": // PKCS #9 signing time
                        Assert.IsType<Pkcs9SigningTime>(attribute.Values[0]);

                        pkcs9SigningTimeAttributeFound = true;
                        break;

                    case Oids.CommitmentTypeIndication:
                        var qualifier = CommitmentTypeQualifier.Read(attribute.Values[0].RawData);

                        Assert.Equal(Oids.CommitmentTypeIdentifierProofOfOrigin, qualifier.CommitmentTypeIdentifier.Value);

                        commitmentTypeIndicationAttributeFound = true;
                        break;

                    case Oids.SigningCertificateV2:
                        var signingCertificateV2 = SigningCertificateV2.Read(attribute.Values[0].RawData);

                        Assert.Equal(1, signingCertificateV2.Certificates.Count);

                        var essCertIdV2 = signingCertificateV2.Certificates[0];

                        Assert.Equal(SignTestUtility.GetHash(request.Certificate, request.SignatureHashAlgorithm), essCertIdV2.CertificateHash);
                        Assert.Equal(request.SignatureHashAlgorithm.ConvertToOidString(), essCertIdV2.HashAlgorithm.Algorithm.Value);
                        Assert.Equal(request.Certificate.IssuerName.Name, essCertIdV2.IssuerSerial.GeneralNames[0].DirectoryName.Name);
                        SignTestUtility.VerifySerialNumber(request.Certificate, essCertIdV2.IssuerSerial);
                        Assert.Null(signingCertificateV2.Policies);

                        signingCertificateV2AttributeFound = true;
                        break;
                }
            }

            Assert.True(pkcs9SigningTimeAttributeFound);
            Assert.True(commitmentTypeIndicationAttributeFound);
            Assert.True(signingCertificateV2AttributeFound);
        }

        private static void VerifyAttributesRepository(
            CryptographicAttributeObjectCollection attributes,
            SignPackageRequest request,
            Uri v3ServiceIndexUrl,
            IReadOnlyList<string> packageOwners)
        {
            VerifyAttributes(attributes, request);

            var nugetV3ServiceIndexUrlAttributeFound = false;
            var nugetPackageOwnersAttributeFound = false;

            foreach (var attribute in attributes)
            {
                Assert.Equal(1, attribute.Values.Count);

                switch (attribute.Oid.Value)
                {
                    case Oids.NuGetV3ServiceIndexUrl:
                        var nugetV3ServiceIndexUrl = NuGetV3ServiceIndexUrl.Read(attribute.Values[0].RawData);

                        Assert.True(nugetV3ServiceIndexUrl.V3ServiceIndexUrl.IsAbsoluteUri);
                        Assert.Equal(v3ServiceIndexUrl.OriginalString, nugetV3ServiceIndexUrl.V3ServiceIndexUrl.OriginalString);

                        nugetV3ServiceIndexUrlAttributeFound = true;
                        break;

                    case Oids.NuGetPackageOwners:
                        var nugetPackageOwners = NuGetPackageOwners.Read(attribute.Values[0].RawData);

                        Assert.Equal(packageOwners, nugetPackageOwners.PackageOwners);

                        nugetPackageOwnersAttributeFound = true;
                        break;
                }
            }

            Assert.True(nugetV3ServiceIndexUrlAttributeFound);
            Assert.Equal(packageOwners != null && packageOwners.Count > 0, nugetPackageOwnersAttributeFound);
        }
#endif

        private static SignPackageRequest CreateRequest(X509Certificate2 certificate)
        {
            return new SignPackageRequest(
                certificate,
                Common.HashAlgorithmName.SHA256,
                Common.HashAlgorithmName.SHA256);
        }
    }
}