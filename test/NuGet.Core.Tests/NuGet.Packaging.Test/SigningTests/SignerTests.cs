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
        public void Constructor_WhenRequestIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new Signer(options: null));

            Assert.Equal("request", exception.ParamName);
        }

        [Fact]
        public async Task SignAsync_WhenCancellationTokenIsCancelled_Throws()
        {
            using (var test = SignTest.Create(new X509Certificate2(), HashAlgorithmName.SHA256))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Signer.SignAsync(
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
                var exception = await Assert.ThrowsAsync<SignatureException>(
                    () => test.Signer.SignAsync(CancellationToken.None));

                Assert.Equal(NuGetLogCode.NU3018, exception.Code);
                Assert.Equal("Certificate chain validation failed.", exception.Message);

                Assert.Equal(1, test.Logger.Errors);
                Assert.Equal(1, test.Logger.Warnings);
                Assert.Contains(test.Logger.LogMessages, message =>
                    message.Code == NuGetLogCode.NU3018 &&
                    message.Level == LogLevel.Error &&
                    message.Message == "A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file.");
                Assert.Contains(test.Logger.LogMessages, message =>
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
                await test.Signer.SignAsync(CancellationToken.None);

                Assert.True(await test.IsSignedAsync());

                Assert.Equal(0, test.Logger.Errors);
                Assert.Equal(1, test.Logger.Warnings);
                Assert.Equal(1, test.Logger.Messages.Count());
                Assert.True(test.Logger.Messages.Contains("A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider."));
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
            private bool _isDisposed = false;
            private readonly TestDirectory _directory;

            internal SignerOptions Options { get; }
            internal Signer Signer { get; }
            internal TestLogger Logger { get; }

            private SignTest(Signer signer,
                TestDirectory directory,
                SignerOptions options,
                TestLogger logger)
            {
                Signer = signer;
                Options = options;
                Logger = logger;
                _directory = directory;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    Options?.Dispose();
                    _directory?.Dispose();

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
                var directory = TestDirectory.Create();
                var signedPackagePath = Path.Combine(directory, Guid.NewGuid().ToString());
                var outputPackagePath = Path.Combine(directory, Guid.NewGuid().ToString());

                if (package != null)
                {
                    using (Stream fileStream = File.OpenWrite(signedPackagePath))
                    {
                        fileStream.Write(package, 0, package.Length);
                    }
                }

                signatureProvider = signatureProvider ?? Mock.Of<ISignatureProvider>();
                var logger = new TestLogger();
                var request = new AuthorSignPackageRequest(certificate, hashAlgorithm);
                var signerOptions = new SignerOptions(signedPackagePath, outputPackagePath, false, signatureProvider, request, logger);
                var signer = new Signer(signerOptions);

                return new SignTest(
                    signer,
                    directory,
                    signerOptions,
                    logger);
            }

            internal Task<bool> IsSignedAsync()
            {
                using (var unused = new MemoryStream())
                using (var outputStream = File.OpenRead(Options.OutputFilePath))
                using (var signedPackage = new SignedPackageArchive(outputStream, unused))
                {
                    return signedPackage.IsSignedAsync(CancellationToken.None);
                }
            }
        }
    }
}
#endif