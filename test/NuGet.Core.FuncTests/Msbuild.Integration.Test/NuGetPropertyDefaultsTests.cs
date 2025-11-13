// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using FluentAssertions;
using NuGet.Frameworks;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Msbuild.Integration.Test
{
    public class NuGetPropertyDefaultsTests : IClassFixture<MsbuildIntegrationTestFixture>
    {
        private MsbuildIntegrationTestFixture _fixture;
        private ITestOutputHelper _output;

        public NuGetPropertyDefaultsTests(MsbuildIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Theory]
        // .NET (Core) SDK 10.0
        [InlineData("10.0.100", "net10.0", "false")]
        [InlineData("10.0.100", "net9.0", "false")]
        [InlineData("10.0.100", "net8.0", "false")]
        [InlineData("10.0.100", "net7.0", "false")]
        [InlineData("10.0.100", "net6.0", "false")]
        [InlineData("10.0.100", "net5.0", "false")]
        [InlineData("10.0.100", "netcoreapp3.1", "false")]
        [InlineData("10.0.100", "netcoreapp3.0", "false")]
        [InlineData("10.0.100", "netcoreapp2.2", "false")]
        [InlineData("10.0.100", "netcoreapp2.1", "false")]
        [InlineData("10.0.100", "netcoreapp2.0", "false")]
        [InlineData("10.0.100", "netcoreapp1.1", "false")]
        [InlineData("10.0.100", "netcoreapp1.0", "false")]
        // .NET Standard SDK 10.0
        [InlineData("10.0.100", "netstandard2.1", "false")]
        [InlineData("10.0.100", "netstandard2.0", "false")]
        [InlineData("10.0.100", "netstandard1.6", "false")]
        // .NET Framework SDK 10.0
        [InlineData("10.0.100", "net48", "false")]
        // SDK 10.0
        [InlineData("9.0.100", "net10.0", "false")]
        [InlineData("9.0.100", "net9.0", "false")]
        [InlineData("9.0.100", "net8.0", "false")]
        [InlineData("9.0.100", "netstandard2.1", "false")]
        [InlineData("9.0.100", "netstandard2.0", "false")]
        public void PackagePruningDefaults_RestoreEnablePackagePruning(string SdkAnalysisLevel, string targetFramework, string expected)
        {
            // Arrange
            using var testDirectory = TestDirectory.Create();

            string projectText = @"<Project>
    <Import Project=""$(NuGetRestoreTargets)"" />
</Project>";
            var projectFilePath = Path.Combine(testDirectory, "my.proj");
            File.WriteAllText(projectFilePath, projectText);

            string args = $"{projectFilePath} -getProperty:RestoreEnablePackagePruning";
            if (!string.IsNullOrEmpty(SdkAnalysisLevel)) args += $" -p:SdkAnalysisLevel={SdkAnalysisLevel}";

            var framework = NuGetFramework.Parse(targetFramework);
            args += $" -p:TargetFrameworkIdentifier={framework.Framework}";
            args += $" -p:TargetFrameworkVersion={framework.Version}";
            args += $" -p:UsingMicrosoftNETSdk=true";

            // Act
            var result = _fixture.RunMsBuild(testDirectory, args);
            var resultText = result.Output.Trim();

            // Assert
            resultText.Should().Be(expected);
        }
    }
}
