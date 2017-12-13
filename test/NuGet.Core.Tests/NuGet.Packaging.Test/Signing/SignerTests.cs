// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET46
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignerTests
    {
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

                Assert.Equal(NuGetLogCode.NU3022, exception.AsLogMessage().Code);
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

                Assert.Equal(NuGetLogCode.NU3023, exception.AsLogMessage().Code);
            }
        }

        private sealed class SignTest : IDisposable
        {
            private bool _isDisposed = false;

            internal Mock<ISignatureProvider> SignatureProvider { get; }
            internal Mock<ISignedPackage> Package { get; }
            internal SignPackageRequest Request { get; }
            internal Signer Signer { get; }

            private SignTest(Signer signer,
                Mock<ISignedPackage> package,
                Mock<ISignatureProvider> signatureProvider,
                SignPackageRequest request)
            {
                Signer = signer;
                Package = package;
                SignatureProvider = signatureProvider;
                Request = request;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    Request.Dispose();

                    _isDisposed = true;
                }
            }

            internal static SignTest Create(X509Certificate2 certificate, HashAlgorithmName hashAlgorithm)
            {
                var signedPackage = new Mock<ISignedPackage>(MockBehavior.Strict);
                var signatureProvider = new Mock<ISignatureProvider>(MockBehavior.Strict);
                var signer = new Signer(signedPackage.Object, signatureProvider.Object);

                var request = new SignPackageRequest()
                {
                    Certificate = certificate,
                    SignatureHashAlgorithm = hashAlgorithm
                };

                return new SignTest(signer, signedPackage, signatureProvider, request);
            }
        }
    }
}
#endif