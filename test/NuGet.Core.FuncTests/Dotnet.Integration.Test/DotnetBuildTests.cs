// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet.Integration.Test
{
    [Collection(DotnetIntegrationCollection.Name)]
    public class DotnetBuildTests
    {
        private readonly DotnetIntegrationTestFixture _dotnetFixture;
        private readonly ITestOutputHelper _testOutputHelper;

        public DotnetBuildTests(DotnetIntegrationTestFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _dotnetFixture = fixture;
            _testOutputHelper = testOutputHelper;
        }

        [PlatformFact(Platform.Windows)]
        public void DotnetBuild_WithAssetTargetFallbackProjectReference_GlobalNoWarnSuppressesNU1702()
        {
            // Global $(NoWarn) with NU1702 suppresses the warning via MSBuild engine's
            // MSBuildWarningsAsMessages mechanism (Microsoft.Common.CurrentVersion.targets line ~669).
            using SimpleTestPathContext pathContext = _dotnetFixture.CreateSimpleTestPathContext();

            var projectFile = SetupAssetTargetFallbackProjectReference(pathContext,
                referringProjectProperties: new Dictionary<string, string> { { "NoWarn", "NU1702" } });

            // Act
            _dotnetFixture.RunDotnetExpectSuccess(pathContext.SolutionRoot, $"restore {projectFile}", testOutputHelper: _testOutputHelper);
            var buildResult = _dotnetFixture.RunDotnetExpectSuccess(pathContext.SolutionRoot, $"build {projectFile} --no-restore", testOutputHelper: _testOutputHelper);

            // Assert - NU1702 should be suppressed
            buildResult.AllOutput.Should().NotContain("NU1702");
        }

        [PlatformFact(Platform.Windows)]
        public void DotnetBuild_WithAssetTargetFallbackProjectReference_PerReferenceNoWarnSuppressesNU1702()
        {
            // Per-reference %(ProjectReference.NoWarn) suppresses NU1702 via MSBuild engine.
            // MSBuild propagates NoWarn metadata to MSBuildWarningsAsMessages through item batching.
            using SimpleTestPathContext pathContext = _dotnetFixture.CreateSimpleTestPathContext();

            var projectFile = SetupAssetTargetFallbackProjectReference(pathContext,
                projectReferenceMetadata: new Dictionary<string, string> { { "NoWarn", "NU1702" } });

            // Act
            _dotnetFixture.RunDotnetExpectSuccess(pathContext.SolutionRoot, $"restore {projectFile}", testOutputHelper: _testOutputHelper);
            var buildResult = _dotnetFixture.RunDotnetExpectSuccess(pathContext.SolutionRoot, $"build {projectFile} --no-restore", testOutputHelper: _testOutputHelper);

            // Assert - NU1702 is suppressed
            buildResult.AllOutput.Should().NotContain("NU1702");
        }

        [PlatformFact(Platform.Windows)]
        public void DotnetBuild_WithAssetTargetFallbackProjectReference_WarningsAsErrorsElevatesNU1702()
        {
            // $(WarningsAsErrors)=NU1702 elevates NU1702 to an error via MSBuild engine's
            // MSBuildWarningsAsErrors mechanism (Microsoft.Common.CurrentVersion.targets line ~670).
            using SimpleTestPathContext pathContext = _dotnetFixture.CreateSimpleTestPathContext();

            var projectFile = SetupAssetTargetFallbackProjectReference(pathContext,
                referringProjectProperties: new Dictionary<string, string> { { "WarningsAsErrors", "NU1702" } });

            // Act
            _dotnetFixture.RunDotnetExpectSuccess(pathContext.SolutionRoot, $"restore {projectFile}", testOutputHelper: _testOutputHelper);
            var buildResult = _dotnetFixture.RunDotnetExpectFailure(pathContext.SolutionRoot, $"build {projectFile} --no-restore", testOutputHelper: _testOutputHelper);

            // Assert - NU1702 is elevated to an error
            buildResult.AllOutput.Should().Contain("error NU1702");
        }

        [PlatformFact(Platform.Windows)]
        public void DotnetBuild_WithAssetTargetFallbackProjectReference_TreatWarningsAsErrorsElevatesNU1702()
        {
            // $(TreatWarningsAsErrors)=true should elevate NU1702 to an error.
            // This requires the MSBuild-side change to pass TreatWarningsAsErrors to the task.
            PatchSdkTargetsWithWarningProperties();
            using SimpleTestPathContext pathContext = _dotnetFixture.CreateSimpleTestPathContext();

            var projectFile = SetupAssetTargetFallbackProjectReference(pathContext,
                referringProjectProperties: new Dictionary<string, string>
                {
                    { "TreatWarningsAsErrors", "true" },
                });

            // Act
            _dotnetFixture.RunDotnetExpectSuccess(pathContext.SolutionRoot, $"restore {projectFile}", testOutputHelper: _testOutputHelper);
            var buildResult = _dotnetFixture.RunDotnetExpectFailure(pathContext.SolutionRoot, $"build {projectFile} --no-restore", testOutputHelper: _testOutputHelper);

            // Assert - NU1702 should be elevated to an error
            buildResult.AllOutput.Should().Contain("error NU1702");
        }

        [PlatformFact(Platform.Windows)]
        public void DotnetBuild_WithAssetTargetFallbackProjectReference_WarningsNotAsErrorsKeepsNU1702AsWarning()
        {
            // $(WarningsNotAsErrors)=NU1702 should prevent NU1702 from being elevated
            // even when $(TreatWarningsAsErrors)=true is set.
            // This requires the MSBuild-side change to pass both properties to the task.
            PatchSdkTargetsWithWarningProperties();
            using SimpleTestPathContext pathContext = _dotnetFixture.CreateSimpleTestPathContext();

            var projectFile = SetupAssetTargetFallbackProjectReference(pathContext,
                referringProjectProperties: new Dictionary<string, string>
                {
                    { "TreatWarningsAsErrors", "true" },
                    { "WarningsNotAsErrors", "NU1702" },
                });

            // Act
            _dotnetFixture.RunDotnetExpectSuccess(pathContext.SolutionRoot, $"restore {projectFile}", testOutputHelper: _testOutputHelper);
            var buildResult = _dotnetFixture.RunDotnetExpectSuccess(pathContext.SolutionRoot, $"build {projectFile} --no-restore", testOutputHelper: _testOutputHelper);

            // Assert - NU1702 should remain a warning, not elevated to error
            buildResult.AllOutput.Should().Contain("warning NU1702");
            buildResult.AllOutput.Should().NotContain("error NU1702");
        }

        private string SetupAssetTargetFallbackProjectReference(
            SimpleTestPathContext pathContext,
            Dictionary<string, string>? referringProjectProperties = null,
            Dictionary<string, string>? projectReferenceMetadata = null)
        {
            // Create referenced project targeting net472
            var project2Name = "Project2";
            var project2File = Path.Combine(pathContext.SolutionRoot, project2Name, $"{project2Name}.csproj");
            _dotnetFixture.CreateDotnetNewProject(pathContext.SolutionRoot, project2Name, " classlib", testOutputHelper: _testOutputHelper);
            using (var stream = File.Open(project2File, FileMode.Open, FileAccess.ReadWrite))
            {
                var xml = XDocument.Load(stream);
                ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "net472");
                ProjectFileUtils.WriteXmlToFile(xml, stream);
            }

            // Create referring project targeting net10.0 (ATF to net472 is implicit in SDK)
            var projectName = "ClassLibrary1";
            var projectFile = Path.Combine(pathContext.SolutionRoot, projectName, $"{projectName}.csproj");
            _dotnetFixture.CreateDotnetNewProject(pathContext.SolutionRoot, projectName, " classlib", testOutputHelper: _testOutputHelper);

            using (var stream = File.Open(projectFile, FileMode.Open, FileAccess.ReadWrite))
            {
                var xml = XDocument.Load(stream);
                ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "net10.0");

                if (referringProjectProperties != null)
                {
                    ProjectFileUtils.AddProperties(xml, referringProjectProperties);
                }

                ProjectFileUtils.AddItem(
                    xml,
                    "ProjectReference",
                    $"..\\{project2Name}\\{project2Name}.csproj",
                    string.Empty,
                    [],
                    projectReferenceMetadata ?? []);

                ProjectFileUtils.WriteXmlToFile(xml, stream);
            }

            return projectFile;
        }

        /// <summary>
        /// Patches the SDK's Microsoft.Common.CurrentVersion.targets to pass warning properties
        /// to GetReferenceNearestTargetFrameworkTask. This simulates the MSBuild-side change.
        /// The patch is idempotent — safe to call multiple times.
        /// </summary>
        private void PatchSdkTargetsWithWarningProperties()
        {
            string targetsFile = Path.Combine(_dotnetFixture.SdkDirectory.FullName, "Microsoft.Common.CurrentVersion.targets");
            string backupFile = targetsFile + ".original";

            // Restore from backup if a previous partial patch left the file in a bad state
            if (File.Exists(backupFile))
            {
                File.Copy(backupFile, targetsFile, overwrite: true);
            }

            string content = File.ReadAllText(targetsFile);

            // Already fully patched — NoWarn="$(NoWarn)" only appears after our patch
            if (content.Contains("NoWarn=\"$(NoWarn)\""))
            {
                return;
            }

            // Save backup before patching
            File.Copy(targetsFile, backupFile, overwrite: true);

            // Insert warning properties after every FallbackTargetFrameworks line in GetReferenceNearestTargetFrameworkTask invocations.
            string patched = content.Replace(
                "FallbackTargetFrameworks=\"$(AssetTargetFallback)\"",
                "FallbackTargetFrameworks=\"$(AssetTargetFallback)\"\n" +
                "                                            TreatWarningsAsErrors=\"$(TreatWarningsAsErrors)\"\n" +
                "                                            WarningsAsErrors=\"$(WarningsAsErrors)\"\n" +
                "                                            WarningsNotAsErrors=\"$(WarningsNotAsErrors)\"\n" +
                "                                            NoWarn=\"$(NoWarn)\"");

            if (patched == content)
            {
                throw new InvalidOperationException("Failed to patch Microsoft.Common.CurrentVersion.targets — FallbackTargetFrameworks attribute not found.");
            }

            File.WriteAllText(targetsFile, patched);
        }
    }
}
