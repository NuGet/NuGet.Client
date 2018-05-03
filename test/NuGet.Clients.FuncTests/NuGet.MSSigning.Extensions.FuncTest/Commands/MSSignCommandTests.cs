// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.CommandLine;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.MSSigning.Extensions.FuncTest.Commands
{
    /// <summary>
    /// Tests Sign command
    /// These tests require admin privilege as the certs need to be added to the root store location
    /// </summary>
    [Collection(MSSignCommandTestCollection.Name)]
    public class MSSignCommandTests
    {
        private readonly string _noTimestamperWarningCode = NuGetLogCode.NU3002.ToString();

        private TrustedTestCert<TestCertificate> _trustedTestCertWithPrivateKey;
        private TrustedTestCert<TestCertificate> _trustedTestCertWithoutPrivateKey;

        private MSSignCommandTestFixture _testFixture;
        private string _nugetExePath;

        public MSSignCommandTests(MSSignCommandTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCertWithPrivateKey = _testFixture.TrustedTestCertificateWithPrivateKey;
            _trustedTestCertWithoutPrivateKey = _testFixture.TrustedTestCertificateWithoutPrivateKey;
            _nugetExePath = _testFixture.NuGetExePath;
        }

        [CIOnlyFact]
        public void GetAuthorSignRequest_InvalidCertificateFile()
        {
            var mockConsole = new Mock<IConsole>();
            var timestampUri = "http://timestamp.test/url";

            // Arrange
            using (var dir = TestDirectory.Create())
            using (var test = new MSSignCommandTestContext(_trustedTestCertWithPrivateKey.TrustedCert))
            {
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestampUri,
                    CertificateFile = Path.Combine(dir, "non-existant-cert.pfx"),
                    CSPName = test.CertificateCSPName,
                    KeyContainer = test.CertificateKeyContainer,
                    CertificateFingerprint = test.Cert.Thumbprint,
                };
                signCommand.Arguments.Add(Path.Combine(dir, "package.nupkg"));

                // Act & Assert
                var ex = Assert.Throws<CryptographicException>(() => signCommand.GetAuthorSignRequest());
                Assert.Contains("The system cannot find the file specified.", ex.Message);
            }
        }

        [CIOnlyFact]
        public void GetAuthorSignRequest_InvalidCSPName()
        {
            var mockConsole = new Mock<IConsole>();
            var timestampUri = "http://timestamp.test/url";

            // Arrange
            using (var dir = TestDirectory.Create())
            using (var test = new MSSignCommandTestContext(_trustedTestCertWithoutPrivateKey.TrustedCert, exportPfx: false))
            {
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestampUri,
                    CertificateFile = test.CertificatePath,
                    CSPName = "random nonexistant csp name",
                    KeyContainer = test.CertificateKeyContainer,
                    CertificateFingerprint = test.Cert.Thumbprint,
                };
                signCommand.Arguments.Add(Path.Combine(dir, "package.nupkg"));

                // Act & Assert
                var ex = Assert.Throws<InvalidOperationException>(() => signCommand.GetAuthorSignRequest());
                Assert.Equal("Can't find cng key.", ex.Message);
            }
        }

        [CIOnlyFact]
        public void GetAuthorSignRequest_InvalidKeyContainer()
        {
            var mockConsole = new Mock<IConsole>();
            var timestampUri = "http://timestamp.test/url";

            // Arrange
            using (var dir = TestDirectory.Create())
            using (var test = new MSSignCommandTestContext(_trustedTestCertWithoutPrivateKey.TrustedCert, exportPfx: false))
            {
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestampUri,
                    CertificateFile = test.CertificatePath,
                    CSPName = test.CertificateCSPName,
                    KeyContainer = "invalid-key-container",
                    CertificateFingerprint = test.Cert.Thumbprint,
                };
                signCommand.Arguments.Add(Path.Combine(dir, "package.nupkg"));

                // Act & Assert
                var ex = Assert.Throws<InvalidOperationException>(() => signCommand.GetAuthorSignRequest());
                Assert.Equal("Can't find cng key.", ex.Message);
            }
        }

        [CIOnlyFact]
        public void GetAuthorSignRequest_InvalidFingerprint()
        {
            var mockConsole = new Mock<IConsole>();
            var timestampUri = "http://timestamp.test/url";

            // Arrange
            using (var dir = TestDirectory.Create())
            using (var test = new MSSignCommandTestContext(_trustedTestCertWithPrivateKey.TrustedCert))
            {
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestampUri,
                    CertificateFile = test.CertificatePath,
                    CSPName = test.CertificateCSPName,
                    KeyContainer = test.CertificateKeyContainer,
                    CertificateFingerprint = "invalid-fingerprint",
                };
                signCommand.Arguments.Add(Path.Combine(dir, "package.nupkg"));

                // Act & Assert
                var ex = Assert.Throws<InvalidOperationException>(() => signCommand.GetAuthorSignRequest());
                Assert.Equal("Can't find specified certificate.", ex.Message);
            }
        }

        [CIOnlyFact]
        public void GetAuthorSignRequest_Success()
        {
            var mockConsole = new Mock<IConsole>();
            var timestampUri = "http://timestamp.test/url";

            // Arrange
            using (var dir = TestDirectory.Create())
            using (var test = new MSSignCommandTestContext(_trustedTestCertWithPrivateKey.TrustedCert))
            {
                var signCommand = new MSSignCommand
                {
                    Console = mockConsole.Object,
                    Timestamper = timestampUri,
                    CertificateFile = test.CertificatePath,
                    CSPName = test.CertificateCSPName,
                    KeyContainer = test.CertificateKeyContainer,
                    CertificateFingerprint = test.Cert.Thumbprint,
                };
                signCommand.Arguments.Add(Path.Combine(dir, "package.nupkg"));

                // Act
                var signRequest = signCommand.GetAuthorSignRequest();

                // Assert
                Assert.Equal(SignatureType.Author, signRequest.SignatureType);
                Assert.NotNull(signRequest.Certificate);
                Assert.Equal(signRequest.Certificate.Thumbprint, test.Cert.Thumbprint, StringComparer.Ordinal);
                Assert.NotNull(signRequest.PrivateKey);
            }
        }

        [CIOnlyFact]
        public async Task MSSignCommand_PrimarySignPackage_WithNoTimestampAsync()
        {
            var package = new SimpleTestPackageContext();

            // Arrange
            using (var test = new MSSignCommandTestContext(_trustedTestCertWithPrivateKey.TrustedCert))
            {
                var unsignedPackageFile = await package.CreateAsFileAsync(test.Directory, Guid.NewGuid().ToString());
                var command = $"mssign {unsignedPackageFile} -CertificateFile {test.CertificatePath} -CSPName \"{test.CertificateCSPName}\" -KeyContainer \"{test.CertificateKeyContainer}\" -CertificateFingerprint {test.Cert.Thumbprint}";

                var result = CommandRunner.Run(
                    _nugetExePath,
                    test.Directory,
                    command,
                    waitForExit: true);

                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public async Task MSSignCommand_PrimarySignPackage_WithTimestampAsync()
        {
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var package = new SimpleTestPackageContext();

            // Arrange
            using (var test = new MSSignCommandTestContext(_trustedTestCertWithPrivateKey.TrustedCert))
            {
                var unsignedPackageFile = await package.CreateAsFileAsync(test.Directory, Guid.NewGuid().ToString());
                var command = $"mssign {unsignedPackageFile} -Timestamper {timestampService.Url} -CertificateFile {test.CertificatePath} -CSPName \"{test.CertificateCSPName}\" -KeyContainer \"{test.CertificateKeyContainer}\" -CertificateFingerprint {test.Cert.Thumbprint}";

                var result = CommandRunner.Run(
                    _nugetExePath,
                    test.Directory,
                    command,
                    waitForExit: true);

                result.Success.Should().BeTrue();
                result.AllOutput.Should().NotContain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public async Task MSSignCommand_ResignPackageWithoutOverwriteFailsAsync()
        {
            var package = new SimpleTestPackageContext();

            // Arrange
            using (var test = new MSSignCommandTestContext(_trustedTestCertWithPrivateKey.TrustedCert))
            {
                var unsignedPackageFile = await package.CreateAsFileAsync(test.Directory, Guid.NewGuid().ToString());
                var command = $"mssign {unsignedPackageFile} -CertificateFile {test.CertificatePath} -CSPName \"{test.CertificateCSPName}\" -KeyContainer \"{test.CertificateKeyContainer}\" -CertificateFingerprint {test.Cert.Thumbprint}";

                var result = CommandRunner.Run(
                    _nugetExePath,
                    test.Directory,
                    command,
                    waitForExit: true);

                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);

                result = CommandRunner.Run(
                    _nugetExePath,
                    test.Directory,
                    command,
                    waitForExit: true);

                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.Errors.Should().Contain("NU3001: The package already contains a signature. Please remove the existing signature before adding a new signature.");
            }
        }

        [CIOnlyFact]
        public async Task MSSignCommand_ResignPackageWithOverwriteSuccessAsync()
        {
            var package = new SimpleTestPackageContext();

            // Arrange
            using (var test = new MSSignCommandTestContext(_trustedTestCertWithPrivateKey.TrustedCert))
            {
                var unsignedPackageFile = await package.CreateAsFileAsync(test.Directory, Guid.NewGuid().ToString());
                var command = $"mssign {unsignedPackageFile} -CertificateFile {test.CertificatePath} -CSPName \"{test.CertificateCSPName}\" -KeyContainer \"{test.CertificateKeyContainer}\" -CertificateFingerprint {test.Cert.Thumbprint}";
                var commandWithOverwrite = $"mssign {unsignedPackageFile} -CertificateFile {test.CertificatePath} -CSPName \"{test.CertificateCSPName}\" -KeyContainer \"{test.CertificateKeyContainer}\" -CertificateFingerprint {test.Cert.Thumbprint} -Overwrite";

                var result = CommandRunner.Run(
                    _nugetExePath,
                    test.Directory,
                    command,
                    waitForExit: true);

                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);

                result = CommandRunner.Run(
                    _nugetExePath,
                    test.Directory,
                    commandWithOverwrite,
                    waitForExit: true);

                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }
    }
}
