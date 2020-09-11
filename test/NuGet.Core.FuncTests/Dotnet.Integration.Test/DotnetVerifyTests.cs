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
        private readonly string _signingDefaultErrorCode = NuGetLogCode.NU3000.ToString();
        private readonly string _noMatchingCertErrorCode = NuGetLogCode.NU3034.ToString();

        private MsbuildIntegrationTestFixture _msbuildFixture;

        public DotnetVerifyTests(MsbuildIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [CIOnlyFact]
        public void Verify_AuthorSignedPackage_Succceeds()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                string packageX = "TestPackage.AuthorSigned.1.0.0.nupkg";
                CopyPackageFromResources(packageX, testDirectory);

                //Act
                var result = _msbuildFixture.RunDotnet(
                    testDirectory,
                    $"nuget verify {Path.Combine(testDirectory, packageX)}",
                    ignoreExitCode: true);

                result.Success.Should().BeTrue(because: result.AllOutput);
            }
        }

        [CIOnlyFact]
        public void Verify_MultipleSignedPackagesWithWildCard_Succceeds()
        {
            // Arrange
            using (var testDirectory1 = TestDirectory.Create())
            {
                using (var testDirectory2 = TestDirectory.Create())
                {
                    string packageX = "TestPackage.AuthorSigned.1.0.0.nupkg";
                    string packageY = "Test.Reposigned.1.0.0.nupkg";
                    string packageZ = "Test.RepoCountersigned.1.0.0.nupkg";

                    CopyPackageFromResources(packageX, testDirectory1);
                    CopyPackageFromResources(packageY, testDirectory2);
                    CopyPackageFromResources(packageZ, testDirectory2);

                    //Act
                    var result = _msbuildFixture.RunDotnet(
                        testDirectory1,
                        $"nuget verify {Path.Combine(testDirectory1, packageX)} {Path.Combine(testDirectory2, "*.nupkg")}",
                        ignoreExitCode: true);

                    result.Success.Should().BeFalse(because: result.AllOutput);
                    result.AllOutput.Contains(_primarySignatureInvalidErrorCode);
                    result.AllOutput.Contains(_noTimestamperWarningCode);
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
