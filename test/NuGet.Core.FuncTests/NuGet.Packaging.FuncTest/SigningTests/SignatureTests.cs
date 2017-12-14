// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection("Signing Funtional Test Collection")]
    public class SignatureTests
    {
        private SigningTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private IList<ISignatureVerificationProvider> _trustProviders;
        private SigningSpecifications _signingSpecifications;

        public SignatureTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _trustProviders = _testFixture.TrustProviders;
            _signingSpecifications = _testFixture.SigningSpecifications;
        }

        [CIOnlyFact]
        public async Task Signature_HasTimestamp()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();

            using (var dir = TestDirectory.Create())
            {
                // Act
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedAndTimeStampedPackageAsync(_trustedTestCert, nupkg, dir);

                // Assert
                using (var stream = File.OpenRead(signedPackagePath))
                using (var reader = new PackageArchiveReader(stream))
                {
                    var signatures = await reader.GetSignaturesAsync(CancellationToken.None);

                    signatures.Count.Should().Be(1);

                    var signature = signatures[0];
                    signature.Timestamps.Should().NotBeEmpty();
                }
            }
        }

        [CIOnlyFact]
        public async Task Signature_HasNoTimestamp()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                // Act
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, nupkg, dir);

                // Assert
                using (var stream = File.OpenRead(signedPackagePath))
                using (var reader = new PackageArchiveReader(stream))
                {
                    var signatures = await reader.GetSignaturesAsync(CancellationToken.None);

                    signatures.Count.Should().Be(1);

                    var signature = signatures[0];
                    signature.Timestamps.Should().BeEmpty();
                }
            }
        }
    }
}

#endif