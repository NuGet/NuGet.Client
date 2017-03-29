using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
