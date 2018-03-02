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
using Test.Utility.Signing;
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

                var isSigned = await SignedArchiveTestUtility.IsSignedAsync(test.Options.OutputPackageStream);

                Assert.True(isSigned);
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

                var isSigned = await SignedArchiveTestUtility.IsSignedAsync(test.Options.InputPackageStream);
                Assert.False(isSigned);

                Assert.False(test.OutputFile.Exists);
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

                var isSigned = await SignedArchiveTestUtility.IsSignedAsync(test.Options.InputPackageStream);
                Assert.False(isSigned);

                Assert.False(test.OutputFile.Exists);
            }
        }

        private sealed class Test : IDisposable
        {
            private readonly X509Certificate2 _certificate;
            private readonly TestDirectory _directory;

            internal SigningOptions Options { get; }
            internal SignPackageRequest Request { get; }
            internal FileInfo OutputFile { get; }

            private bool _isDisposed;

            internal Test(X509Certificate2 certificate)
            {
                _directory = TestDirectory.Create();
                _certificate = new X509Certificate2(certificate);

                var outputPath = Path.Combine(_directory, Guid.NewGuid().ToString());

                OutputFile = new FileInfo(outputPath);

                var packageContext = new SimpleTestPackageContext();
                var packageFileName = Guid.NewGuid().ToString();
                var package = packageContext.CreateAsFile(_directory, packageFileName);
                var signatureProvider = new X509SignatureProvider(timestampProvider: null);

                Request = new AuthorSignPackageRequest(_certificate, HashAlgorithmName.SHA256);

                var overwrite = true;

                Options = SigningOptions.CreateFromFilePaths(
                    package.FullName,
                    OutputFile.FullName,
                    overwrite,
                    signatureProvider,
                    NullLogger.Instance);
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    Request?.Dispose();
                    _certificate?.Dispose();
                    Options.Dispose();
                    _directory?.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }
        }
    }
}
#endif