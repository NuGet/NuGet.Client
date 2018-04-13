// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.CommandLine.FuncTest.Commands
{
    /// <summary>
    /// Tests Sign command
    /// These tests require admin privilege as the certs need to be added to the root store location
    /// </summary>
    [Collection(SignCommandTestCollection.Name)]
    public class VerifyCommandTests
    {
        private readonly string _noTimestamperWarningCode = NuGetLogCode.NU3027.ToString();
        private readonly string _primarySignatureInvalidErrorCode = NuGetLogCode.NU3018.ToString();
        private readonly string _signingDefaultErrorCode = NuGetLogCode.NU3000.ToString();
        private readonly string _noMatchingCertErrorCode = NuGetLogCode.NU3034.ToString();

        private SignCommandTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private string _nugetExePath;

        public VerifyCommandTests(SignCommandTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _nugetExePath = _testFixture.NuGetExePath;
        }

        [CIOnlyFact]
        public async Task VerifyCommand_VerifySignedPackageSucceedsAsync()
        {
            // Arrange
            var package = new SimpleTestPackageContext();
            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                var signResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation}",
                    waitForExit: true);

                signResult.Success.Should().BeTrue();

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public async Task VerifyCommand_VerifySignedAndTimestampedPackageSucceedsAsync()
        {
            // Arrange
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                var signResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -Timestamper {timestampService.Url.OriginalString} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation}",
                    waitForExit: true);

                signResult.Success.Should().BeTrue();

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().NotContain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public async Task VerifyCommand_VerifyResignedPackageSucceedsAsync()
        {
            // Arrange
            var package = new SimpleTestPackageContext();
            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation}",
                    waitForExit: true);

                var secondResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation} -Overwrite",
                    waitForExit: true);

                firstResult.Success.Should().BeTrue();
                secondResult.Success.Should().BeTrue();

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public async Task VerifyCommand_VerifyOnPackageSignedWithValidCertificateChainSucceedsAsync()
        {
            // Arrange
            var cert = _testFixture.TrustedTestCertificateChain.Leaf;
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                var signResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {cert.Source.Cert.Thumbprint} -CertificateStoreName {cert.StoreName} -CertificateStoreLocation {cert.StoreLocation}",
                    waitForExit: true);

                signResult.Success.Should().BeTrue();

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public async Task VerifyCommand_VerifyOnPackageSignedWithAllowedCertificateSucceedsAsync()
        {
            // Arrange
            var cert = _testFixture.TrustedTestCertificateChain.Leaf;
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                var signResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {cert.Source.Cert.Thumbprint} -CertificateStoreName {cert.StoreName} -CertificateStoreLocation {cert.StoreLocation}",
                    waitForExit: true);

                signResult.Success.Should().BeTrue();

                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(cert.Source.Cert, HashAlgorithmName.SHA256);

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures -CertificateFingerprint {certificateFingerprintString};abc;def",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public async Task VerifyCommand_VerifyOnPackageSignedWithoutAllowedCertificateFailsAsync()
        {
            // Arrange
            var cert = _testFixture.TrustedTestCertificateChain.Leaf;
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                var signResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {cert.Source.Cert.Thumbprint} -CertificateStoreName {cert.StoreName} -CertificateStoreLocation {cert.StoreLocation}",
                    waitForExit: true);

                signResult.Success.Should().BeTrue();

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures -CertificateFingerprint abc;def",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeFalse();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_noMatchingCertErrorCode);
            }
        }
    }
}