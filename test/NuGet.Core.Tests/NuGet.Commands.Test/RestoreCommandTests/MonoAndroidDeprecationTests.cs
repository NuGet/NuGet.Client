// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test.RestoreCommandTests
{
    public class MonoAndroidDeprecationTests
    {
        #region ShouldCheck tests

        [Fact]
        public void ShouldCheck_Net11Android_SdkLevel11_ReturnsTrue()
        {
            // Arrange
            var spec = CreatePackageSpec("net11.0-android35.0");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;
            var framework = NuGetFramework.Parse("net11.0-android35.0");

            // Act & Assert
            MonoAndroidDeprecation.ShouldCheck(spec, framework).Should().BeTrue();
        }

        [Fact]
        public void ShouldCheck_Net12Android_SdkLevel11_ReturnsTrue()
        {
            // Arrange
            var spec = CreatePackageSpec("net12.0-android35.0");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;
            var framework = NuGetFramework.Parse("net12.0-android35.0");

            // Act & Assert
            MonoAndroidDeprecation.ShouldCheck(spec, framework).Should().BeTrue();
        }

        [Fact]
        public void ShouldCheck_Net10Android_SdkLevel11_ReturnsFalse()
        {
            // net10.0-android has version major 10, which is < 11
            var spec = CreatePackageSpec("net10.0-android35.0");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;
            var framework = NuGetFramework.Parse("net10.0-android35.0");

            MonoAndroidDeprecation.ShouldCheck(spec, framework).Should().BeFalse();
        }

        [Fact]
        public void ShouldCheck_Net11Android_SdkLevel10_ReturnsFalse()
        {
            // SDK analysis level 10.0.100 is too old
            var spec = CreatePackageSpec("net11.0-android35.0");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("10.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;
            var framework = NuGetFramework.Parse("net11.0-android35.0");

            MonoAndroidDeprecation.ShouldCheck(spec, framework).Should().BeFalse();
        }

        [Fact]
        public void ShouldCheck_Net11iOS_SdkLevel11_ReturnsFalse()
        {
            // iOS platform, not android
            var spec = CreatePackageSpec("net11.0-ios18.0");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;
            var framework = NuGetFramework.Parse("net11.0-ios18.0");

            MonoAndroidDeprecation.ShouldCheck(spec, framework).Should().BeFalse();
        }

        [Fact]
        public void ShouldCheck_Net11_NoPlatform_SdkLevel11_ReturnsFalse()
        {
            // net11.0 without platform
            var spec = CreatePackageSpec("net11.0");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;
            var framework = NuGetFramework.Parse("net11.0");

            MonoAndroidDeprecation.ShouldCheck(spec, framework).Should().BeFalse();
        }

        [Fact]
        public void ShouldCheck_NullRestoreMetadata_ReturnsFalse()
        {
            var spec = new PackageSpec();
            spec.RestoreMetadata = null;
            var framework = NuGetFramework.Parse("net11.0-android35.0");

            MonoAndroidDeprecation.ShouldCheck(spec, framework).Should().BeFalse();
        }

        [Fact]
        public void ShouldCheck_NullSdkAnalysisLevel_UsingMicrosoftNETSdk_ReturnsFalse()
        {
            // When SdkAnalysisLevel is null and UsingMicrosoftNETSdk is true, IsEnabled returns false
            var spec = CreatePackageSpec("net11.0-android35.0");
            spec.RestoreMetadata.SdkAnalysisLevel = null;
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;
            var framework = NuGetFramework.Parse("net11.0-android35.0");

            MonoAndroidDeprecation.ShouldCheck(spec, framework).Should().BeFalse();
        }

        [Fact]
        public void ShouldCheck_NullSdkAnalysisLevel_NotUsingMicrosoftNETSdk_ReturnsTrue()
        {
            // When SdkAnalysisLevel is null and UsingMicrosoftNETSdk is false, IsEnabled returns true
            // but the framework check should still apply
            var spec = CreatePackageSpec("net11.0-android35.0");
            spec.RestoreMetadata.SdkAnalysisLevel = null;
            spec.RestoreMetadata.UsingMicrosoftNETSdk = false;
            var framework = NuGetFramework.Parse("net11.0-android35.0");

            MonoAndroidDeprecation.ShouldCheck(spec, framework).Should().BeTrue();
        }

        [Fact]
        public void ShouldCheck_Net6Android_SdkLevel11_ReturnsFalse()
        {
            // net6.0-android has version major 6, which is < 11
            var spec = CreatePackageSpec("net6.0-android31.0");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;
            var framework = NuGetFramework.Parse("net6.0-android31.0");

            MonoAndroidDeprecation.ShouldCheck(spec, framework).Should().BeFalse();
        }

        #endregion

        #region IsMonoAndroidFramework tests

        [Fact]
        public void IsMonoAndroidFramework_MonoAndroid_ReturnsTrue()
        {
            var framework = NuGetFramework.Parse("monoandroid10.0");

            MonoAndroidDeprecation.IsMonoAndroidFramework(framework).Should().BeTrue();
        }

        [Fact]
        public void IsMonoAndroidFramework_MonoAndroidNoVersion_ReturnsTrue()
        {
            var framework = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.MonoAndroid);

            MonoAndroidDeprecation.IsMonoAndroidFramework(framework).Should().BeTrue();
        }

        [Fact]
        public void IsMonoAndroidFramework_NetCoreApp_ReturnsFalse()
        {
            var framework = NuGetFramework.Parse("net6.0-android31.0");

            MonoAndroidDeprecation.IsMonoAndroidFramework(framework).Should().BeFalse();
        }

        [Fact]
        public void IsMonoAndroidFramework_NetStandard_ReturnsFalse()
        {
            var framework = NuGetFramework.Parse("netstandard2.0");

            MonoAndroidDeprecation.IsMonoAndroidFramework(framework).Should().BeFalse();
        }

        [Fact]
        public void IsMonoAndroidFramework_Null_ReturnsFalse()
        {
            MonoAndroidDeprecation.IsMonoAndroidFramework(null).Should().BeFalse();
        }

        #endregion

        #region Integration tests

        [Fact]
        public async Task Restore_Net11Android_MonoAndroidPackage_SdkLevel11_EmitsNU1703()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            var packageA = new SimpleTestPackageContext("a", "1.0.0");
            packageA.AddFile("lib/monoandroid10.0/a.dll");

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA);

            var spec = ProjectTestHelpers.GetPackageSpec("Project1",
                pathContext.SolutionRoot,
                framework: "net11.0-android35.0",
                dependencyName: "a");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;

            var logger = new TestLogger();
            var command = new RestoreCommand(ProjectTestHelpers.CreateRestoreRequest(pathContext, logger, spec));

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue(because: logger.ShowMessages());
            result.LockFile.LogMessages.Should().HaveCount(1);
            result.LockFile.LogMessages[0].Code.Should().Be(NuGetLogCode.NU1703);
            result.LockFile.LogMessages[0].Level.Should().Be(LogLevel.Warning);
            result.LockFile.LogMessages[0].LibraryId.Should().Be("a");
            result.LockFile.LogMessages[0].Message.Should().Contain("MonoAndroid");
            logger.Errors.Should().Be(0);
            logger.Warnings.Should().Be(1);
        }

        [Fact]
        public async Task Restore_Net10Android_MonoAndroidPackage_SdkLevel11_DoesNotEmitNU1703()
        {
            // net10.0-android has version major 10, below the 11 threshold
            using var pathContext = new SimpleTestPathContext();

            var packageA = new SimpleTestPackageContext("a", "1.0.0");
            packageA.AddFile("lib/monoandroid10.0/a.dll");

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA);

            var spec = ProjectTestHelpers.GetPackageSpec("Project1",
                pathContext.SolutionRoot,
                framework: "net10.0-android35.0",
                dependencyName: "a");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;

            var logger = new TestLogger();
            var command = new RestoreCommand(ProjectTestHelpers.CreateRestoreRequest(pathContext, logger, spec));

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue(because: logger.ShowMessages());
            result.LockFile.LogMessages.Should().NotContain(m => m.Code == NuGetLogCode.NU1703);
        }

        [Fact]
        public async Task Restore_Net11Android_MonoAndroidPackage_SdkLevel10_DoesNotEmitNU1703()
        {
            // SDK analysis level too old
            using var pathContext = new SimpleTestPathContext();

            var packageA = new SimpleTestPackageContext("a", "1.0.0");
            packageA.AddFile("lib/monoandroid10.0/a.dll");

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA);

            var spec = ProjectTestHelpers.GetPackageSpec("Project1",
                pathContext.SolutionRoot,
                framework: "net11.0-android35.0",
                dependencyName: "a");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("10.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;

            var logger = new TestLogger();
            var command = new RestoreCommand(ProjectTestHelpers.CreateRestoreRequest(pathContext, logger, spec));

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue(because: logger.ShowMessages());
            result.LockFile.LogMessages.Should().NotContain(m => m.Code == NuGetLogCode.NU1703);
        }

        [Fact]
        public async Task Restore_Net11Android_NetAndroidPackage_SdkLevel11_DoesNotEmitNU1703()
        {
            // Package uses net6.0-android, not monoandroid - should not warn
            using var pathContext = new SimpleTestPathContext();

            var packageA = new SimpleTestPackageContext("a", "1.0.0");
            packageA.AddFile("lib/net6.0-android31.0/a.dll");

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA);

            var spec = ProjectTestHelpers.GetPackageSpec("Project1",
                pathContext.SolutionRoot,
                framework: "net11.0-android35.0",
                dependencyName: "a");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;

            var logger = new TestLogger();
            var command = new RestoreCommand(ProjectTestHelpers.CreateRestoreRequest(pathContext, logger, spec));

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue(because: logger.ShowMessages());
            result.LockFile.LogMessages.Should().NotContain(m => m.Code == NuGetLogCode.NU1703);
        }

        [Fact]
        public async Task Restore_Net11iOS_MonoAndroidPackage_SdkLevel11_DoesNotEmitNU1703()
        {
            // iOS project, not android - should not warn even with monoandroid package
            using var pathContext = new SimpleTestPathContext();

            var packageA = new SimpleTestPackageContext("a", "1.0.0");
            packageA.AddFile("lib/monoandroid10.0/a.dll");
            // Also add netstandard so the package resolves for iOS
            packageA.AddFile("lib/netstandard2.0/a.dll");

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA);

            var spec = ProjectTestHelpers.GetPackageSpec("Project1",
                pathContext.SolutionRoot,
                framework: "net11.0-ios18.0",
                dependencyName: "a");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;

            var logger = new TestLogger();
            var command = new RestoreCommand(ProjectTestHelpers.CreateRestoreRequest(pathContext, logger, spec));

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue(because: logger.ShowMessages());
            result.LockFile.LogMessages.Should().NotContain(m => m.Code == NuGetLogCode.NU1703);
        }

        [Fact]
        public async Task Restore_Net11Android_MonoAndroidPackage_SdkLevel11_NU1703_CanBeSuppressed()
        {
            // NoWarn for NU1703 should suppress the warning
            using var pathContext = new SimpleTestPathContext();

            var packageA = new SimpleTestPackageContext("a", "1.0.0");
            packageA.AddFile("lib/monoandroid10.0/a.dll");

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA);

            var spec = ProjectTestHelpers.GetPackageSpec("Project1",
                pathContext.SolutionRoot,
                framework: "net11.0-android35.0",
                dependencyName: "a");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;
            spec.RestoreMetadata.ProjectWideWarningProperties = new WarningProperties(
                warningsAsErrors: new System.Collections.Generic.HashSet<NuGetLogCode>(),
                noWarn: new System.Collections.Generic.HashSet<NuGetLogCode> { NuGetLogCode.NU1703 },
                allWarningsAsErrors: false,
                warningsNotAsErrors: new System.Collections.Generic.HashSet<NuGetLogCode>());

            var logger = new TestLogger();
            var command = new RestoreCommand(ProjectTestHelpers.CreateRestoreRequest(pathContext, logger, spec));

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue(because: logger.ShowMessages());
            result.LockFile.LogMessages.Should().NotContain(m => m.Code == NuGetLogCode.NU1703);
            logger.Warnings.Should().Be(0);
        }

        [Fact]
        public async Task Restore_Net11Android_MultiplePackages_OnlyMonoAndroidPackageGetsNU1703()
        {
            // One package with monoandroid, one with netstandard - only monoandroid package should warn
            using var pathContext = new SimpleTestPathContext();

            var packageA = new SimpleTestPackageContext("a", "1.0.0");
            packageA.AddFile("lib/monoandroid10.0/a.dll");

            var packageB = new SimpleTestPackageContext("b", "2.0.0");
            packageB.AddFile("lib/netstandard2.0/b.dll");

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA,
                packageB);

            var spec = ProjectTestHelpers.GetPackageSpec("Project1",
                pathContext.SolutionRoot,
                framework: "net11.0-android35.0",
                dependencyName: "a");
            // Add second dependency
            var tfi = spec.TargetFrameworks[0];
            tfi.Dependencies.Add(new LibraryModel.LibraryDependency
            {
                LibraryRange = new LibraryModel.LibraryRange("b", VersionRange.Parse("2.0.0"), LibraryModel.LibraryDependencyTarget.Package)
            });

            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;

            var logger = new TestLogger();
            var command = new RestoreCommand(ProjectTestHelpers.CreateRestoreRequest(pathContext, logger, spec));

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue(because: logger.ShowMessages());
            result.LockFile.LogMessages.Where(m => m.Code == NuGetLogCode.NU1703).Should().HaveCount(1);
            result.LockFile.LogMessages.Single(m => m.Code == NuGetLogCode.NU1703).LibraryId.Should().Be("a");
            logger.Warnings.Should().Be(1);
        }

        [Fact]
        public async Task Restore_Net11Android_MonoAndroidPackage_SdkLevel11_WarningMessageFormat()
        {
            // Verify the exact warning message format
            using var pathContext = new SimpleTestPathContext();

            var packageA = new SimpleTestPackageContext("MyPackage", "3.2.1");
            packageA.AddFile("lib/monoandroid10.0/MyPackage.dll");

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA);

            var spec = ProjectTestHelpers.GetPackageSpec("Project1",
                pathContext.SolutionRoot,
                framework: "net11.0-android35.0",
                dependencyName: "MyPackage",
                dependencyVersion: "3.2.1");
            spec.RestoreMetadata.SdkAnalysisLevel = NuGetVersion.Parse("11.0.100");
            spec.RestoreMetadata.UsingMicrosoftNETSdk = true;

            var logger = new TestLogger();
            var command = new RestoreCommand(ProjectTestHelpers.CreateRestoreRequest(pathContext, logger, spec));

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue(because: logger.ShowMessages());
            var logMessage = result.LockFile.LogMessages.Single(m => m.Code == NuGetLogCode.NU1703);
            var expectedMessage = string.Format(CultureInfo.CurrentCulture,
                Strings.Warning_MonoAndroidFrameworkDeprecated,
                "MyPackage",
                "3.2.1");
            logMessage.Message.Should().Be(expectedMessage);
        }

        #endregion

        #region Helpers

        private static PackageSpec CreatePackageSpec(string framework)
        {
            var spec = ProjectTestHelpers.GetPackageSpec("TestProject", @"C:\", framework);
            return spec;
        }

        #endregion
    }
}
