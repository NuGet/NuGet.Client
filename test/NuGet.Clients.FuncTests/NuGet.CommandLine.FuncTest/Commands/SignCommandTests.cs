// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.CommandLine.FuncTest.Commands
{
    [Collection("Sign Command Test Collection")]
    public class SignCommandTests
    {
        private const string _packageAlreadySignedError = "Error NU5000: The package already contains a signature. Please remove the existing signature before adding a new signature.";

        private SignCommandTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private string _nugetExePath;
        private string _timestamper;

        public SignCommandTests(SignCommandTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _nugetExePath = _testFixture.NuGetExePath;
            _timestamper = _testFixture.Timestamper;
        }

        [Fact]
        public void SignCommand_SignPackage()
        {
            // Arrange
            var testLogger = new TestLogger();

            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, new Guid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (Stream fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName TrustedPeople",
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain("NU3521");
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
                var packagePath = Path.Combine(dir, new Guid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (Stream fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName TrustedPeople -Timestamper {_timestamper}",
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().NotContain("NU3521");
            }
        }

        [Fact]
        public void SignCommand_SignPackageWithOutputDirectory()
        {
            // Arrange
            var testLogger = new TestLogger();

            using (var dir = TestDirectory.Create())
            using (var outputDir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, new Guid().ToString());
                var signedPackagePath = Path.Combine(dir, new Guid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (Stream fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var result = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName TrustedPeople -OutputDirectory {outputDir}",
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                // Assert
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain("NU3521");
                File.Exists(signedPackagePath).Should().BeTrue();
            }
        }

        [Fact]
        public void SignCommand_ResignPackageWithoutOverwriteFails()
        {
            // Arrange
            var testLogger = new TestLogger();

            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, new Guid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (Stream fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName TrustedPeople",
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                // Act
                var secondResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName TrustedPeople",
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain("NU3521");
                secondResult.Success.Should().BeFalse();
                secondResult.Errors.Should().Contain(_packageAlreadySignedError);
            }
        }

        [Fact]
        public void SignCommand_ResignPackageWithOverwriteFails()
        {
            // Arrange
            var testLogger = new TestLogger();

            using (var dir = TestDirectory.Create())
            using (var zipStream = new SimpleTestPackageContext().CreateAsStream())
            {
                var packagePath = Path.Combine(dir, new Guid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (Stream fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                // Act
                var firstResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName TrustedPeople",
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                var secondResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName TrustedPeople -Overwrite",
                    waitForExit: true,
                    timeOutInMilliseconds: 10000);

                // Assert
                firstResult.Success.Should().BeTrue();
                firstResult.AllOutput.Should().Contain("NU3521");
                secondResult.Success.Should().BeTrue();
                secondResult.AllOutput.Should().Contain("NU3521");
            }
        }
    }
}
