// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.StaFact;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Tests.Apex
{
    public class NuGetConsoleTestCase : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        public NuGetConsoleTestCase(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory, ITestOutputHelper output)
            : base(visualStudioHostFixtureFactory, output)
        {
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public async Task InstallPackageFromPMCWithNoAutoRestoreVerifyAssetsFileAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger, noAutoRestore: true))
            {
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

                CommonUtility.AssertPackageInAssetsFile(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task InstallPackageFromPMCVerifyInstallForPCAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

                CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task UninstallPackageFromPMCForPCAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                nugetConsole.UninstallPackageFromPMC(packageName);

                CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task UpdatePackageFromPMCForPCAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion1);
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion1);
                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2);

                CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, packageName, packageVersion2, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task InstallMultiplePackagesFromPMCForPCAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                var packageName1 = "TestPackage1";
                var packageVersion1 = "1.0.0";
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName1, packageVersion1);

                var packageName2 = "TestPackage2";
                var packageVersion2 = "1.2.3";
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName2, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1);
                nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2);

                CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, packageName1, packageVersion1, XunitLogger);
                CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, packageName2, packageVersion2, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task UninstallMultiplePackagesFromPMCForPCAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();
            var packageName1 = "TestPackage1";
            var packageVersion1 = "1.0.0";
            var packageName2 = "TestPackage2";
            var packageVersion2 = "1.2.3";

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName1, packageVersion1);
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName2, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1);
                testContext.SolutionService.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2);
                testContext.SolutionService.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UninstallPackageFromPMC(packageName1);
                testContext.SolutionService.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UninstallPackageFromPMC(packageName2);
                testContext.SolutionService.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, testContext.Project, packageName1, packageVersion1, XunitLogger);
                CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, testContext.Project, packageName2, packageVersion2, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public async Task DowngradePackageFromPMCForPCAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();
            var packageName = "TestPackage";
            var packageVersion1 = "1.0.0";
            var packageVersion2 = "2.0.0";

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion1);
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion2);
                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion1);

                CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, packageName, packageVersion1, XunitLogger);
            }
        }

        [NuGetWpfTheory(Skip = "https://github.com/NuGet/Home/issues/8469")]
        [MemberData(nameof(GetNetCoreTemplates))]
        public async Task NetCoreTransitivePackageReferenceLimitAsync(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                var project2 = testContext.SolutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject2");
                project2.Build();
                var project3 = testContext.SolutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject3");
                project3.Build();
                var projectX = testContext.SolutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProjectX");
                projectX.Build();
                testContext.SolutionService.Build();

                testContext.Project.References.Dte.AddProjectReference(project2);
                testContext.Project.References.Dte.AddProjectReference(projectX);
                project2.References.Dte.AddProjectReference(project3);
                testContext.SolutionService.SaveAll();
                testContext.SolutionService.Build();

                var nugetConsole = GetConsole(project3);

                var packageName = "newtonsoft.json";
                var packageVersion = "9.0.1";
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                testContext.SolutionService.Build();
                project2.Build();
                project3.Build();
                projectX.Build();
                testContext.SolutionService.Build();

                CommonUtility.AssertPackageInAssetsFile(VisualStudio, project3, packageName, packageVersion, XunitLogger);
                CommonUtility.AssertPackageInAssetsFile(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
                CommonUtility.AssertPackageInAssetsFile(VisualStudio, project2, packageName, packageVersion, XunitLogger);
                CommonUtility.AssertPackageNotInAssetsFile(VisualStudio, projectX, packageName, packageVersion, XunitLogger);
            }
        }

        [NuGetWpfTheory(Skip = "https://github.com/NuGet/Home/issues/8386")]
        [InlineData(ProjectTemplate.ClassLibrary, false)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp, true)]
        [InlineData(ProjectTemplate.NetStandardClassLib, true)]
        public async Task InstallAndUpdatePackageWithSourceParameterWarnsAsync(ProjectTemplate projectTemplate, bool warns)
        {
            EnsureVisualStudioHost();
            var packageName = "TestPackage";
            var packageVersion1 = "1.0.0";
            var packageVersion2 = "2.0.0";
            var source = "https://api.nuget.org/v3/index.json";

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                // Arrange
                var solutionService = VisualStudio.Get<SolutionService>();
                testContext.SolutionService.Build();

                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion1);
                await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion2);

                var nugetTestService = GetNuGetTestService();
                var nugetConsole = GetConsole(testContext.Project);

                // Act
                nugetConsole.InstallPackageFromPMC(packageName, packageVersion1, source);
                testContext.SolutionService.Build();

                // Assert
                var expectedMessage = $"The 'Source' parameter is not respected for the transitive package management based project(s) {Path.GetFileNameWithoutExtension(testContext.Project.UniqueName)}. The enabled sources in your NuGet configuration will be used";
                Assert.True(warns == nugetConsole.IsMessageFoundInPMC(expectedMessage), expectedMessage);
                VisualStudio.AssertNuGetOutputDoesNotHaveErrors();
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                // setup again
                nugetConsole.Clear();

                // Act
                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2, source);
                testContext.SolutionService.Build();

                // Assert
                Assert.True(warns == nugetConsole.IsMessageFoundInPMC(expectedMessage), expectedMessage);
                VisualStudio.AssertNuGetOutputDoesNotHaveErrors();
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        // There  is a bug with VS or Apex where NetCoreConsoleApp creates a netcore 2.1 project that is not supported by the sdk
        // Commenting out any NetCoreConsoleApp template and swapping it for NetStandardClassLib as both are package ref.
        public static IEnumerable<object[]> GetNetCoreTemplates()
        {
            yield return new object[] { ProjectTemplate.NetStandardClassLib };
        }

        public static IEnumerable<object[]> GetPackageReferenceTemplates(string flag, string expectedVersion)
        {
            yield return new object[] { ProjectTemplate.NetStandardClassLib , flag , expectedVersion};
        }

        public static IEnumerable<object[]> GetPackageReferenceTemplates()
        {
            yield return new object[] { ProjectTemplate.NetStandardClassLib };
        }

        public static IEnumerable<object[]> GetPackagesConfigTemplates()
        {
            yield return new object[] { ProjectTemplate.ClassLibrary };
        }
    }
}
