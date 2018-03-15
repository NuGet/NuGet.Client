// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging;
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
        private readonly string _noMatchingCertErrorCode = NuGetLogCode.NU3003.ToString();
        private readonly string _repositorySignatureType = "Signature type: Repository";

        private SignCommandTestFixture _testFixture;
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private string _nugetExePath;

        private readonly string _repoSignedPackageResourceName = "Test.Reposigned.1.0.0.nupkg";
        private readonly string _repoCountersignedPackageResourceName = "Test.RepoCountersigned.1.0.0.nupkg";
        private readonly string _authorSigningCertResourceSHA256Fingerprint = "BB2C007632B1831D9BD91B9A0AE07982EEE5A86F7B1C7CD2B436C56F5F2B1597";
        private readonly string _repositorySigningCertResourceSHA256Fingerprint = "775AAB607AA76028A7CC7A873A9513FF0C3B40DF09B7B83D21689A3675B34D9A";


        public VerifyCommandTests(SignCommandTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _nugetExePath = _testFixture.NuGetExePath;
        }

        [CIOnlyFact]
        public void VerifyCommand_VerifySignedPackageSucceeds()
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
        public async Task VerifyCommand_VerifySignedAndTimestampedPackageSucceeds()
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
        public void VerifyCommand_VerifyResignedPackageSucceeds()
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
        public void VerifyCommand_VerifyOnPackageSignedWithValidCertificateChainSucceeds()
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
        public void VerifyCommand_VerifyOnPackageSignedWithAllowedCertificateSucceeds()
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
        public void VerifyCommand_VerifyOnPackageSignedWithoutAllowedCertificateFails()
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
        public void VerifyCommand_VerifyOnPackageAuthorSigned_WithTrustedSource_Fails()
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

                var signResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"sign {packagePath} -CertificateFingerprint {_trustedTestCert.Source.Cert.Thumbprint} -CertificateStoreName {_trustedTestCert.StoreName} -CertificateStoreLocation {_trustedTestCert.StoreLocation}",
                    waitForExit: true);

                signResult.Success.Should().BeTrue();

                var configFilePath = CreateConfigFileWithRepoTrustedSource("CustomNuGet.Config", dir);

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures -Config {configFilePath}",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeFalse();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_noMatchingCertErrorCode);
            }
        }

        [CIOnlyFact]
        public void VerifyCommand_VerifyOnPackageRepositorySigned_WithTrustedSource_Succeeds()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var nupkgStream = new MemoryStream(GetResource(_repoSignedPackageResourceName)))
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                nupkgStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    nupkgStream.CopyTo(fileStream);
                }

                var configFilePath = CreateConfigFileWithRepoTrustedSource("CustomNuGet.Config", dir);

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures -Config {configFilePath}",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_repositorySignatureType);
            }
        }

        [CIOnlyFact]
        public void VerifyCommand_VerifyOnPackageRepositorySigned_WithUntrustedSource_Fails()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var nupkgStream = new MemoryStream(GetResource(_repoSignedPackageResourceName)))
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                nupkgStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    nupkgStream.CopyTo(fileStream);
                }

                var configFilePath = CreateConfigFileWithoutRepoTrustedSource("CustomNuGet.Config", dir);

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures -Config {configFilePath}",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeFalse();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_noMatchingCertErrorCode);
            }
        }

        [CIOnlyFact]
        public void VerifyCommand_VerifyOnPackageCountersigned_WithTrustedSource_Succeeds()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var nupkgStream = new MemoryStream(GetResource(_repoCountersignedPackageResourceName)))
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                nupkgStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    nupkgStream.CopyTo(fileStream);
                }

                var configFilePath = CreateConfigFileWithRepoTrustedSource("CustomNuGet.Config", dir);

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures -Config {configFilePath}",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_repositorySignatureType);
            }
        }

        [CIOnlyFact]
        public void VerifyCommand_VerifyOnPackageCountersigned_WithUntrustedSource_Fails()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var nupkgStream = new MemoryStream(GetResource(_repoCountersignedPackageResourceName)))
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                nupkgStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    nupkgStream.CopyTo(fileStream);
                }

                var configFilePath = CreateConfigFileWithoutRepoTrustedSource("CustomNuGet.Config", dir);

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures -Config {configFilePath}",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeFalse();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_noMatchingCertErrorCode);
            }
        }


        [CIOnlyFact]
        public void VerifyCommand_VerifyOnPackageSigned_WithTrustedSource_AndCertificateFingerprintNotInList_Succeeds()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var nupkgStream = new MemoryStream(GetResource(_repoSignedPackageResourceName)))
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                nupkgStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    nupkgStream.CopyTo(fileStream);
                }

                var configFilePath = CreateConfigFileWithRepoTrustedSource("CustomNuGet.Config", dir);

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures -Config {configFilePath} -CertificateFingerprint abc;def",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_repositorySignatureType);
            }
        }

        [CIOnlyFact]
        public void VerifyCommand_VerifyOnPackageSigned_WithUntrustedSource_AndCertificateFingerprintInList_Succeeds()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var nupkgStream = new MemoryStream(GetResource(_repoSignedPackageResourceName)))
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                nupkgStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    nupkgStream.CopyTo(fileStream);
                }

                var configFilePath = CreateConfigFileWithoutRepoTrustedSource("CustomNuGet.Config", dir);

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures -Config {configFilePath} -CertificateFingerprint {_repositorySigningCertResourceSHA256Fingerprint};abc;def",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_repositorySignatureType);
            }
        }

        [CIOnlyFact]
        public void VerifyCommand_VerifyOnPackageCountersigned_WithTrustedSource_AndCertificateFingerprintNotInList_Succeeds()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var nupkgStream = new MemoryStream(GetResource(_repoCountersignedPackageResourceName)))
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                nupkgStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    nupkgStream.CopyTo(fileStream);
                }

                var configFilePath = CreateConfigFileWithRepoTrustedSource("CustomNuGet.Config", dir);

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures -Config {configFilePath} -CertificateFingerprint abc;def",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeTrue();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_repositorySignatureType);
            }
        }

        [CIOnlyFact]
        public void VerifyCommand_VerifyOnPackageCountersigned_WithUntrustedSource_AndCertificateFingerprintNotInList_Fails()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var nupkgStream = new MemoryStream(GetResource(_repoCountersignedPackageResourceName)))
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                nupkgStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    nupkgStream.CopyTo(fileStream);
                }

                var configFilePath = CreateConfigFileWithoutRepoTrustedSource("CustomNuGet.Config", dir);

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures -Config {configFilePath} -CertificateFingerprint abc;def",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeFalse();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_noMatchingCertErrorCode);
            }
        }

        [CIOnlyFact]
        public void VerifyCommand_VerifyOnPackageSigned_WithUntrustedSource_AndCertificateFingerprintNotInList_Fails()
        {
            // Arrange
            using (var dir = TestDirectory.Create())
            using (var nupkgStream = new MemoryStream(GetResource(_repoSignedPackageResourceName)))
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());
                nupkgStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    nupkgStream.CopyTo(fileStream);
                }

                var configFilePath = CreateConfigFileWithoutRepoTrustedSource("CustomNuGet.Config", dir);

                // Act
                var verifyResult = CommandRunner.Run(
                    _nugetExePath,
                    dir,
                    $"verify {packagePath} -Signatures -Config {configFilePath} -CertificateFingerprint abc;def",
                    waitForExit: true);

                // Assert
                verifyResult.Success.Should().BeFalse();
                verifyResult.AllOutput.Should().Contain(_noTimestamperWarningCode);
                verifyResult.AllOutput.Should().Contain(_noMatchingCertErrorCode);
            }
        }

        private string CreateConfigFileWithRepoTrustedSource(string configFileName, string dir)
        {
            var configContent = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources> 
        <add key='TestTrustedSource' value='https://source.test/api/v1' /> 
    </packageSources> 
    <trustedSources> 
        <TestTrustedSource> 
            <add key='{_repositorySigningCertResourceSHA256Fingerprint}' value='Test Repository Signing Cert' fingerprintAlgorithm='SHA256' /> 
        </TestTrustedSource> 
    </trustedSources> 
</configuration>";

            return CreateFile(configFileName, dir, configContent);
        }

        private string CreateConfigFileWithoutRepoTrustedSource(string configFileName, string dir)
        {
            var configContent = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='TestTrustedSource' value='https://source.test/api/v1' />
        <add key='TestRandomTrustedSource' value='https://randomsource.test/api/v1' /> 
    </packageSources> 
    <trustedSources> 
        <TestRandomTrustedSource> 
            <add key='HASH' value='Random Cert' fingerprintAlgorithm='SHA256' /> 
        </TestRandomTrustedSource> 
    </trustedSources> 
</configuration>";

            return CreateFile(configFileName, dir, configContent);
        }

        private static string CreateFile(string fileName, string directory, string content)
        {
            var configFullFilePath = Path.Combine(directory, fileName);
            using (var file = File.Create(configFullFilePath))
            {
                var info = new UTF8Encoding(true).GetBytes(content);
                file.Write(info, 0, info.Count());
            }

            return configFullFilePath;
        }

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"NuGet.CommandLine.FuncTest.compiler.resources.{name}",
                typeof(VerifyCommandTests));
        }
    }
}