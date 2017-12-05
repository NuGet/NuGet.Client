// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
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
        private const string _packageAlreadySignedError = "Error NU5000: The package already contains a signature. Please remove the existing signature before adding a new signature.";
        private const string _invalidPasswordError = @"Invalid password was provided for the certificate file '{0}'. Please provide a valid password using the '-CertificatePassword' option";
        private const string _invalidEkuError = "The following certificate cannot be used for signing a package as it does not have Code Signing enhanced key usage";
        private const string _noTimestamperWarningCode = "NU3521";

        private SignCommandTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private TrustedTestCert<TestCertificate> _trustedTestCertWithInvalidEku;
        private string _nugetExePath;
        private string _timestamper;

        public SignCommandTests(SignCommandTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _trustedTestCertWithInvalidEku = _testFixture.TrustedTestCertificateWithInvalidEku;
            _nugetExePath = _testFixture.NuGetExePath;
            _timestamper = _testFixture.Timestamper;
        }

        [CIOnlyFact]
        public void SignCommand_SignPackage()
        {
            // Arrange
            var testLogger = new TestLogger();

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
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithInvalidEkuFails()
        {
            // Arrange
            var testLogger = new TestLogger();

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
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCertWithInvalidEku.Source.Cert.Thumbprint} -CertificateStoreName {_trustedTestCertWithInvalidEku.StoreName} -CertificateStoreLocation {_trustedTestCertWithInvalidEku.StoreLocation}",
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                // Assert
                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_invalidEkuError);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithTimestamping()
        {
            // Arrange
            var testLogger = new TestLogger();

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
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint}  -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation} -Timestamper {_timestamper}",
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().NotContain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithOutputDirectory()
        {
            // Arrange
            var testLogger = new TestLogger();

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
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

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
            var testLogger = new TestLogger();

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
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                // Act
                var secondResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint}  -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation}",
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

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
            var testLogger = new TestLogger();

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
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                var secondResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation} -Overwrite",
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                secondResult.Success.Should().BeTrue();
                secondResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithPfxFileSuccess()
        {
            // Arrange
            var testLogger = new TestLogger();

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
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }


        [CIOnlyFact]
        public void SignCommand_SignPackageWithPfxFileInteractiveSuccess()
        {
            // Arrange
            var testLogger = new TestLogger();

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
                    },
                    timeOutInMilliseconds: 10000);

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithPfxFileInteractiveInvalidPasswordFails()
        {
            // Arrange
            var testLogger = new TestLogger();

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
                    },
                    timeOutInMilliseconds: 10000);

                // Assert
                firstResult.Success.Should().BeFalse();
                firstResult.AllOutput.Should().Contain(string.Format(_invalidPasswordError, pfxPath));
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithPfxFileWithoutPasswordAndWithNonInteractiveFails()
        {
            // Arrange
            var testLogger = new TestLogger();

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
                    timeOutInMilliseconds: 10000);

                // Assert
                firstResult.Success.Should().BeFalse();
                firstResult.AllOutput.Should().Contain(string.Format(_invalidPasswordError, pfxPath));
            }
        }

        [CIOnlyFact]
        public void SignCommand_SignPackageWithPfxFileWithNonInteractiveAndStdInPasswordFails()
        {
            // Arrange
            var testLogger = new TestLogger();

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
                    },
                    timeOutInMilliseconds: 10000);

                // Assert
                firstResult.Success.Should().BeFalse();
                firstResult.AllOutput.Should().Contain(string.Format(_invalidPasswordError, pfxPath));
            }
        }
    }
}