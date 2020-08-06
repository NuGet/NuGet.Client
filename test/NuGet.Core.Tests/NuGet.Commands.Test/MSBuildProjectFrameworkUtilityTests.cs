// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.Commands.Test
{
    public class MSBuildProjectFrameworkUtilityTests
    {
        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyTargetFramworksParsed()
        {
            // Arrange & Act
            var frameworks = GetFrameworks(
                projectFilePath: "/tmp/project.csproj",
                targetFrameworks: "net45;netcoreapp1.1");

            // Assert
            Assert.Equal(2, frameworks.Count);
            Assert.Equal(NuGetFramework.Parse("net45"), frameworks[0]);
            Assert.Equal(NuGetFramework.Parse("netcoreapp1.1"), frameworks[1]);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyTargetFramworkParsed()
        {
            // Arrange & Act
            var frameworks = GetFrameworks(
                projectFilePath: "/tmp/project.csproj",
                targetFrameworks: "net45");

            // Assert
            Assert.Equal(1, frameworks.Count);
            Assert.Equal(NuGetFramework.Parse("net45"), frameworks[0]);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyTargetFramworksUsedInsteadOfTargetFramework()
        {
            // Arrange & Act
            var frameworks = GetFrameworks(
                projectFilePath: "/tmp/project.csproj",
                targetFrameworks: "net45;netcoreapp1.1",
                targetFramework: "net46");

            // Assert
            Assert.Equal(2, frameworks.Count);
            Assert.Equal(NuGetFramework.Parse("net45"), frameworks[0]);
            Assert.Equal(NuGetFramework.Parse("netcoreapp1.1"), frameworks[1]);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyTargetFrameworkMonikerUsed()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.csproj",
                targetFrameworkMoniker: ".NETFramework,Version=v4.5")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.Parse("net45"), framework);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyUAPFullVersion()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.csproj",
                targetPlatformIdentifier: "UAP",
                targetPlatformVersion: "10.0.1.2")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.Parse("UAP10.0.1.2"), framework);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyPlatformIgnoredWithoutVersion()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.csproj",
                targetPlatformIdentifier: "UAP",
                targetFrameworkMoniker: ".NETFramework,Version=v4.5")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.Parse("net45"), framework);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyUAPFullVersionForJavascript()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.jsproj",
                targetPlatformIdentifier: "UAP",
                targetPlatformVersion: "10.0.1.2")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.Parse("UAP10.0.1.2"), framework);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyJavascriptDefaultsToWindows()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.jsproj")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.Parse("win0.0"), framework);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyNETCore45ToWin8()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.csproj",
                targetFrameworkMoniker: ".NETCore,Version=v4.5")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.Parse("win8"), framework);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyNETCore451ToWin81()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.csproj",
                targetFrameworkMoniker: ".NETCore,Version=v4.5.1")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.Parse("win81"), framework);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyNETCore50IsUnchanged()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.csproj",
                targetFrameworkMoniker: ".NETCore,Version=v5.0")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.Parse("netcore5.0"), framework);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_IsXnaWindowsPhoneProjectVerifyReplacement()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.csproj",
                isXnaWindowsPhoneProject: true,
                targetFrameworkMoniker: ".NETFramework,Version=v4.0")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.Parse("Silverlight,Version=v4.0,Profile=WindowsPhone71"), framework);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_IsManagementPackProject()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.csproj",
                isManagementPackProject: true,
                targetFrameworkMoniker: ".NETFramework,Version=v4.0")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.Parse("SCMPInfra, Version=0.0"), framework);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyNativeProjectOverMoniker()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.vcxproj",
                targetFrameworkMoniker: ".NETFramework,Version=v4.0")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.Parse("native"), framework);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyUnknownMonikerIsParsed()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.csproj",
                targetFrameworkMoniker: "Blah,Version=v4.0")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.Parse("Blah,Version=v4.0"), framework);
        }

        [Fact]
        public void MSBuildProjectFrameworkUtility_VerifyFallbackToUnsupported()
        {
            // Arrange & Act
            var framework = GetFrameworks(
                projectFilePath: "/tmp/project.csproj")
                .SingleOrDefault();

            // Assert
            Assert.Equal(NuGetFramework.UnsupportedFramework, framework);
        }

        [Theory]
        [InlineData(".NETFramework,Version=v.4.5", ".NETFramework", "v4.5", "", "", "", "", "net45")]
        [InlineData(null, ".NETFramework", "v4.5", "", "", "", "", "net45")]
        [InlineData(".NETFramework,Version=v.4.5", ".NETFramework", "v4.5", "client", "", "", "", "net45-client")]
        [InlineData(".NETCoreApp,Version=v.5.0", ".NETCoreApp", "v5.0", "", "android", "10", "", "net5.0-android10.0")]
        [InlineData(".NETCoreApp,Version=v.5.0", ".NETCoreApp", "v5.0", "", "ios", "21.0", "", "net5.0-ios21.0")]
        [InlineData(null, ".NETCoreApp", "v6.0", "", "ios", "21.0", "", "net6.0-ios21.0")]
        [InlineData(null, ".NETCoreApp", "v6.0", "", "ios", "v21.0", "", "net6.0-ios21.0")]
        [InlineData(null, null, null, "", "ios", "v21.0", "", "unsupported")]
        [InlineData(null, ".NETCoreApp", "v6.0", "", "UAP", "10.0.1.2", "", "uap10.0.1.2")]
        [InlineData(null, ".NETCoreApp", "v6.0", "", "UAP", "10.0.1.2", "10.0.1.3", "uap10.0.1.3")]
        [InlineData(".NETCoreApp,Version=v3.0", ".NETCoreApp", "v3.0", "", "android", "10", "", "netcoreapp3.0")]
        public void GetProjectFramework_WithCanonicalProperties_Succeeds(
                string targetFrameworkMoniker,
                string targetFrameworkIdentifier,
                string targetFrameworkVersion,
                string targetFrameworkProfile,
                string targetPlatformIdentifier,
                string targetPlatformVersion,
                string targetPlatformMinVersion,
                string expectedShortName)
        {
            var nugetFramework = MSBuildProjectFrameworkUtility.GetProjectFramework(

                projectFilePath: @"C:\csproj",
                targetFrameworkMoniker,

                targetFrameworkIdentifier,
                targetFrameworkVersion,
                targetFrameworkProfile,
                targetPlatformIdentifier,
                targetPlatformVersion,
                targetPlatformMinVersion);

            Assert.Equal(expectedShortName, nugetFramework.GetShortFolderName());
        }

        [Theory]
        [InlineData(null, ".NETCoreApp", "v6.0", "", "ios", "5.0-preview.3", "")]
        [InlineData(null, ".NETCoreApp", "v6.0-preview.3", "", "ios", "5.0", "")]
        [InlineData(".NETCoreApp,Version=v.5.0", ".NETCoreApp", "v5.0", "NET50CannotHaveProfiles", "android", "10", "")]
        public void GetProjectFramework_WithInvalidInput_Throws(
        string targetFrameworkMoniker,
        string targetFrameworkIdentifier,
        string targetFrameworkVersion,
        string targetFrameworkProfile,
        string targetPlatformIdentifier,
        string targetPlatformVersion,
        string targetPlatformMinVersion)
        {
            Assert.ThrowsAny<Exception>(() => MSBuildProjectFrameworkUtility.GetProjectFramework(
               projectFilePath: @"C:\csproj",
               targetFrameworkMoniker,
               targetFrameworkIdentifier,
               targetFrameworkVersion,
               targetFrameworkProfile,
               targetPlatformIdentifier,
               targetPlatformVersion,
               targetPlatformMinVersion));
        }


        /// <summary>
        /// Test helper
        /// </summary>
        private static List<NuGetFramework> GetFrameworks(
            string projectFilePath,
            string targetFrameworks = "",
            string targetFramework = "",
            string targetFrameworkMoniker = "",
            string targetPlatformIdentifier = "",
            string targetPlatformVersion="",
            string targetPlatformMinVersion = "",
            bool isXnaWindowsPhoneProject=false,
            bool isManagementPackProject=false)
        {
            return MSBuildProjectFrameworkUtility.GetProjectFrameworks(
                MSBuildProjectFrameworkUtility.GetProjectFrameworkStrings(
                    projectFilePath,
                    targetFrameworks,
                    targetFramework,
                    targetFrameworkMoniker,
                    targetPlatformIdentifier,
                    targetPlatformVersion,
                    targetPlatformMinVersion,
                    isXnaWindowsPhoneProject,
                    isManagementPackProject))
                    .ToList();
        }
    }
}
