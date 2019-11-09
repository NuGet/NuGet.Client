// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Configuration;
using NuGet.VisualStudio.Telemetry;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    public class TelemetryUtilityTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void CreateFileAndForgetEventName_WhenTypeNameIsNullOrEmpty_Throws(string typeName)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => TelemetryUtility.CreateFileAndForgetEventName(typeName, "memberName"));

            Assert.Equal("typeName", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void CreateFileAndForgetEventName_WhenMemberNameIsNullOrEmpty_Throws(string memberName)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => TelemetryUtility.CreateFileAndForgetEventName("typeName", memberName));

            Assert.Equal("memberName", exception.ParamName);
        }

        [Fact]
        public void CreateFileAndForgetEventName_WhenArgumentsAreValid_ReturnsString()
        {
            string actualResult = TelemetryUtility.CreateFileAndForgetEventName("a", "b");

            Assert.Equal("VS/NuGet/fileandforget/a/b", actualResult);
        }

        [Theory]
        [InlineData("http://nuget.org/api/v2", true)]
        [InlineData("http://NUGET.ORG/api/v2", true)]
        [InlineData("https://nuget.org/api/v2", true)]
        [InlineData("https://NUGET.ORG/api/v2", true)]
        [InlineData("http://www.nuget.org/api/v2", true)]
        [InlineData("http://WWW.NUGET.ORG/api/v2", true)]
        [InlineData("https://www.nuget.org/api/v2", true)]
        [InlineData("https://WWW.NUGET.ORG/api/v2", true)]
        [InlineData("http://api.nuget.org/v3/index.json", true)]
        [InlineData("http://API.NUGET.ORG/v3/index.json", true)]
        [InlineData("https://api.nuget.org/v3/index.json", true)]
        [InlineData("https://API.NUGET.ORG/v3/index.json", true)]
        [InlineData("http://notnuget.org/api/v2", false)]
        [InlineData("https://nuget.org.internal/v3/index.json", false)]
        public void IsNuGetOrg(string sourceUrl, bool expected)
        {
            // Arrange
            var source = new PackageSource(sourceUrl);

            // Act
            var actual = TelemetryUtility.IsNuGetOrg(source);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("https://tenant.pkgs.visualstudio.com/_packaging/feedname/nuget/v3/index.json", true)]
        [InlineData("https://pkgs.dev.azure.com/tenant/_packaging/feedname/nuget/v3/index.json", true)]
        [InlineData("https://mywebsite.azurewebsites.net/nuget/", false)]
        [InlineData("https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json", false)]
        public void IsAzureArtifacts(string sourceUrl, bool expected)
        {
            // Arrange
            var source = new PackageSource(sourceUrl);

            // Act
            var actual = TelemetryUtility.IsAzureArtifacts(source);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("https://nuget.pkg.github.com/dotnet/index.json", true)]
        [InlineData("https://nuget.pkg.github.com/nuget/index.json", true)]
        [InlineData("https://raw.githubusercontent.com/account/repo/branch/index.json", false)]
        public void IsGitHub(string sourceUrl, bool expected)
        {
            // Arrange
            var source = new PackageSource(sourceUrl);

            // Act
            var actual = TelemetryUtility.IsGitHub(source);

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}
