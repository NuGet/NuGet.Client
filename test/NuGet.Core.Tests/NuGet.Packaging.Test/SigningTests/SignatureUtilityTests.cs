// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET46
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.X509.Store;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignatureUtilityTests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public SignatureUtilityTests(CertificatesFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
        }

        [Fact]
        public void GetPrimarySignatureCertificates_WhenSignatureNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => SignatureUtility.GetPrimarySignatureCertificates(signature: null));

            Assert.Equal("signature", exception.ParamName);
        }

        [Fact]
        public void GetPrimarySignatureCertificates_WithAuthorSignature_ReturnsCertificates()
        {
            var signature = Signature.Load(SignTestUtility.GetResourceBytes("SignatureWithTimestamp.p7s"));

            var certificates = SignatureUtility.GetPrimarySignatureCertificates(signature);

            Assert.Equal(3, certificates.Count);
            Assert.Equal("8219f5772ef562a3ea9b90da00ca7b9523a96fbf", certificates[0].Thumbprint, StringComparer.OrdinalIgnoreCase);
            Assert.Equal("d8198d59087bbe6a6e7d69af62030145366be93e", certificates[1].Thumbprint, StringComparer.OrdinalIgnoreCase);
            Assert.Equal("6d73d582b73b5b3b18a27506acceedea75ab63c2", certificates[2].Thumbprint, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPrimarySignatureCertificates_WithUnknownSignature_ReturnsCertificates()
        {
            using (var directory = TestDirectory.Create())
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var packageContext = new SimpleTestPackageContext();
                var unsignedPackageFile = packageContext.CreateAsFile(directory, "Package.nupkg");
                var signedPackageFile = await SignedArchiveTestUtility.SignPackageFileWithBasicSignedCmsAsync(
                    directory,
                    unsignedPackageFile,
                    certificate);

                using (var packageReader = new PackageArchiveReader(signedPackageFile.FullName))
                {
                    var signature = await packageReader.GetSignatureAsync(CancellationToken.None);

                    var certificates = SignatureUtility.GetPrimarySignatureCertificates(signature);

                    Assert.Equal(1, certificates.Count);
                    Assert.Equal(certificate.RawData, certificates[0].RawData);
                }
            }
        }

        [Fact]
        public void GetPrimarySignatureTimestampSignatureCertificates_WhenSignatureNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => SignatureUtility.GetPrimarySignatureTimestampSignatureCertificates(signature: null));

            Assert.Equal("signature", exception.ParamName);
        }

        [Fact]
        public void GetPrimarySignatureTimestampSignatureCertificates_WithValidTimestamp_ReturnsCertificates()
        {
            var signature = Signature.Load(SignTestUtility.GetResourceBytes("SignatureWithTimestamp.p7s"));

            var certificates = SignatureUtility.GetPrimarySignatureTimestampSignatureCertificates(signature);

            Assert.Equal(3, certificates.Count);
            Assert.Equal("ff162bef155cb3d5b5962bbe084b21fc4d740001", certificates[0].Thumbprint, StringComparer.OrdinalIgnoreCase);
            Assert.Equal("2aa752fe64c49abe82913c463529cf10ff2f04ee", certificates[1].Thumbprint, StringComparer.OrdinalIgnoreCase);
            Assert.Equal("3b1efd3a66ea28b16697394703a72ca340a05bd5", certificates[2].Thumbprint, StringComparer.OrdinalIgnoreCase);
        }

        private static Signature GenerateSignatureWithNoCertificates(Signature signature)
        {
            var certificateStore = X509StoreFactory.Create(
                "Certificate/Collection",
                new X509CollectionStoreParameters(Array.Empty<Org.BouncyCastle.X509.X509Certificate>()));
            var crlStore = X509StoreFactory.Create(
                "CRL/Collection",
                new X509CollectionStoreParameters(Array.Empty<Org.BouncyCastle.X509.X509Crl>()));
            var bytes = signature.SignedCms.Encode();

            using (var readStream = new MemoryStream(bytes))
            using (var writeStream = new MemoryStream())
            {
                CmsSignedDataParser.ReplaceCertificatesAndCrls(
                    readStream,
                    certificateStore,
                    crlStore,
                    certificateStore,
                    writeStream);

                return Signature.Load(writeStream.ToArray());
            }
        }
    }
}
#endif