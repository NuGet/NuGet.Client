// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetVerifyTests
    {

        private readonly string _noTimestamperWarningCode = NuGetLogCode.NU3027.ToString();
        private readonly string _primarySignatureInvalidErrorCode = NuGetLogCode.NU3018.ToString();
        private readonly string _noMatchingCertErrorCode = NuGetLogCode.NU3034.ToString();

        private MsbuildIntegrationTestFixture _msbuildFixture;

        public DotnetVerifyTests(MsbuildIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [CIOnlyTheory]
        [InlineData("--all")]
        [InlineData("")]
        public void Verify_AuthorSignedAndTimestampedPackage_Succceeds(string optionAll)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                string packageX = "TestPackage.AuthorSigned.1.0.0.nupkg";
                CopyPackageFromResources(packageX, testDirectory);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {Path.Combine(testDirectory, packageX)} {optionAll}",
                    ignoreExitCode: true);

                result.Success.Should().BeTrue(because: result.AllOutput);
                result.Output.Should().NotContain(_noTimestamperWarningCode);
                result.Output.Should().NotContain(_primarySignatureInvalidErrorCode);
            }
        }

        [CIOnlyTheory]
        [InlineData("--certificate-fingerprint 775AAB607AA76028A7CC7A873A9513FF0C3B40DF09B7B83D21689A3675B34D9A")]
        [InlineData("--certificate-fingerprint ABC --certificate-fingerprint DEF")]
        public void Verify_AuthorSignedPackageWithoutAllowedCertificate_Fails(string fingerprints)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                string packageX = "TestPackage.AuthorSigned.1.0.0.nupkg";
                CopyPackageFromResources(packageX, testDirectory);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {Path.Combine(testDirectory, packageX)} {fingerprints}",
                    ignoreExitCode: true);

                result.Success.Should().BeFalse(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noMatchingCertErrorCode);
            }
        }

        [CIOnlyFact]
        public void Verify_MultipleSignedPackagesWithWildCardAndDetailedVerbosity_MixedResults()
        {
            // Arrange
            using (var testDirectory1 = TestDirectory.Create())
            {
                using (var testDirectory2 = TestDirectory.Create())
                {
                    string packageX = "TestPackage.AuthorSigned.1.0.0.nupkg";
                    string packageY = "Test.Reposigned.1.0.0.nupkg";

                    CopyPackageFromResources(packageX, testDirectory1);
                    CopyPackageFromResources(packageY, testDirectory2);

                    //Act
                    var result = _msbuildFixture.RunDotnet(
                        testDirectory1,
                        $"nuget verify {Path.Combine(testDirectory1, packageX)} {Path.Combine(testDirectory2, "*.nupkg")} -v d",
                        ignoreExitCode: true);

                    result.Success.Should().BeFalse(because: result.AllOutput);
                    result.AllOutput.Should().Contain("Successfully verified package 'TestPackage.AuthorSigned.1.0.0'.");
                    result.AllOutput.Should().Contain($"Verifying Test.Reposigned.1.0.0");
                    result.AllOutput.Should().Contain(_primarySignatureInvalidErrorCode);
                    result.AllOutput.Should().Contain(_noTimestamperWarningCode);
                }
            }
        }

        private static void CopyPackageFromResources(string packageId, string destination)
        {
            var packageFile = new FileInfo(Path.Combine(destination, packageId));
            var package = GetResource(packageId);
            File.WriteAllBytes(packageFile.FullName, package);
        }

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"Dotnet.Integration.Test.compiler.resources.{name}",
                typeof(DotnetRestoreTests));
        }
    }
}
