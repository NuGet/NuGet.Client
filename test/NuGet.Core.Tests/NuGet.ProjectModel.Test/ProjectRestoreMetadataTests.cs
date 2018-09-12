// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class ProjectRestoreMetadataTests
    {
        [Fact]
        public void Equals_WithSameObject_ReturnsTrue()
        {
            // Arrange
            var metadata = CreateProjectRestoreMetadata();

            // Act & Assert
            metadata.Equals(metadata).Should().BeTrue();
        }

        [Fact]
        public void Equals_WithNullObject_ReturnsFalse()
        {
            // Arrange
            var metadata = CreateProjectRestoreMetadata();

            // Act & Assert
            metadata.Equals(null).Should().BeFalse();
        }

        [Fact]
        public void Equals_WithSameCaseInPaths_ReturnsTrue()
        {
            // Arrange
            var metadata1 = CreateProjectRestoreMetadata();
            var metadata2 = metadata1.Clone();

            metadata2.ProjectPath = "ProjectPath";
            metadata2.ProjectJsonPath = "ProjectJsonPath";
            metadata2.OutputPath = "OutputPath";
            metadata2.ProjectName = "ProjectName";
            metadata2.ProjectUniqueName = "ProjectUniqueName";
            metadata2.PackagesPath = "PackagesPath";
            metadata2.ConfigFilePaths = new List<string>() { "config1" };
            metadata2.FallbackFolders = new List<string>() { "fallback1" };

            // Act & Assert
            metadata1.Equals(metadata2).Should().BeTrue();
        }

        [PlatformFact(Platform.Windows, Platform.Darwin)]
        public void Equals_WithDifferentCaseInPathsOnWindowsAndMac_ReturnsTrue()
        {
            // Arrange
            var metadata1 = CreateProjectRestoreMetadata();
            var metadata2 = metadata1.Clone();

            metadata2.ProjectPath = "projectPath";
            metadata2.ProjectJsonPath = "projectJsonPath";
            metadata2.OutputPath = "outputPath";
            metadata2.ProjectName = "projectName";
            metadata2.ProjectUniqueName = "projectUniqueName";
            metadata2.PackagesPath = "packagesPath";
            metadata2.ConfigFilePaths = new List<string>() { "Config1" };
            metadata2.FallbackFolders = new List<string>() { "Fallback1" };

            // Act & Assert
            metadata1.Equals(metadata2).Should().BeTrue();
        }

        [PlatformFact(Platform.Linux)]
        public void Equals_WithDifferentCaseInPathsOnLinux_ReturnsFalse()
        {
            // Arrange
            var metadata1 = CreateProjectRestoreMetadata();
            var metadata2 = metadata1.Clone();

            metadata2.ProjectPath = "projectPath";
            metadata2.ProjectJsonPath = "projectJsonPath";
            metadata2.OutputPath = "outputPath";
            metadata2.ProjectName = "projectName";
            metadata2.ProjectUniqueName = "projectUniqueName";
            metadata2.PackagesPath = "packagesPath";
            metadata2.ConfigFilePaths = new List<string>() { "Config1" };
            metadata2.FallbackFolders = new List<string>() { "Fallback1" };

            // Act & Assert
            metadata1.Equals(metadata2).Should().BeFalse();
        }

        [Fact]
        public void Equals_WithDifferentRestoreLockProperties_ReturnsFalse()
        {
            // Arrange
            var metadata1 = CreateProjectRestoreMetadata();
            var metadata2 = metadata1.Clone();
            var metadata3 = metadata1.Clone();
            var metadata4 = metadata1.Clone();

            metadata2.RestoreLockProperties = new RestoreLockProperties("false", null, false);
            metadata3.RestoreLockProperties = new RestoreLockProperties("true", "tempPath", false);
            metadata4.RestoreLockProperties = new RestoreLockProperties("true", null, true);

            // Act & Assert
            metadata1.Equals(metadata2).Should().BeFalse();
            metadata1.Equals(metadata3).Should().BeFalse();
            metadata1.Equals(metadata4).Should().BeFalse();
        }

        private ProjectRestoreMetadata CreateProjectRestoreMetadata()
        {
            var projectReference = new ProjectRestoreReference
            {
                ProjectPath = "Path",
                ProjectUniqueName = "ProjectUniqueName",
                IncludeAssets = LibraryIncludeFlags.All,
                ExcludeAssets = LibraryIncludeFlags.Analyzers,
                PrivateAssets = LibraryIncludeFlags.Build
            };

            var nugetFramework = NuGetFramework.Parse("net461");
            var originalPRMFI = new ProjectRestoreMetadataFrameworkInfo(nugetFramework)
            {
                ProjectReferences = new List<ProjectRestoreReference>() { projectReference }
            };

            var targetframeworks = new List<ProjectRestoreMetadataFrameworkInfo>() { originalPRMFI };
            var allWarningsAsErrors = true;
            var noWarn = new HashSet<NuGetLogCode>() { NuGetLogCode.NU1000, NuGetLogCode.NU1500 };
            var warningsAsErrors = new HashSet<NuGetLogCode>() { NuGetLogCode.NU1001, NuGetLogCode.NU1501 };
            var warningProperties = new WarningProperties(allWarningsAsErrors: allWarningsAsErrors, warningsAsErrors: warningsAsErrors, noWarn: noWarn);
            var restoreLockProperties = new RestoreLockProperties(restorePackagesWithLockFile: "true", nuGetLockFilePath: null, restoreLockedMode: false);
            var originalProjectRestoreMetadata = new ProjectRestoreMetadata
            {
                ProjectStyle = ProjectStyle.PackageReference,
                ProjectPath = "ProjectPath",
                ProjectJsonPath = "ProjectJsonPath",
                OutputPath = "OutputPath",
                ProjectName = "ProjectName",
                ProjectUniqueName = "ProjectUniqueName",
                PackagesPath = "PackagesPath",
                CacheFilePath = "CacheFilePath",
                CrossTargeting = true,
                LegacyPackagesDirectory = true,
                ValidateRuntimeAssets = true,
                SkipContentFileWrite = true,
                TargetFrameworks = targetframeworks,
                Sources = new List<PackageSource>() { new PackageSource("http://api.nuget.org/v3/index.json") },
                FallbackFolders = new List<string>() { "fallback1" },
                ConfigFilePaths = new List<string>() { "config1" },
                OriginalTargetFrameworks = new List<string>() { "net45" },
                Files = new List<ProjectRestoreMetadataFile>() { new ProjectRestoreMetadataFile("packagePath", "absolutePath") },
                ProjectWideWarningProperties = warningProperties,
                RestoreLockProperties = restoreLockProperties
            };

            return originalProjectRestoreMetadata;
        }
    }
}
