// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection("Signing Funtional Test Collection")]
    public class SignerTests
    {
        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private IList<ISignatureVerificationProvider> _trustProviders;
        private SigningSpecifications _signingSpecifications;

        public SignerTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _trustProviders = _testFixture.TrustProviders;
            _signingSpecifications = _testFixture.SigningSpecifications;
        }

        [CIOnlyFact]
        public async Task Signer_SignPackageAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();

            using (var dir = TestDirectory.Create())
            {
                // Act
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(_trustedTestCert, nupkg, dir);

                // Assert
                using (var stream = File.OpenRead(signedPackagePath))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    zip.GetEntry(_signingSpecifications.SignaturePath).Should().NotBeNull();
                }
            }
        }

        [CIOnlyFact]
        public async Task Signer_UnsignPackageAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();

            using (var dir = TestDirectory.Create())
            {
                // Act
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(_trustedTestCert, nupkg, dir);

                using (var stream = File.OpenRead(signedPackagePath))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    zip.GetEntry(_signingSpecifications.SignaturePath).Should().NotBeNull();
                }

                await SignedArchiveTestUtility.UnsignPackageAsync(signedPackagePath, dir);

                // Assert
                using (var stream = File.OpenRead(signedPackagePath))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    zip.GetEntry(_signingSpecifications.SignaturePath).Should().BeNull();
                }
            }
        }
    }
}

#endif