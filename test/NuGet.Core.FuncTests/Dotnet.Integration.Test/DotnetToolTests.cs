// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.IO;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetToolTests
    {
        private MsbuildIntegrationTestFixture _msbuildFixture;

        public DotnetToolTests(MsbuildIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [Fact]
        public void DotnetToolTests_NoPackageReferenceToolRestore_ThrowsError()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = workingDirectory;
                var tfm = "netcoreapp1.0";
                var rid = "win7-x86";
                var packages = new List<PackageIdentity>();

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: rid,
                    source: workingDirectory, packages: packages);
                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 1, result.AllOutput);
                Assert.Contains("NU1211", result.Item2);
            }

        }
    }

}
