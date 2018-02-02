// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET46
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignerTests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public SignerTests(CertificatesFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
        }

        [Fact]
        public void Constructor_WhenPackageIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new Signer(package: null, signatureProvider: Mock.Of<ISignatureProvider>()));

            Assert.Equal("package", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenSignatureProviderIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new Signer(Mock.Of<ISignedPackage>(), signatureProvider: null));

            Assert.Equal("signatureProvider", exception.ParamName);
        }

        [Fact]
        public async Task SignAsync_WhenRequestIsNull_Throws()
        {
            using (var test = SignTest.Create(new X509Certificate2(), HashAlgorithmName.SHA256))
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Signer.SignAsync(
                        request: null,
                        logger: Mock.Of<ILogger>(),
                        token: CancellationToken.None));

                Assert.Equal("request", exception.ParamName);
            }
        }

        [Fact]
        public async Task SignAsync_WhenLoggerIsNull_Throws()
        {
            using (var test = SignTest.Create(new X509Certificate2(), HashAlgorithmName.SHA256))
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Signer.SignAsync(
                        test.Request,
                        logger: null,
                        token: CancellationToken.None));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public async Task SignAsync_WhenCancellationTokenIsCancelled_Throws()
        {
            using (var test = SignTest.Create(new X509Certificate2(), HashAlgorithmName.SHA256))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Signer.SignAsync(
                        test.Request,
                        Mock.Of<ILogger>(),
                        new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task SignAsync_WhenCertificateSignatureAlgorithmIsUnsupported_Throws()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate(
                "test",
                generator => { },
                "SHA256WITHRSAANDMGF1"))
            using (var test = SignTest.Create(certificate, HashAlgorithmName.SHA256))
            {
                var exception = await Assert.ThrowsAsync<SignatureException>(
                    () => test.Signer.SignAsync(
                        test.Request,
                        Mock.Of<ILogger>(),
                        CancellationToken.None));

                Assert.Equal(NuGetLogCode.NU3013, exception.Code);
                Assert.Equal("The signing certificate has an unsupported signature algorithm.", exception.Message);
            }
        }

        [Fact]
        public async Task SignAsync_WhenCertificatePublicKeyLengthIsUnsupported_Throws()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate(
                "test",
                generator => { },
                publicKeyLength: 1024))
            using (var test = SignTest.Create(certificate, HashAlgorithmName.SHA256))
            {
                var exception = await Assert.ThrowsAsync<SignatureException>(
                    () => test.Signer.SignAsync(
                        test.Request,
                        Mock.Of<ILogger>(),
                        CancellationToken.None));

                Assert.Equal(NuGetLogCode.NU3014, exception.Code);
                Assert.Equal("The signing certificate does not meet a minimum public key length requirement.", exception.Message);
            }
        }

        [Fact]
        public async Task SignAsync_WhenPackageIsZip64_Throws()
        {
            using (var test = SignTest.Create(
                _fixture.GetDefaultCertificate(),
                HashAlgorithmName.SHA256,
                GetResource("CentralDirectoryHeaderWithZip64ExtraField.zip")))
            {
                var exception = await Assert.ThrowsAsync<SignatureException>(
                    () => test.Signer.SignAsync(
                        test.Request,
                        Mock.Of<ILogger>(),
                        CancellationToken.None));

                Assert.Equal(NuGetLogCode.NU3006, exception.Code);
                Assert.Equal("Signed Zip64 packages are not supported.", exception.Message);
            }
        }

        [Fact]
        public async Task SignAsync_WhenChainBuildingFails_Throws()
        {
            using (var packageStream = new SimpleTestPackageContext().CreateAsStream())
            using (var test = SignTest.Create(
                 _fixture.GetExpiredCertificate(),
                HashAlgorithmName.SHA256,
                packageStream.ToArray(),
                new X509SignatureProvider(timestampProvider: null)))
            {
                var logger = new TestLogger();

                var exception = await Assert.ThrowsAsync<SignatureException>(
                    () => test.Signer.SignAsync(test.Request, logger, CancellationToken.None));

                Assert.Equal(NuGetLogCode.NU3018, exception.Code);
                Assert.Equal("Certificate chain validation failed.", exception.Message);

                Assert.Equal(1, logger.Errors);
                Assert.Equal(1, logger.Warnings);
                Assert.Contains(logger.LogMessages, message =>
                    message.Code == NuGetLogCode.NU3018 &&
                    message.Level == LogLevel.Error &&
                    message.Message == "A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file.");
                Assert.Contains(logger.LogMessages, message =>
                    message.Code == NuGetLogCode.NU3018 &&
                    message.Level == LogLevel.Warning &&
                    message.Message == "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.");
            }
        }

        [Fact]
        public async Task SignAsync_WithUntrustedSelfSignedCertificate_Succeeds()
        {
            using (var packageStream = new SimpleTestPackageContext().CreateAsStream())
            using (var test = SignTest.Create(
                 _fixture.GetDefaultCertificate(),
                HashAlgorithmName.SHA256,
                packageStream.ToArray(),
                new X509SignatureProvider(timestampProvider: null)))
            {
                var logger = new TestLogger();

                await test.Signer.SignAsync(test.Request, logger, CancellationToken.None);

                Assert.True(await test.IsSignedAsync());

                Assert.Equal(0, logger.Errors);
                Assert.Equal(1, logger.Warnings);
                Assert.Equal(1, logger.Messages.Count());
                Assert.True(logger.Messages.Contains("A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider."));
            }
        }

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"NuGet.Packaging.Test.compiler.resources.{name}",
                typeof(SignerTests));
        }

        private sealed class SignTest : IDisposable
        {
            private readonly MemoryStream _readStream;
            private readonly MemoryStream _writeStream;
            private bool _isDisposed = false;

            internal ISignatureProvider SignatureProvider { get; }
            internal ISignedPackage Package { get; }
            internal SignPackageRequest Request { get; }
            internal Signer Signer { get; }

            private SignTest(Signer signer,
                ISignedPackage package,
                ISignatureProvider signatureProvider,
                SignPackageRequest request,
                MemoryStream readStream,
                MemoryStream writeStream)
            {
                Signer = signer;
                Package = package;
                SignatureProvider = signatureProvider;
                Request = request;
                _readStream = readStream;
                _writeStream = writeStream;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    Request.Dispose();

                    if (Package is SignedPackageArchive)
                    {
                        Package.Dispose();
                    }

                    _readStream?.Dispose();
                    _writeStream?.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }

            internal static SignTest Create(
                X509Certificate2 certificate,
                HashAlgorithmName hashAlgorithm,
                byte[] package = null,
                ISignatureProvider signatureProvider = null)
            {
                ISignedPackage signedPackage;
                MemoryStream readStream = null;
                MemoryStream writeStream = null;

                if (package == null)
                {
                    signedPackage = Mock.Of<ISignedPackage>();
                }
                else
                {
                    readStream = new MemoryStream(package);
                    writeStream = new MemoryStream();
                    signedPackage = new SignedPackageArchive(readStream, writeStream);
                }

                signatureProvider = signatureProvider ?? Mock.Of<ISignatureProvider>();
                var signer = new Signer(signedPackage, signatureProvider);
                var request = new SignPackageRequest(certificate, signatureHashAlgorithm: hashAlgorithm);

                return new SignTest(
                    signer,
                    signedPackage,
                    signatureProvider,
                    request,
                    readStream,
                    writeStream);
            }

            internal Task<bool> IsSignedAsync()
            {
                using (var unused = new MemoryStream())
                {
                    var signedPackage = new SignedPackageArchive(_writeStream, unused);

                    return signedPackage.IsSignedAsync(CancellationToken.None);
                }
            }
        }
    }
}
#endif