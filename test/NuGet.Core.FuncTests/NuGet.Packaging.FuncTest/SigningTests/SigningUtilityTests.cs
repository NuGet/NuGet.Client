// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
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
    public class SigningUtilityTests
    {
        private readonly SigningTestFixture _testFixture;

        public SigningUtilityTests(SigningTestFixture fixture)
        {
             _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [CIOnlyFact]
        public void Verify_WithValidInput_DoesNotThrow()
        {
            using (var certificate = new X509Certificate2(_testFixture.TrustedTestCertificate.Source.PublicCert.RawData))
            using (var request = new AuthorSignPackageRequest(certificate, HashAlgorithmName.SHA256, HashAlgorithmName.SHA256))
            {
                SigningUtility.Verify(request, NullLogger.Instance);
            }
        }

        [CIOnlyFact]
        public async Task SignAsync_AddsPackageSignatureAsync()
        {
            using (var test = new Test(_testFixture.TrustedTestCertificate.Source.Cert))
            {
                await SigningUtility.SignAsync(test.Options, test.Request, CancellationToken.None);

                var isSigned = await IsSignedAsync(test.Options.OutputFilePath);

                Assert.True(isSigned);
            }
        }

        [CIOnlyFact]
        public async Task RemoveSignaturesAsync_RemovesPackageSignatureAsync()
        {
            using (var signTest = new Test(_testFixture.TrustedTestCertificate.Source.Cert))
            {
                await SigningUtility.SignAsync(signTest.Options, signTest.Request, CancellationToken.None);

                var isSigned = await IsSignedAsync(signTest.Options.OutputFilePath);

                Assert.True(isSigned);

                using (var unsignTest = new Test(signTest.Options.OutputFilePath))
                {
                    await SigningUtility.RemoveSignatureAsync(unsignTest.Options.PackageFilePath, unsignTest.Options.OutputFilePath, CancellationToken.None);

                    isSigned = await IsSignedAsync(unsignTest.Options.OutputFilePath);

                    Assert.False(isSigned);
                }
            }
        }

        [CIOnlyFact]
        public async Task SignAsync_WithExpiredCertificate_ThrowsAsync()
        {
            using (var test = new Test(_testFixture.TrustedTestCertificateExpired.Source.Cert))
            {
                var exception = await Assert.ThrowsAsync<SignatureException>(
                    () => SigningUtility.SignAsync(test.Options, test.Request, CancellationToken.None));

                Assert.Equal(NuGetLogCode.NU3018, exception.Code);
                Assert.Contains("Certificate chain validation failed.", exception.Message);

                var isSigned = await IsSignedAsync(test.Options.PackageFilePath);
                Assert.False(isSigned);

                Assert.False(File.Exists(test.Options.OutputFilePath));
            }
        }

        [CIOnlyFact]
        public async Task SignAsync_WithNotYetValidCertificate_ThrowsAsync()
        {
            using (var test = new Test(_testFixture.TrustedTestCertificateNotYetValid.Source.Cert))
            {
                var exception = await Assert.ThrowsAsync<SignatureException>(
                    () => SigningUtility.SignAsync(test.Options, test.Request, CancellationToken.None));

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
            using (var reader = new PackageArchiveReader(package, leaveStreamOpen: true))
            {
                var isSigned = await reader.IsSignedAsync(CancellationToken.None);

                return isSigned;
            }
        }

        private sealed class Test : IDisposable
        {
            private readonly X509Certificate2 _certificate;
            private readonly TestDirectory _directory;

            internal SigningOptions Options { get; }
            internal SignPackageRequest Request { get; }

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

                Request = new AuthorSignPackageRequest(_certificate, HashAlgorithmName.SHA256);
                Options = new SigningOptions(packageFilePath: package.FullName, outputFilePath: outputPath, overwrite: true, signatureProvider: signatureProvider, logger: NullLogger.Instance);
            }

            internal Test(string packageFilePath)
            {
                _directory = TestDirectory.Create();
                _certificate = new X509Certificate2();

                var outputPath = Path.Combine(_directory, Guid.NewGuid().ToString());

                var signatureProvider = new X509SignatureProvider(timestampProvider: null);

                Request = new AuthorSignPackageRequest(_certificate, HashAlgorithmName.SHA256);
                Options = new SigningOptions(packageFilePath: packageFilePath, outputFilePath: outputPath, overwrite: true, signatureProvider: signatureProvider, logger: NullLogger.Instance);
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    Request?.Dispose();
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