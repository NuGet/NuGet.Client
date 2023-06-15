// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection(DotnetIntegrationCollection.Name)]
    public class ProjectPropertyTests
    {
        private MsbuildIntegrationTestFixture _msbuildFixture;

        public ProjectPropertyTests(MsbuildIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [Fact]
        public void NuGetAudit_EnabledByDefaultOnNet8AndHigher()
        {
            // Arrange
            using TestDirectory testDirectory = TestDirectory.Create();
            string projectFilePath = Path.Combine(testDirectory.Path, "test.csproj");
            var projectFileContents = @"<Project Sdk=""Microsoft.NET.Sdk"">
    <Target Name=""ValidateNuGetAuditValue"">
        <Message Importance=""high"" Text=""NuGetAudit: '$(NuGetAudit)'"" />
        <Error Text=""ExpectEnabled was not set"" Condition="" '$(ExpectEnabled)' != 'true' AND '$(ExpectEnabled)' != 'false' "" />
        <Message Importance=""high"" Text=""SdkVersion: $(NETCoreSdkVersion) "" />
    </Target>
    <Target Name=""ValidateEnabled"" Condition="" '$(ExpectEnabled)' == 'true' "" AfterTargets=""ValidateNuGetAuditValue"">
        <Error Text=""NuGetAudit property should be set to true by default"" Condition="" '$(NuGetAudit)' != 'true' "" />
    </Target>
    <Target Name=""ValidateNotSet"" Condition="" '$(ExpectEnabled)' != 'true' "" AfterTargets=""ValidateNuGetAuditValue"">
        <Error Text=""NuGetAudit property should not be set"" Condition="" '$(NuGetAudit)' != '' "" />
    </Target>
</Project>";
            File.WriteAllText(projectFilePath, projectFileContents);

            // Dotnet.Integration.Tests works by finding the test assembly compile TFM, then finding the SDK that maches the TFM's major version.
            // (see TestDotnetCliUtility.GetSdkToTestByAssemblyPath).
            // Therefore, we can depend on this file's compile time TFM to tell us if we're testing the .NET 7 or 8 SDK.
            string expected =
#if NET8_0_OR_GREATER
                "true";
#else
                "false";
#endif

            // Act
            var result = _msbuildFixture.RunDotnet(testDirectory.Path, $"msbuild -t:ValidateNuGetAuditValue -p:ExpectEnabled={expected}");

            // Assert
            result.Success.Should().BeTrue(result.AllOutput);
        }
    }
}
