// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetVerifyTests
    {
        private MsbuildIntegrationTestFixture _msbuildFixture;

        public DotnetVerifyTests(MsbuildIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [Fact]
        public void Verify_WithAuthorSignedPackage_Succceeds()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var packageFile = new FileInfo(Path.Combine(testDirectory, "TestPackage.AuthorSigned.1.0.0.nupkg"));
                var package = GetResource(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                //Act
                var result = _msbuildFixture.RunDotnet(testDirectory, $"nuget verify {packageFile.FullName}", ignoreExitCode: true);
                result.Success.Should().BeTrue(because: result.AllOutput);
            }
        }
        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"Dotnet.Integration.Test.compiler.resources.{name}",
                typeof(DotnetRestoreTests));
        }
    }
}
