// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration.Test;
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

        [CIOnlyFact]
        public async Task VerifyCommand_PackageSignedWithAllowedCertificate_AllowUntrustedRootIsSetFalse_Succeeds()
        {
            // Arrange
            TrustedTestCert<TestCertificate> cert = _testFixture.TrustedTestCertificateChain.Leaf;
            var package = new SimpleTestPackageContext();

            using (var pathContext = new SimpleTestPathContext())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(pathContext.PackageSource, "testpackage.nupkg");

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                var signResult = CommandRunner.Run(
                    _nugetExePath,
                    pathContext.PackageSource,
                    $"sign {packagePath} -CertificateFingerprint {cert.Source.Cert.Thumbprint} -CertificateStoreName {cert.StoreName} -CertificateStoreLocation {cert.StoreLocation}",
                    waitForExit: true);

                signResult.Success.Should().BeTrue();

                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(cert.Source.Cert, HashAlgorithmName.SHA256);

                var trustedSignersSectionContent = $@"
    <trustedSigners>
        <author name=""signed"">
            <certificate fingerprint=""{certificateFingerprintString}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </author>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(pathContext.WorkingDirectory, trustedSignersSectionContent, "configuration");

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    pathContext.PackageSource,
                    $"verify {packagePath} -Signatures -CertificateFingerprint {certificateFingerprintString};abc;def",
                    waitForExit: true);

                // Assert
                // Succeeds even allowUntrustedRoot is set false in nuget.config since actual signing certificate has trusted root
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task VerifyCommand_PackageSignedWithUntrustedCertificate_AllowUntrustedRootIsSetFalse_Fails(bool withTrustedSignersSection)
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext("A", "1.0.0");

            using (var pathContext = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_testFixture.UntrustedSelfIssuedCertificateInCertificateStore))
            {
                var packagePath = Path.Combine(pathContext.WorkingDirectory, nupkg.PackageName);
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, pathContext.WorkingDirectory);
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);

                if (withTrustedSignersSection)
                {
                    var trustedSignersSectionContent = $@"
    <trustedSigners>
        <author name=""MyCert"">
            <certificate fingerprint=""{certificateFingerprintString}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""false"" />
        </author>
    </trustedSigners>
";
                    SimpleTestSettingsContext.AddSectionIntoNuGetConfig(pathContext.WorkingDirectory, trustedSignersSectionContent, "configuration");
                }

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    pathContext.PackageSource,
                    $"verify {packagePath} -Signatures -CertificateFingerprint {certificateFingerprintString};abc;def",
                    waitForExit: true);

                // Assert
                // Unless allowUntrustedRoot is set true in nuget.config verify always fails for cert without trusted root.
                verifyResult.Success.Should().BeFalse();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_primarySignatureInvalidErrorCode);
            }
        }

        [CIOnlyFact]
        public async Task VerifyCommand_PackageSignedWithUntrustedCertificate_AllowUntrustedRootIsSetTrue_Succeeds()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext("A", "1.0.0");

            using (var pathContext = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_testFixture.UntrustedSelfIssuedCertificateInCertificateStore))
            {
                var packagePath = Path.Combine(pathContext.WorkingDirectory, nupkg.PackageName);

                //Act
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, pathContext.WorkingDirectory);

                // Arrange
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);

                var trustedSignersSectionContent = $@"
    <trustedSigners>
        <author name=""MyCert"">
            <certificate fingerprint=""{certificateFingerprintString}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""true"" />
        </author>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(pathContext.WorkingDirectory, trustedSignersSectionContent, "configuration");

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    pathContext.PackageSource,
                    $"verify {packagePath} -Signatures -CertificateFingerprint {certificateFingerprintString};abc;def",
                    waitForExit: true);

                // Assert
                // If allowUntrustedRoot is set true in nuget.config then verify succeeds for cert with untrusted root.
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [CIOnlyFact]
        public async Task VerifyCommand_PackageSignedWithUntrustedCertificate_AllowUntrustedRootIsSetTrue_WrongNugetConfig_Fails()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext("A", "1.0.0");

            using (var pathContext = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_testFixture.UntrustedSelfIssuedCertificateInCertificateStore))
            {
                var packagePath = Path.Combine(pathContext.WorkingDirectory, nupkg.PackageName);

                //Act
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, pathContext.WorkingDirectory);
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);

                // Arrange
                string nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, NuGet.Configuration.Settings.DefaultSettingsFileName);
                string nugetConfigPath2 = Path.Combine(pathContext.WorkingDirectory, "nuget2.config");
                // nuget2.config doesn't have change for trustedSigners
                File.Copy(nugetConfigPath, nugetConfigPath2);

                var trustedSignersSectionContent = $@"
    <trustedSigners>
        <author name=""MyCert"">
            <certificate fingerprint=""{certificateFingerprintString}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""true"" />
        </author>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(pathContext.WorkingDirectory, trustedSignersSectionContent, "configuration");

                // Act
                // pass custom nuget2.config file, but doesn't have trustedSigners section
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    pathContext.PackageSource,
                    $"verify {packagePath} -All -CertificateFingerprint {certificateFingerprintString};abc;def -ConfigFile {nugetConfigPath2}",
                    waitForExit: true);

                // Assert
                // allowUntrustedRoot is not set true in nuget2.config, but in nuget.config, so verify fails.
                verifyResult.Success.Should().BeFalse();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_primarySignatureInvalidErrorCode);
            }
        }

        [CIOnlyFact]
        public async Task VerifyCommand_PackageSignedWithUntrustedCertificate_AllowUntrustedRootIsSetTrue_CorrectNugetConfig_Succeed()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext("A", "1.0.0");

            using (var pathContext = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_testFixture.UntrustedSelfIssuedCertificateInCertificateStore))
            {
                var packagePath = Path.Combine(pathContext.WorkingDirectory, nupkg.PackageName);

                //Act
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, pathContext.WorkingDirectory);
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);

                // Arrange
                string nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, NuGet.Configuration.Settings.DefaultSettingsFileName);
                string nugetConfigPath2 = Path.Combine(pathContext.WorkingDirectory, "nuget2.config");
                File.Copy(nugetConfigPath, nugetConfigPath2);

                var trustedSignersSectionContent = $@"
    <trustedSigners>
        <author name=""MyCert"">
            <certificate fingerprint=""{certificateFingerprintString}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""true"" />
        </author>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(nugetConfigPath2, trustedSignersSectionContent, "configuration");

                // Act
                // pass custom nuget2.config file, it has trustedSigners section
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    pathContext.PackageSource,
                    $"verify {packagePath} -All -CertificateFingerprint {certificateFingerprintString};abc;def -ConfigFile {nugetConfigPath2}",
                    waitForExit: true);

                // Assert
                // allowUntrustedRoot is set true in nuget2.config, so verify succeeds.
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }
    }
}
