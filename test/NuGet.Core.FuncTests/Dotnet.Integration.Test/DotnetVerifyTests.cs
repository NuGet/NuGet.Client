// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
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

        // https://github.com/NuGet/Home/issues/11178
        // https://github.com/NuGet/Home/issues/11892
        [PlatformFact(Platform.Windows)]
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

        // https://github.com/NuGet/Home/issues/11892
        // https://github.com/NuGet/Home/issues/11178
        [PlatformFact(Platform.Windows)]
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

        // https://github.com/NuGet/Home/issues/11178
        // https://github.com/NuGet/Home/issues/11892
        [PlatformFact(Platform.Windows)]
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
    }
}
