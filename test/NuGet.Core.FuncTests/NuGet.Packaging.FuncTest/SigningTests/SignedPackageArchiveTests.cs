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
    public class SignedPackageArchiveTests
    {
        private readonly SigningTestFixture _fixture;

        public SignedPackageArchiveTests(SigningTestFixture fixture)
        {
             _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [CIOnlyFact]
        public async Task RemoveSignatureAsync_RemovesPackageSignatureAsync()
        {
            using (var directory = TestDirectory.Create())
            using (var certificate = new X509Certificate2(_fixture.TrustedTestCertificate.Source.Cert))
            {
                var signedPackageFile = await CreateSignedPackageAsync(directory, certificate);

                using (var test = new Test(signedPackageFile.FullName))
                {
                    using (var package = new SignedPackageArchive(test.InputPackageStream, test.OutputPackageStream))
                    {
                        await package.RemoveSignatureAsync(CancellationToken.None);
                    }

                    var isSigned = await SignedArchiveTestUtility.IsSignedAsync(test.OutputPackageStream);

                    Assert.False(isSigned);
                }
            }
        }

        private static async Task<FileInfo> CreateSignedPackageAsync(TestDirectory directory, X509Certificate2 certificate)
        {
            var packageContext = new SimpleTestPackageContext();
            var packageFileName = Guid.NewGuid().ToString();
            var package = packageContext.CreateAsFile(directory, packageFileName);
            var signatureProvider = new X509SignatureProvider(timestampProvider: null);
            var overwrite = true;
            var outputFile = new FileInfo(Path.Combine(directory, Guid.NewGuid().ToString()));

            using (var request = new AuthorSignPackageRequest(certificate, HashAlgorithmName.SHA256))
            using (var options = SigningOptions.CreateFromFilePaths(
                package.FullName,
                outputFile.FullName,
                overwrite,
                signatureProvider,
                NullLogger.Instance))
            {
                await SigningUtility.SignAsync(options, request, CancellationToken.None);

                var isSigned = await SignedArchiveTestUtility.IsSignedAsync(options.OutputPackageStream);

                Assert.True(isSigned);
            }

            return outputFile;
        }

        private sealed class Test : IDisposable
        {
            private readonly TestDirectory _directory;

            internal Stream InputPackageStream { get; }
            internal Stream OutputPackageStream { get; }

            private bool _isDisposed;

            internal Test(string packageFilePath)
            {
                _directory = TestDirectory.Create();

                var outputPath = Path.Combine(_directory, Guid.NewGuid().ToString());

                InputPackageStream = File.OpenRead(packageFilePath);
                OutputPackageStream = File.Open(outputPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _directory?.Dispose();

                    InputPackageStream.Dispose();
                    OutputPackageStream.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }
        }
    }
}
#endif