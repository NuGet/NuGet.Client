// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection("Signing Functional Test Collection")]
    public class SignerTests
    {
        private SigningTestFixture _testFixture;
        private IList<ISignatureVerificationProvider> _trustProviders;
        private SigningSpecifications _signingSpecifications;

        public SignerTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustProviders = _testFixture.TrustProviders;
            _signingSpecifications = _testFixture.SigningSpecifications;
        }

        [CIOnlyFact]
        public async Task SignAsync_AddsPackageSignature()
        {
            using (var test = new Test(_testFixture.TrustedTestCertificate.Source.Cert))
            {
                await test.Signer.SignAsync(test.Request, NullLogger.Instance, CancellationToken.None);

                var isSigned = await IsSignedAsync(test.WriteStream);

                Assert.True(isSigned);
            }
        }

        [CIOnlyFact]
        public async Task RemoveSignaturesAsync_RemovesPackageSignature()
        {
            using (var signTest = new Test(_testFixture.TrustedTestCertificate.Source.Cert))
            {
                await signTest.Signer.SignAsync(signTest.Request, NullLogger.Instance, CancellationToken.None);

                var isSigned = await IsSignedAsync(signTest.WriteStream);

                Assert.True(isSigned);

                using (var unsignTest = new Test(signTest.WriteStream))
                {
                    await unsignTest.Signer.RemoveSignaturesAsync(NullLogger.Instance, CancellationToken.None);

                    isSigned = await IsSignedAsync(unsignTest.WriteStream);

                    Assert.False(isSigned);
                }
            }
        }

        [CIOnlyFact]
        public async Task SignAsync_WithExpiredCertificate_Throws()
        {
            using (var test = new Test(_testFixture.TrustedTestCertificateExpired.Source.Cert))
            {
                var exception = await Assert.ThrowsAsync<SignatureException>(
                    () => test.Signer.SignAsync(test.Request, NullLogger.Instance, CancellationToken.None));

                Assert.Equal(NuGetLogCode.NU3018, exception.Code);
                Assert.Contains("Certificate chain validation failed.", exception.Message);

                var isSigned = await IsSignedAsync(test.WriteStream);

                Assert.False(isSigned);
            }
        }

        [CIOnlyFact]
        public async Task SignAsync_WithNotYetValidCertificate_Throws()
        {
            using (var test = new Test(_testFixture.TrustedTestCertificateNotYetValid.Source.Cert))
            {
                var exception = await Assert.ThrowsAsync<SignatureException>(
                    () => test.Signer.SignAsync(test.Request, NullLogger.Instance, CancellationToken.None));

                Assert.Equal(NuGetLogCode.NU3017, exception.Code);
                Assert.Contains("The signing certificate is not yet valid", exception.Message);

                var isSigned = await IsSignedAsync(test.WriteStream);

                Assert.False(isSigned);
            }
        }

        private static async Task<bool> IsSignedAsync(MemoryStream package)
        {
            var currentPosition = package.Position;

            var reader = new PackageArchiveReader(package, leaveStreamOpen: true);

            var isSigned = await reader.IsSignedAsync(CancellationToken.None);

            package.Seek(offset: currentPosition, loc: SeekOrigin.Begin);

            return isSigned;
        }

        private sealed class Test : IDisposable
        {
            private readonly X509Certificate2 _certificate;
            private readonly TestDirectory _directory;

            internal SignedPackageArchive Package { get; }
            internal MemoryStream ReadStream { get; }
            internal SignPackageRequest Request { get; }
            internal Signer Signer { get; }
            internal MemoryStream WriteStream { get; }

            private bool _isDisposed;

            internal Test(X509Certificate2 certificate)
            {
                _directory = TestDirectory.Create();
                _certificate = new X509Certificate2(certificate);

                var packageContext = new SimpleTestPackageContext();

                ReadStream = packageContext.CreateAsStream();
                WriteStream = packageContext.CreateAsStream();

                Package = new SignedPackageArchive(ReadStream, WriteStream);
                Request = new SignPackageRequest(_certificate, HashAlgorithmName.SHA256);

                var signatureProvider = new X509SignatureProvider(timestampProvider: null);

                Signer = new Signer(Package, signatureProvider);
            }

            internal Test(MemoryStream stream)
            {
                ReadStream = stream;
                WriteStream = stream;

                Package = new SignedPackageArchive(ReadStream, WriteStream);

                var signatureProvider = new X509SignatureProvider(timestampProvider: null);

                Signer = new Signer(Package, signatureProvider);
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _certificate?.Dispose();
                    _directory?.Dispose();
                    ReadStream.Dispose();
                    WriteStream.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }
        }
    }
}
#endif