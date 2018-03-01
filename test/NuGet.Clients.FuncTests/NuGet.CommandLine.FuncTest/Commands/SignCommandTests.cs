// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.CommandLine.FuncTest.Commands
{
    /// <summary>
    /// Tests Sign command
    /// These tests require admin privilege as the certs need to be added to the root store location
    /// </summary>
    [Collection("Sign Command Test Collection")]
    public class SignCommandTests
    {
        private const string _packageAlreadySignedError = "NU3001: The package already contains a signature. Please remove the existing signature before adding a new signature.";
        private readonly string _invalidPasswordErrorCode = NuGetLogCode.NU3001.ToString();
        private readonly string _chainBuildFailureErrorCode = NuGetLogCode.NU3018.ToString();
        private readonly string _noCertFoundErrorCode = NuGetLogCode.NU3001.ToString();
        private readonly string _noTimestamperWarningCode = NuGetLogCode.NU3002.ToString();

        private SignCommandTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private string _nugetExePath;

        public SignCommandTests(SignCommandTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _nugetExePath = _testFixture.NuGetExePath;
        }

        [CIOnlyFact]
        public void SignCommand_SignPackage()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation}",
                    waitForExit: true);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithInvalidEkuFails()
        {
            // Arrange
            var invalidEkuCert = _testFixture.TrustedTestCertificateWithInvalidEku;

            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {invalidEkuCert.Source.Cert.Thumbprint} -CertificateStoreName {invalidEkuCert.StoreName} -CertificateStoreLocation {invalidEkuCert.StoreLocation}",
                    waitForExit: true);

                // Assert
                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithExpiredCertificateFails()
        {
            // Arrange
            var expiredCert = _testFixture.TrustedTestCertificateExpired;

            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {expiredCert.Source.Cert.Thumbprint} -CertificateStoreName {expiredCert.StoreName} -CertificateStoreLocation {expiredCert.StoreLocation}",
                    waitForExit: true);

                // Assert
                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_noCertFoundErrorCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithNotYetValidCertificateFails()
        {
            // Arrange
            var cert = _testFixture.TrustedTestCertificateNotYetValid;

            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {cert.Source.Cert.Thumbprint} -CertificateStoreName {cert.StoreName} -CertificateStoreLocation {cert.StoreLocation}",
                    waitForExit: true);

                // Assert
                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_noCertFoundErrorCode);
            }
        }

        [CIOnlyFact]
        public async Task SignCommand_SignPackageWithTimestamping()
        {
            // Arrange
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint}  -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation} -Timestamper {timestampService.Url.OriginalString}",
                    waitForExit: true);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().NotContain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithValidCertChain()
        {
            // Arrange
            var cert = _testFixture.TrustedTestCertificateChain.Leaf;

            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {cert.Source.Cert.Thumbprint}  -CertificateStoreName {cert.StoreName} -CertificateStoreLocation {cert.StoreLocation}",
                    waitForExit: true);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithRevokedLeafCertChain()
        {
            // Arrange
            var cert = _testFixture.RevokedTestCertificateWithChain;

            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {cert.Source.Cert.Thumbprint}  -CertificateStoreName {cert.StoreName} -CertificateStoreLocation {cert.StoreLocation}",
                    waitForExit: true);

                // Assert
                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithUnknownRevocationCertChain()
        {
            // Arrange
            var cert = _testFixture.RevocationUnknownTestCertificateWithChain;

            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {cert.Source.Cert.Thumbprint} -CertificateStoreName {cert.StoreName} -CertificateStoreLocation {cert.StoreLocation}",
                    waitForExit: true);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_chainBuildFailureErrorCode);
                result.AllOutput.Should().Contain("The revocation function was unable to check revocation for the certificate");
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithOutputDirectory()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var outputDir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packageFileName = Guid.NewGuid().ToString();
                var packagePath = Path.Combine(dir, packageFileName);
                var signedPackagePath = Path.Combine(outputDir, packageFileName);

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint}  -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation} -OutputDirectory {outputDir}",
                    waitForExit: true);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                File.Exists(signedPackagePath).Should().BeTrue();
            }
        }

        [CIOnlyFact]
        public void SignCommand_ResignPackageWithoutOverwriteFails()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
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
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint}  -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation}",
                    waitForExit: true);

                // Act
                var secondResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint}  -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation}",
                    waitForExit: true);

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                secondResult.Success.Should().BeFalse();
                secondResult.Errors.Should().Contain(_packageAlreadySignedError);
            }
        }

        [CIOnlyFact]
        public void SignCommand_ResignPackageWithOverwriteSuccess()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
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

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                secondResult.Success.Should().BeTrue();
                secondResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithOverwriteSuccess()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation} -Overwrite",
                    waitForExit: true);

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithPfxFileSuccess()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                var pfxPath = Path.Combine(dir, Guid.NewGuid().ToString());

                var password = Guid.NewGuid().ToString();
                var pfxBytes = _trustedTestCert.Source.Cert.Export(X509ContentType.Pfx, password);

                using (var fileStream = File.OpenWrite(pfxPath))
                using (var pfxStream = new MemoryStream(pfxBytes))
                {
                    pfxStream.CopyTo(fileStream);
                }

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificatePath {pfxPath} -CertificatePassword {password}",
                    waitForExit: true);

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithPfxFileInteractiveSuccess()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                var pfxPath = Path.Combine(dir, Guid.NewGuid().ToString());

                var password = Guid.NewGuid().ToString();
                var pfxBytes = _trustedTestCert.Source.Cert.Export(X509ContentType.Pfx, password);

                using (var fileStream = File.OpenWrite(pfxPath))
                using (var pfxStream = new MemoryStream(pfxBytes))
                {
                    pfxStream.CopyTo(fileStream);
                }

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificatePath {pfxPath}",
                    waitForExit: true,
                    inputAction: (w) =>
                    {
                        w.WriteLine(password);
                    });

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithPfxFileInteractiveInvalidPasswordFails()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                var pfxPath = Path.Combine(dir, Guid.NewGuid().ToString());

                var password = Guid.NewGuid().ToString();
                var pfxBytes = _trustedTestCert.Source.Cert.Export(X509ContentType.Pfx, password);

                using (var fileStream = File.OpenWrite(pfxPath))
                using (var pfxStream = new MemoryStream(pfxBytes))
                {
                    pfxStream.CopyTo(fileStream);
                }

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificatePath {pfxPath}",
                    waitForExit: true,
                    inputAction: (w) =>
                    {
                        w.WriteLine(Guid.NewGuid().ToString());
                    });

                // Assert
                firstResult.Success.Should().BeFalse();
                firstResult.AllOutput.Should().Contain(string.Format(_invalidPasswordErrorCode, pfxPath));
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithPfxFileWithoutPasswordAndWithNonInteractiveFails()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                var pfxPath = Path.Combine(dir, Guid.NewGuid().ToString());

                var password = Guid.NewGuid().ToString();
                var pfxBytes = _trustedTestCert.Source.Cert.Export(X509ContentType.Pfx, password);

                using (var fileStream = File.OpenWrite(pfxPath))
                using (var pfxStream = new MemoryStream(pfxBytes))
                {
                    pfxStream.CopyTo(fileStream);
                }

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificatePath {pfxPath} -NonInteractive",
                    waitForExit: true);

                // Assert
                firstResult.Success.Should().BeFalse();
                firstResult.AllOutput.Should().Contain(string.Format(_invalidPasswordErrorCode, pfxPath));
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithPfxFileWithNonInteractiveAndStdInPasswordFails()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                var pfxPath = Path.Combine(dir, Guid.NewGuid().ToString());

                var password = Guid.NewGuid().ToString();
                var pfxBytes = _trustedTestCert.Source.Cert.Export(X509ContentType.Pfx, password);

                using (var fileStream = File.OpenWrite(pfxPath))
                using (var pfxStream = new MemoryStream(pfxBytes))
                {
                    pfxStream.CopyTo(fileStream);
                }

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificatePath {pfxPath} -NonInteractive",
                    waitForExit: true,
                    inputAction: (w) =>
                    {
                        w.WriteLine(Guid.NewGuid().ToString());
                    });

                // Assert
                firstResult.Success.Should().BeFalse();
                firstResult.AllOutput.Should().Contain(string.Format(_invalidPasswordErrorCode, pfxPath));
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithUntrustedSelfIssuedCertificateInCertificateStore()
        {
            using (var directory = TestDirectory.Create())
            {
                var packageContext = new SimpleTestPackageContext();
                var packageFile = packageContext.CreateAsFile(directory, fileName: Guid.NewGuid().ToString());

                using (var certificate = _testFixture.UntrustedSelfIssuedCertificateInCertificateStore)
                {
                    var result = CommandRunner.Run(
                        _nugetExePath,
                        directory,
                        $"sign {packageFile.FullName} -CertificateFingerprint {certificate.Thumbprint}",
                        waitForExit: true);

                    Assert.True(result.Success);
                    Assert.Contains(_noTimestamperWarningCode, result.AllOutput);
                }
            }
        }
    }
}