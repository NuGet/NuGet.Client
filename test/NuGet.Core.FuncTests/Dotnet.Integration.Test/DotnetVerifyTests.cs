// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetVerifyTests
    {
        private readonly string _noTimestamperWarningCode = NuGetLogCode.NU3027.ToString();
        private readonly string _primarySignatureInvalidErrorCode = NuGetLogCode.NU3018.ToString();
        private readonly string _noMatchingCertErrorCode = NuGetLogCode.NU3034.ToString();
        private readonly string _notSignedErrorCode = NuGetLogCode.NU3004.ToString();

        private MsbuildIntegrationTestFixture _msbuildFixture;

        public DotnetVerifyTests(MsbuildIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [CIOnlyFact]
        public async Task Verify_UnSignedPackage_Fails()
        {
            using (var packageDir = TestDirectory.Create())
            {
                var packageId = "Unsigned.PackageX";
                var packageVersion = "1.0.0";
                var packageFile = await TestPackagesCore.GetRuntimePackageAsync(packageDir, packageId, packageVersion);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    packageDir,
                    $"nuget verify {packageFile.FullName}",
                    ignoreExitCode: true);

                result.Success.Should().BeFalse(because: result.AllOutput);
                result.Output.Should().Contain(_notSignedErrorCode);
            }
        }

        [PlatformFact(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        public void Verify_AuthorSignedAndTimestampedPackageWithOptionAll_Succeeds()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var packageFile = new FileInfo(Path.Combine(testDirectory, "TestPackage.AuthorSigned.1.0.0.nupkg"));
                var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {packageFile.FullName} --all",
                    ignoreExitCode: true);

                result.Success.Should().BeTrue(because: result.AllOutput);
                result.Output.Should().NotContain(_noTimestamperWarningCode);
                result.Output.Should().NotContain(_primarySignatureInvalidErrorCode);
            }
        }

        [CIOnlyFact]
        public void Verify_SignedPackageWithoutAllowedCertificate_Fails()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var packageFile = new FileInfo(Path.Combine(testDirectory, "TestPackage.AuthorSigned.1.0.0.nupkg"));
                var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {packageFile.FullName} " +
                    $"--certificate-fingerprint 775AAB607AA76028A7CC7A873A9513FF0C3B40DF09B7B83D21689A3675B34D9A --certificate-fingerprint DEF",
                    ignoreExitCode: true);

                result.Success.Should().BeFalse(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noMatchingCertErrorCode);
            }
        }

        [PlatformFact(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        public void Verify_SignedPackageWithAllowedCertificate_Succeeds()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var packageFile = new FileInfo(Path.Combine(testDirectory, "TestPackage.AuthorSigned.1.0.0.nupkg"));
                var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {packageFile.FullName} " +
                    $"--certificate-fingerprint 3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE  --certificate-fingerprint DEF",
                    ignoreExitCode: true);

                result.Success.Should().BeTrue(because: result.AllOutput);
            }
        }

        [PlatformFact(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        public void Verify_MultipleSignedPackagesWithWildCardAndDetailedVerbosity_MixedResults()
        {
            // Arrange
            using (var testDirectory1 = TestDirectory.Create())
            {
                using (var testDirectory2 = TestDirectory.Create())
                {
                    var packagX = new FileInfo(Path.Combine(testDirectory1, "TestPackage.AuthorSigned.1.0.0.nupkg"));
                    var bPackageX = SigningTestUtility.GetResourceBytes(packagX.Name);
                    File.WriteAllBytes(packagX.FullName, bPackageX);

                    var packageY = new FileInfo(Path.Combine(testDirectory2, "Test.Reposigned.1.0.0.nupkg"));
                    var bpackageY = SigningTestUtility.GetResourceBytes(packageY.Name);
                    File.WriteAllBytes(packageY.FullName, bpackageY);

                    //Act
                    var result = _msbuildFixture.RunDotnet(
                        testDirectory1,
                        $"nuget verify {packagX.FullName} {Path.Combine(testDirectory2, "*.nupkg")} -v d",
                        ignoreExitCode: true);

                    result.Success.Should().BeFalse(because: result.AllOutput);
                    result.AllOutput.Should().Contain("Successfully verified package 'TestPackage.AuthorSigned.1.0.0'.");
                    result.AllOutput.Should().Contain($"Verifying Test.Reposigned.1.0.0");
                    result.AllOutput.Should().Contain(_primarySignatureInvalidErrorCode);
                    result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                }
            }
        }

        [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        [InlineData("true", true)]
        [InlineData("true", false)]
        [InlineData("false", true)]
        [InlineData("false", false)]
        public void Verify_AuthorSignedPackageWithTrustedCertificate_AuthorTag_Succeeds(string trust, bool fingerPrintOption)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var testDirectory = pathContext.WorkingDirectory;
                var packageFile = new FileInfo(Path.Combine(testDirectory, "TestPackage.AuthorSigned.1.0.0.nupkg"));
                var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                var certificateFingerprintString = "3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE";

                var trustedSignersSectionContent = $@"
    <trustedSigners>
        <author name=""MyCert"">
            <certificate fingerprint=""{certificateFingerprintString}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{trust}"" />
        </author>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(pathContext.WorkingDirectory, trustedSignersSectionContent, "configuration");
                string fingerprint = fingerPrintOption ? $"--certificate-fingerprint {certificateFingerprintString}  --certificate-fingerprint DEF" : string.Empty;

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {packageFile.FullName} " +
                    fingerprint,
                    ignoreExitCode: true);

                // Assert
                // For certificate with trusted root setting allowUntrustedRoot to true/false doesn't matter
                result.Success.Should().BeTrue(because: result.AllOutput);
            }
        }

        [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        [InlineData("true", true)]
        [InlineData("true", false)]
        [InlineData("false", true)]
        [InlineData("false", false)]
        public void Verify_AuthorSignedPackageWithTrustedCertificate_RepositoryTag_Fails(string trust, bool fingerPrintOption)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var testDirectory = pathContext.WorkingDirectory;
                var packageFile = new FileInfo(Path.Combine(testDirectory, "TestPackage.AuthorSigned.1.0.0.nupkg"));
                var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                var certificateFingerprintString = "3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE";

                var trustedSignersSectionContent = $@"
    <trustedSigners>
        <repository name=""MyCert"" serviceIndex=""{pathContext.PackageSource}"">
            <certificate fingerprint=""{certificateFingerprintString}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{trust}"" />
        </repository>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(pathContext.WorkingDirectory, trustedSignersSectionContent, "configuration");
                string fingerprint = fingerPrintOption ? $"--certificate-fingerprint {certificateFingerprintString}  --certificate-fingerprint DEF" : string.Empty;

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {packageFile.FullName} " +
                    fingerprint,
                    ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noMatchingCertErrorCode);
                result.AllOutput.Should().Contain("This package is signed but not by a trusted signer.");
            }
        }

        [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        [InlineData(true)]
        [InlineData(false)]
        public void Verify_RepositorySignedPackageWithUntrustedCertificate_AuthorTag_Fails(bool trust)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var testDirectory = pathContext.WorkingDirectory;
                // Signed with cert with untrusted root
                var packageFile = new FileInfo(Path.Combine(testDirectory, "Test.Reposigned.1.0.0.nupkg"));
                var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                var certificateFingerprintString = "775AAB607AA76028A7CC7A873A9513FF0C3B40DF09B7B83D21689A3675B34D9A";

                var trustedSignersSectionContent = $@"
    <trustedSigners>
        <author name=""MyCert"">
            <certificate fingerprint=""{certificateFingerprintString}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{trust}"" />
        </author>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(pathContext.WorkingDirectory, trustedSignersSectionContent, "configuration");

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {packageFile.FullName} " +
                    $"--certificate-fingerprint {certificateFingerprintString}  --certificate-fingerprint DEF",
                    ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_noMatchingCertErrorCode);
                result.AllOutput.Should().Contain("This package is signed but not by a trusted signer.");

                if (!trust)
                {
                    result.AllOutput.Should().Contain(_primarySignatureInvalidErrorCode);
                }
            }
        }

        [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        [InlineData("false", true)]
        [InlineData("false", false)]
        [InlineData("FALSE", true)]
        [InlineData("FALSE", false)]
        public void Verify_RepositorySignedPackageWithUntrustedCertificate_RepositoryTag_AllowUntrustedRootSetFalse_Fails(string trust, bool fingerPrintOption)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var testDirectory = pathContext.WorkingDirectory;
                // Signed with cert with untrusted root
                var packageFile = new FileInfo(Path.Combine(testDirectory, "Test.Reposigned.1.0.0.nupkg"));
                var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                var certificateFingerprintString = "775AAB607AA76028A7CC7A873A9513FF0C3B40DF09B7B83D21689A3675B34D9A";

                var trustedSignersSectionContent = $@"
    <trustedSigners>
        <repository name=""MyCert"" serviceIndex = ""{pathContext.PackageSource}"">
            <certificate fingerprint=""{certificateFingerprintString}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{trust}"" />
        </repository>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(pathContext.WorkingDirectory, trustedSignersSectionContent, "configuration");
                string fingerprint = fingerPrintOption ? $"--certificate-fingerprint {certificateFingerprintString}  --certificate-fingerprint DEF" : string.Empty;

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {packageFile.FullName} " +
                    fingerprint,
                    ignoreExitCode: true);

                // Assert
                // Unless allowUntrustedRoot is set true in nuget.config verify always fails for cert without trusted root.
                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_primarySignatureInvalidErrorCode);
                result.AllOutput.Should().Contain("The repository primary signature's signing certificate is not trusted by the trust provider.");
            }
        }

        [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        [InlineData("true", true)]
        [InlineData("true", false)]
        [InlineData("TRUE", true)]
        [InlineData("TRUE", false)]
        public void Verify_RepositorySignedPackageWithUntrustedCertificate_RepositoryTag_AllowUntrustedRootSetTrue_Succeeds(string trust, bool fingerPrintOption)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var testDirectory = pathContext.WorkingDirectory;
                // Signed with cert with untrusted root
                var packageFile = new FileInfo(Path.Combine(testDirectory, "Test.Reposigned.1.0.0.nupkg"));
                var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                var certificateFingerprintString = "775AAB607AA76028A7CC7A873A9513FF0C3B40DF09B7B83D21689A3675B34D9A";

                var trustedSignersSectionContent = $@"
    <trustedSigners>
        <repository name=""MyCert"" serviceIndex=""{pathContext.PackageSource}"">
            <certificate fingerprint=""{certificateFingerprintString}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{trust}"" />
        </repository>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(pathContext.WorkingDirectory, trustedSignersSectionContent, "configuration");
                string fingerprint = fingerPrintOption ? $"--certificate-fingerprint {certificateFingerprintString}  --certificate-fingerprint DEF" : string.Empty;

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {packageFile.FullName} " +
                    fingerprint,
                    ignoreExitCode: true);

                // Assert
                // If allowUntrustedRoot is set true in nuget.config then verify succeeds for cert with untrusted root.
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        [InlineData("true", true)]
        [InlineData("false", true)]
        [InlineData("true", false)]
        [InlineData("false", false)]
        [InlineData("TRUE", true)]
        [InlineData("TRUE", false)]
        public async Task Verify_RepositorySignedPackageWithUntrustedCertificate_RepositoryTag_AllowUntrustedRootSetTrue_WrongOwners_Fails(string trust, bool fingerPrintOption)
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            using (MemoryStream zipStream = await package.CreateAsStreamAsync())
            using (X509Certificate2 trustedTestCert = SigningTestUtility.GenerateSelfIssuedCertificate(isCa: false))
            {
                var certFingerprint = SignatureTestUtility.GetFingerprint(trustedTestCert, HashAlgorithmName.SHA256);
                var packageOwners = new List<string>()
                {
                    "nuget",
                    "contoso"
                };
                var repoServiceIndex = "https://serviceindex.test/v3/index.json";
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedTestCert, package, pathContext.PackageSource, new Uri(repoServiceIndex), null, packageOwners);

                string testDirectory = pathContext.WorkingDirectory;

                var trustedSignersSectionContent = $@"
    <trustedSigners>
    <repository name=""NuGetTrust"" serviceIndex=""{repoServiceIndex}"">
      <certificate fingerprint=""{certFingerprint}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{trust}"" />
      <owners>Nuget;Contoso</owners>
    </repository>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(testDirectory, trustedSignersSectionContent, "configuration");
                string fingerprint = fingerPrintOption ? $"--certificate-fingerprint {certFingerprint} --certificate-fingerprint DEF" : string.Empty;

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {signedPackagePath} " +
                    fingerprint,
                    ignoreExitCode: true);

                // Assert
                // Owners is casesensitive, owner info should be "nuget;contoso" not "Nuget;Contoso"
                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noMatchingCertErrorCode);
            }
        }

        [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        [InlineData("true", true)]
        [InlineData("true", false)]
        [InlineData("TRUE", true)]
        [InlineData("TRUE", false)]
        public async Task Verify_RepositorySignedPackageWithUntrustedCertificate_RepositoryTag_AllowUntrustedRootSetTrue_CorrectOwners_Succeeds(string trust, bool fingerPrintOption)
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            using (MemoryStream zipStream = await package.CreateAsStreamAsync())
            using (X509Certificate2 trustedTestCert = SigningTestUtility.GenerateSelfIssuedCertificate(isCa: false))
            {
                var certFingerprint = SignatureTestUtility.GetFingerprint(trustedTestCert, HashAlgorithmName.SHA256);
                var packageOwners = new List<string>()
                {
                    "nuget",
                    "contoso"
                };
                var repoServiceIndex = "https://serviceindex.test/v3/index.json";
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedTestCert, package, pathContext.PackageSource, new Uri(repoServiceIndex), null, packageOwners);

                string testDirectory = pathContext.WorkingDirectory;

                var trustedSignersSectionContent = $@"
    <trustedSigners>
    <repository name=""NuGetTrust"" serviceIndex=""{repoServiceIndex}"">
      <certificate fingerprint=""{certFingerprint}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{trust}"" />
      <owners>nuget;Contoso</owners>
    </repository>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(testDirectory, trustedSignersSectionContent, "configuration");
                string fingerprint = fingerPrintOption ? $"--certificate-fingerprint {certFingerprint} --certificate-fingerprint DEF" : string.Empty;

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {signedPackagePath} " +
                    fingerprint,
                    ignoreExitCode: true);

                // Assert
                // Owners is casesensitive, here owner "nuget" matches
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        [InlineData("true", true)]
        [InlineData("false", true)]
        [InlineData("false", false)]
        [InlineData("true", false)]
        [InlineData("TRUE", true)]
        [InlineData("TRUE", false)]
        public async Task Verify_RepositorySignedPackageWithTrustedCertificate_RepositoryTag_AllowUntrustedRootSet_WrongOwners_Fails(string trust, bool fingerPrintOption)
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            using (MemoryStream zipStream = await package.CreateAsStreamAsync())
            using (TrustedTestCert<TestCertificate> trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate())
            {
                var certFingerprint = SignatureTestUtility.GetFingerprint(trustedTestCert.Source.Cert, HashAlgorithmName.SHA256);
                var packageOwners = new List<string>()
                {
                    "nuget",
                    "contoso"
                };
                var repoServiceIndex = "https://serviceindex.test/v3/index.json";
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedTestCert.Source.Cert, package, pathContext.PackageSource, new Uri(repoServiceIndex), null, packageOwners);

                string testDirectory = pathContext.WorkingDirectory;

                var trustedSignersSectionContent = $@"
    <trustedSigners>
    <repository name=""NuGetTrust"" serviceIndex=""{repoServiceIndex}"">
      <certificate fingerprint=""{certFingerprint}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{trust}"" />
      <owners>Nuget;Contoso</owners>
    </repository>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(testDirectory, trustedSignersSectionContent, "configuration");
                string fingerprint = fingerPrintOption ? $"--certificate-fingerprint {certFingerprint} --certificate-fingerprint DEF" : string.Empty;

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {signedPackagePath} " +
                    fingerprint,
                    ignoreExitCode: true);

                // Assert
                // Owners is casesensitive, owner info should be "nuget;contoso" not "Nuget;Contoso"
                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noMatchingCertErrorCode);
            }
        }

        [PlatformTheory(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        [InlineData("true", true)]
        [InlineData("false", true)]
        [InlineData("false", false)]
        [InlineData("true", false)]
        [InlineData("TRUE", true)]
        [InlineData("TRUE", false)]
        public async Task Verify_RepositorySignedPackageWithTrustedCertificate_RepositoryTag_AllowUntrustedRootSet_CorrectOwners_Succeeds(string trust, bool fingerPrintOption)
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (SimpleTestPathContext pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            using (MemoryStream zipStream = await package.CreateAsStreamAsync())
            using (TrustedTestCert<TestCertificate> trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate())
            {
                var certFingerprint = SignatureTestUtility.GetFingerprint(trustedTestCert.Source.Cert, HashAlgorithmName.SHA256);
                var packageOwners = new List<string>()
                {
                    "nuget",
                    "contoso"
                };
                var repoServiceIndex = "https://serviceindex.test/v3/index.json";
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(trustedTestCert.Source.Cert, package, pathContext.PackageSource, new Uri(repoServiceIndex), null, packageOwners);

                string testDirectory = pathContext.WorkingDirectory;

                var trustedSignersSectionContent = $@"
    <trustedSigners>
    <repository name=""NuGetTrust"" serviceIndex=""{repoServiceIndex}"">
      <certificate fingerprint=""{certFingerprint}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""{trust}"" />
      <owners>nuget;Contoso</owners>
    </repository>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(testDirectory, trustedSignersSectionContent, "configuration");
                string fingerprint = fingerPrintOption ? $"--certificate-fingerprint {certFingerprint} --certificate-fingerprint DEF" : string.Empty;

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {signedPackagePath} " +
                    fingerprint,
                    ignoreExitCode: true);

                // Assert
                // For certificate with trusted root setting allowUntrustedRoot value true/false doesn't matter.
                // Owners is casesensitive, here owner "nuget" matches
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }

        [PlatformFact(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        public void Verify_RepositorySignedPackageWithUntrustedCertificate_AllowUntrustedRootIsSetTrue_PassWrongNugetConfigOption_Fails()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var testDirectory = pathContext.WorkingDirectory;
                // Signed with cert with untrusted root
                var packageFile = new FileInfo(Path.Combine(testDirectory, "Test.Reposigned.1.0.0.nupkg"));
                var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                var certificateFingerprintString = "775AAB607AA76028A7CC7A873A9513FF0C3B40DF09B7B83D21689A3675B34D9A";
                string nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, NuGet.Configuration.Settings.DefaultSettingsFileName);
                string nugetConfigPath2 = Path.Combine(pathContext.WorkingDirectory, "nuget2.config");
                // nuget2.config doesn't have change for trustedSigners
                File.Copy(nugetConfigPath, nugetConfigPath2);

                var trustedSignersSectionContent = $@"
    <trustedSigners>
        <repository name=""MyCert"">
            <certificate fingerprint=""{certificateFingerprintString}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""true"" />
        </repository>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(pathContext.WorkingDirectory, trustedSignersSectionContent, "configuration");

                //Act
                // pass custom nuget2.config file, but doesn't have trustedSigners section
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {packageFile.FullName} " +
                    $"--certificate-fingerprint {certificateFingerprintString}  --certificate-fingerprint DEF --configfile {nugetConfigPath2}",
                    ignoreExitCode: true);

                // Assert
                // allowUntrustedRoot is not set true in nuget2.config, but in nuget.config, so verify fails.
                result.Success.Should().BeFalse();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                result.AllOutput.Should().Contain(_primarySignatureInvalidErrorCode);
            }
        }

        [PlatformFact(Platform.Windows, Platform.Linux)] // https://github.com/NuGet/Home/issues/11178
        public void Verify_RepositorySignedPackageWithUntrustedCertificate_AllowUntrustedRootIsSetTrue_PassCorrectNugetConfigOption_Succeeds()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var testDirectory = pathContext.WorkingDirectory;
                // Signed with cert with untrusted root
                var packageFile = new FileInfo(Path.Combine(testDirectory, "Test.Reposigned.1.0.0.nupkg"));
                var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                var certificateFingerprintString = "775AAB607AA76028A7CC7A873A9513FF0C3B40DF09B7B83D21689A3675B34D9A";
                string nugetConfigPath = Path.Combine(pathContext.WorkingDirectory, NuGet.Configuration.Settings.DefaultSettingsFileName);
                string nugetConfigPath2 = Path.Combine(pathContext.WorkingDirectory, "nuget2.config");
                File.Copy(nugetConfigPath, nugetConfigPath2);

                var trustedSignersSectionContent = $@"
    <trustedSigners>
        <repository name=""MyCert"" serviceIndex=""{pathContext.PackageSource}"">
            <certificate fingerprint=""{certificateFingerprintString}"" hashAlgorithm=""SHA256"" allowUntrustedRoot=""true"" />
        </repository>
    </trustedSigners>
";
                SimpleTestSettingsContext.AddSectionIntoNuGetConfig(nugetConfigPath2, trustedSignersSectionContent, "configuration");

                //Act
                // pass custom nuget2.config file, it has trustedSigners section
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {packageFile.FullName} " +
                    $"--certificate-fingerprint {certificateFingerprintString}  --certificate-fingerprint DEF --configfile {nugetConfigPath2}",
                    ignoreExitCode: true);

                // Assert
                // allowUntrustedRoot is set true in nuget2.config, so verify succeeds.
                result.Success.Should().BeTrue();
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }
    }
}
