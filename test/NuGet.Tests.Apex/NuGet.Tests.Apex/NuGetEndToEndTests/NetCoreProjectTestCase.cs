using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.PackageManagement;

namespace NuGet.Tests.Apex
{
    [TestClass]
    public class NetCoreProjectTestCase : SharedVisualStudioHostTestClass
    {
        // basic create for .net core template
        [DataTestMethod]
        [DynamicData(nameof(GetNetCoreTemplates), DynamicDataSourceType.Method)]
        [Timeout(DefaultTimeout)]
        public void CreateNetCoreProject_RestoresNewProject(ProjectTemplate projectTemplate)
        {
            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger, addNetStandardFeeds: true))
            {
                VisualStudio.AssertNoErrors();
            }
        }

        // basic create for .net core template
        [DataTestMethod]
        [DynamicData(nameof(GetNetCoreTemplates), DynamicDataSourceType.Method)]
        [Timeout(DefaultTimeout)]
        public void CreateNetCoreProject_AddProjectReference(ProjectTemplate projectTemplate)
        {
            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger, addNetStandardFeeds: true))
            {
                var project2 = testContext.SolutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, CommonUtility.DefaultTargetFramework, "TestProject2");
                project2.Build();

                testContext.Project.References.Dte.AddProjectReference(project2);
                testContext.SolutionService.SaveAll();

                testContext.SolutionService.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                VisualStudio.AssertNoErrors();
                CommonUtility.AssertPackageInAssetsFile(VisualStudio, testContext.Project, "TestProject2", "1.0.0", Logger);
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(GetNetCoreTemplates), DynamicDataSourceType.Method)]
        [Timeout(DefaultTimeout)]
        public async Task WithSourceMappingEnabled_InstallPackageFromPMUIFromExpectedSource_Succeeds(ProjectTemplate projectTemplate)
        {
            // Arrange
            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger, addNetStandardFeeds: true))
            {
                var privateRepositoryPath = Path.Combine(testContext.SolutionRoot, "PrivateRepository");
                Directory.CreateDirectory(privateRepositoryPath);
                var externalRepositoryPath = Path.Combine(testContext.SolutionRoot, "ExternalRepository");
                Directory.CreateDirectory(externalRepositoryPath);

                var packageName = "Contoso.a";
                var packageVersion = "1.0.0";

                await CommonUtility.CreatePackageInSourceAsync(privateRepositoryPath, packageName, packageVersion);
                await CommonUtility.CreatePackageInSourceAsync(externalRepositoryPath, packageName, packageVersion);


                // Create nuget.config with Package source mapping filtering rules before project is created.
                CommonUtility.CreateConfigurationFile(Path.Combine(testContext.SolutionRoot, "NuGet.Config"), $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""ExternalRepository"" value=""{externalRepositoryPath}"" />
        <add key=""PrivateRepository"" value=""{privateRepositoryPath}"" />
    </packageSources>
    <packageSourceMapping>
        <packageSource key=""externalRepository"">
            <package pattern=""External.*"" />
            <package pattern=""Others.*"" />
        </packageSource>
        <packageSource key=""PrivateRepository"">
            <package pattern=""contoso.*"" />
            <package pattern=""Test.*"" />
        </packageSource>
        <packageSource key=""nuget"">
            <package pattern=""Microsoft.*"" />
            <package pattern=""NetStandard*"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");


                VisualStudio.AssertNoErrors();

                // Act
                CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, Logger);
                var nugetTestService = GetNuGetTestService();
                var uiwindow = nugetTestService.GetUIWindowfromProject(testContext.SolutionService.Projects[0]);

                // The Install action will automatically create a package source mapping to the selected package source if it's missing,
                // so select the source which already has a mapping.
                uiwindow.SetPackageSourceOptionToSource("PrivateRepository");
                uiwindow.InstallPackageFromUI(packageName, packageVersion);

                // Assert
                VisualStudio.AssertNuGetOutputDoesNotHaveErrors();
                CommonUtility.AssertPackageReferenceExists(testContext.SolutionService.Projects[0], packageName, packageVersion, Logger);
                StringAssert.Contains(GetPackageManagerOutputWindowPaneText(), $"Installed {packageName} {packageVersion} from {privateRepositoryPath}");
            }
        }

        [Ignore("https://github.com/NuGet/Home/issues/12898")]
        [DataTestMethod]
        [DynamicData(nameof(GetNetCoreTemplates), DynamicDataSourceType.Method)]
        [Timeout(DefaultTimeout)]
        public async Task WithSourceMappingEnabled_InstallAndUpdatePackageFromPMUIFromExpectedSource_Succeeds(ProjectTemplate projectTemplate)
        {
            // Arrange
            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger, addNetStandardFeeds: true))
            {
                var privateRepositoryPath = Path.Combine(testContext.SolutionRoot, "PrivateRepository");
                Directory.CreateDirectory(privateRepositoryPath);
                var externalRepositoryPath = Path.Combine(testContext.SolutionRoot, "ExternalRepository");
                Directory.CreateDirectory(externalRepositoryPath);

                var packageName = "Contoso.a";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";

                await CommonUtility.CreatePackageInSourceAsync(privateRepositoryPath, packageName, packageVersion1);
                await CommonUtility.CreatePackageInSourceAsync(externalRepositoryPath, packageName, packageVersion1);

                await CommonUtility.CreatePackageInSourceAsync(privateRepositoryPath, packageName, packageVersion2);
                await CommonUtility.CreatePackageInSourceAsync(externalRepositoryPath, packageName, packageVersion2);

                // Create nuget.config with Package source mapping filtering rules before project is created.
                CommonUtility.CreateConfigurationFile(Path.Combine(testContext.SolutionRoot, "NuGet.Config"), $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""ExternalRepository"" value=""{externalRepositoryPath}"" />
        <add key=""PrivateRepository"" value=""{privateRepositoryPath}"" />
    </packageSources>
    <packageSourceMapping>
        <packageSource key=""externalRepository"">
            <package pattern=""External.*"" />
            <package pattern=""Others.*"" />
        </packageSource>
        <packageSource key=""PrivateRepository"">
            <package pattern=""contoso.*"" />
            <package pattern=""Test.*"" />
        </packageSource>
        <packageSource key=""nuget"">
            <package pattern=""Microsoft.*"" />
            <package pattern=""NetStandard*"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");


                VisualStudio.AssertNoErrors();

                // Arrange
                CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, Logger);
                var nugetTestService = GetNuGetTestService();
                var uiwindow = nugetTestService.GetUIWindowfromProject(testContext.SolutionService.Projects[0]);
                uiwindow.InstallPackageFromUI(packageName, packageVersion1);
                testContext.SolutionService.SaveAll();
                VisualStudio.AssertNuGetOutputDoesNotHaveErrors();
                VisualStudio.ClearWindows();

                // Act
                uiwindow.UpdatePackageFromUI(packageName, packageVersion2);

                // Assert
                VisualStudio.AssertNuGetOutputDoesNotHaveErrors();
                CommonUtility.AssertPackageReferenceExists(testContext.SolutionService.Projects[0], packageName, packageVersion2, Logger);
                StringAssert.Contains(GetPackageManagerOutputWindowPaneText(), $"Installed {packageName} {packageVersion2} from {privateRepositoryPath}");
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(GetNetCoreTemplates), DynamicDataSourceType.Method)]
        [Timeout(DefaultTimeout)]
        public async Task WithSourceMappingEnabled_InstallPackageFromPMUIAndNoSourcesFound_Fails(ProjectTemplate projectTemplate)
        {
            // Arrange
            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger, addNetStandardFeeds: true))
            {
                var privateRepositoryPath = Path.Combine(testContext.SolutionRoot, "PrivateRepository");
                Directory.CreateDirectory(privateRepositoryPath);
                var externalRepositoryPath = Path.Combine(testContext.SolutionRoot, "ExternalRepository");
                Directory.CreateDirectory(externalRepositoryPath);

                var packageName = "Contoso.a";
                var packageVersion = "1.0.0";

                await CommonUtility.CreatePackageInSourceAsync(externalRepositoryPath, packageName, packageVersion);

                // Create nuget.config with Package source mapping filtering rules before project is created.
                CommonUtility.CreateConfigurationFile(Path.Combine(testContext.SolutionRoot, "NuGet.Config"), $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""ExternalRepository"" value=""{externalRepositoryPath}"" />
        <add key=""PrivateRepository"" value=""{privateRepositoryPath}"" />
    </packageSources>
    <packageSourceMapping>
        <packageSource key=""externalRepository"">
            <package pattern=""External.*"" />
            <package pattern=""Others.*"" />
        </packageSource>
        <packageSource key=""PrivateRepository"">
            <package pattern=""contoso.*"" />
            <package pattern=""Test.*"" />
        </packageSource>
        <packageSource key=""nuget"">
            <package pattern=""Microsoft.*"" />
            <package pattern=""NetStandard*"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");

                VisualStudio.AssertNoErrors();

                // Act
                CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, Logger);
                var nugetTestService = GetNuGetTestService();
                var uiwindow = nugetTestService.GetUIWindowfromProject(testContext.SolutionService.Projects[0]);

                // The Install action will automatically create a package source mapping to the selected package source if it's missing,
                // so select the source which already has a mapping.
                uiwindow.SetPackageSourceOptionToSource("PrivateRepository");
                uiwindow.InstallPackageFromUI(packageName, packageVersion);

                // Assert
                CommonUtility.AssertPackageReferenceDoesNotExist(testContext.SolutionService.Projects[0], packageName, packageVersion, Logger);
            }
        }

        // Migrated from Test-NetCoreProjectSystemCacheUpdateEvent in NetCoreProjectTest.ps1
        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackageFromPMC_TriggersNuGetCacheUpdatedEventAsync()
        {
            // Arrange
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.NetCoreConsoleApp, Logger, addNetStandardFeeds: true);

            var packageName = "TestPackage";
            var packageVersion = "1.0.0";
            await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion);

            testContext.SolutionService.Build();
            testContext.NuGetApexTestService.WaitForAutoRestore();

            // Subscribe to the ISolutionManager.AfterNuGetCacheUpdated event
            using var cacheUpdatedEvent = new ManualResetEventSlim(false);
            var solutionManager = testContext.NuGetApexTestService.SolutionManager;
            void OnAfterNuGetCacheUpdated(object sender, NuGetEventArgs<string> e) => cacheUpdatedEvent.Set();
            solutionManager.AfterNuGetCacheUpdated += OnAfterNuGetCacheUpdated;

            try
            {
                // Act
                var nugetConsole = GetConsole(testContext.Project);
                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

                // Assert
                Assert.IsTrue(
                    cacheUpdatedEvent.Wait(TimeSpan.FromSeconds(10)),
                    "Cache update event should have been raised after package install.");
            }
            finally
            {
                solutionManager.AfterNuGetCacheUpdated -= OnAfterNuGetCacheUpdated;
            }
        }

        // Migrated from Test-NetCoreVSandMSBuildNoOp in NetCoreProjectTest.ps1
        [TestMethod]
        [Timeout(DefaultTimeout)]
        public void NetCoreVSAndMSBuildRestoreIsNoOp()
        {
            // Arrange
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.NetCoreConsoleApp, Logger, addNetStandardFeeds: true);

            testContext.SolutionService.Build();
            testContext.NuGetApexTestService.WaitForAutoRestore();

            var cacheFilePath = CommonUtility.GetCacheFilePath(testContext.Project.FullPath);
            CommonUtility.WaitForFileExists(cacheFilePath);

            var vsRestoreTimestamp = File.GetLastWriteTime(cacheFilePath.FullName).Ticks;

            // Act - run MSBuild restore externally
            using var process = new Process();
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = $"msbuild /t:restore \"{testContext.Project.FullPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, $"MSBuild restore failed: {standardError}");

            var msbuildRestoreTimestamp = File.GetLastWriteTime(cacheFilePath.FullName).Ticks;

            // Assert - MSBuild restore should be a no-op, cache file timestamp should not change
            Assert.AreEqual(vsRestoreTimestamp, msbuildRestoreTimestamp,
                "MSBuild restore should be a no-op after VS restore - cache file timestamp should not change.");
        }

        // There  is a bug with VS or Apex where NetCoreConsoleApp and NetCoreClassLib create netcore 2.1 projects that are not supported by the sdk
        // Commenting out any NetCoreConsoleApp or NetCoreClassLib template and swapping it for NetStandardClassLib as both are package ref.

        public static IEnumerable<object[]> GetNetCoreTemplates()
        {
            yield return new object[] { ProjectTemplate.NetStandardClassLib };
        }
    }
}
