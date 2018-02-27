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
    [Collection(SigningTestCollection.Name)]
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
                await test.Signer.SignAsync(CancellationToken.None);

                var isSigned = await IsSignedAsync(test.Options.OutputFilePath);

                Assert.True(isSigned);
            }
        }

        [CIOnlyFact]
        public async Task RemoveSignaturesAsync_RemovesPackageSignature()
        {
            using (var signTest = new Test(_testFixture.TrustedTestCertificate.Source.Cert))
            {
                await signTest.Signer.SignAsync(CancellationToken.None);

                var isSigned = await IsSignedAsync(signTest.Options.OutputFilePath);

                Assert.True(isSigned);

                using (var unsignTest = new Test(signTest.Options.OutputFilePath))
                {
                    await unsignTest.Signer.RemoveSignatureAsync(CancellationToken.None);

                    isSigned = await IsSignedAsync(unsignTest.Options.OutputFilePath);

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
                    () => test.Signer.SignAsync(CancellationToken.None));

                Assert.Equal(NuGetLogCode.NU3018, exception.Code);
                Assert.Contains("Certificate chain validation failed.", exception.Message);

                var isSigned = await IsSignedAsync(test.Options.PackageFilePath);
                Assert.False(isSigned);

                Assert.False(File.Exists(test.Options.OutputFilePath));
            }
        }

        [CIOnlyFact]
        public async Task SignAsync_WithNotYetValidCertificate_Throws()
        {
            using (var test = new Test(_testFixture.TrustedTestCertificateNotYetValid.Source.Cert))
            {
                var exception = await Assert.ThrowsAsync<SignatureException>(
                    () => test.Signer.SignAsync(CancellationToken.None));

                Assert.Equal(NuGetLogCode.NU3017, exception.Code);
                Assert.Contains("The signing certificate is not yet valid", exception.Message);

                var isSigned = await IsSignedAsync(test.Options.PackageFilePath);
                Assert.False(isSigned);

                Assert.False(File.Exists(test.Options.OutputFilePath));

            }
        }

        private static async Task<bool> IsSignedAsync(string packagePath)
        {
            using (var package = File.OpenRead(packagePath))
            {
                var reader = new PackageArchiveReader(package, leaveStreamOpen: true);

                var isSigned = await reader.IsSignedAsync(CancellationToken.None);

                return isSigned;
            }
        }

        private sealed class Test : IDisposable
        {
            private readonly X509Certificate2 _certificate;
            private readonly TestDirectory _directory;

            internal SignerOptions Options { get; }
            internal Signer Signer { get; }

            private bool _isDisposed;

            internal Test(X509Certificate2 certificate)
            {
                _directory = TestDirectory.Create();
                _certificate = new X509Certificate2(certificate);

                var packageContext = new SimpleTestPackageContext();
                var packageFileName = Guid.NewGuid().ToString();
                var package = packageContext.CreateAsFile(_directory, packageFileName);

                var outputPath = Path.Combine(_directory, Guid.NewGuid().ToString());

                var signatureProvider = new X509SignatureProvider(timestampProvider: null);

                var request = new AuthorSignPackageRequest(_certificate, HashAlgorithmName.SHA256);
                Options = new SignerOptions(packagePath: package.FullName, outputPath: outputPath, overwrite: true, signatureProvider: signatureProvider, signRequest: request, logger: NullLogger.Instance);

                Signer = new Signer(Options);
            }

            internal Test(string packagePath)
            {
                _directory = TestDirectory.Create();
                _certificate = new X509Certificate2();

                var outputPath = Path.Combine(_directory, Guid.NewGuid().ToString());

                var signatureProvider = new X509SignatureProvider(timestampProvider: null);

                var request = new AuthorSignPackageRequest(_certificate, HashAlgorithmName.SHA256);
                Options = new SignerOptions(packagePath: packagePath, outputPath: outputPath, overwrite: true, signatureProvider: signatureProvider, signRequest: request, logger: NullLogger.Instance);

                Signer = new Signer(Options);
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    Options?.Dispose();
                    _certificate?.Dispose();
                    _directory?.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }
        }
    }
}
#endif