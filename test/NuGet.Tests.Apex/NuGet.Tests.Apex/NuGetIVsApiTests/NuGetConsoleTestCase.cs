// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.StaFact;
using Xunit;

namespace NuGet.Tests.Apex
{
    public class NuGetConsoleTestCase : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        public NuGetConsoleTestCase(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory)
            : base(visualStudioHostFixtureFactory)
        {
        }

        // Verify PR only, packages.config is tested in InstallPackageFromPMCVerifyGetPackageDisplaysPackage
        [NuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void InstallPackageFromPMCWithNoAutoRestoreVerifyAssetsFile(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, noAutoRestore: true))
            {
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(testContext.Project);

                var installed = nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                installed.Should().BeTrue("Install-Package should pass");

                // Verify install from project.assets.json
                var inAssetsFile = Utils.IsPackageInstalledInAssetsFile(nugetConsole, testContext.Project.FullPath, packageName, packageVersion);
                inAssetsFile.Should().BeTrue("package was installed");
            }
        }

        // Verify packages.config and PackageReference
        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallPackageFromPMCVerifyGetPackageDisplaysPackage(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(testContext.Project);

                var installed = nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                installed.Should().BeTrue("Install-Package should pass");

                // Build before the install check to ensure that everything is up to date.
                testContext.Project.Build();

                // Verify install from Get-Package
                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, packageName, packageVersion);

                VisualStudio.AssertNoErrors();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UninstallPackageFromPMC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion).Should().BeTrue("Install-Package");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, packageName, packageVersion);

                nugetConsole.UninstallPackageFromPMC(packageName).Should().BeTrue("Uninstall-Package");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsNotInstalled(testContext.Project.UniqueName, packageName);

                VisualStudio.AssertNoErrors();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UpdatePackageFromPMC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion1).Should().BeTrue("Install-Package");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, packageName, packageVersion1);

                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2).Should().BeTrue("UnInstall-Package");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, packageName, packageVersion2);

                VisualStudio.AssertNoErrors();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallMultiplePackagesFromPMC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                var packageName1 = "TestPackage1";
                var packageVersion1 = "1.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName1, packageVersion1);

                var packageName2 = "TestPackage2";
                var packageVersion2 = "1.2.3";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName2, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1).Should().BeTrue("Install-Package 1");
                nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2).Should().BeTrue("Install-Package 2");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, packageName1, packageVersion1);
                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, packageName2, packageVersion2);

                VisualStudio.AssertNoErrors();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UninstallMultiplePackagesFromPMC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                var packageName1 = "TestPackage1";
                var packageVersion1 = "1.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName1, packageVersion1);

                var packageName2 = "TestPackage2";
                var packageVersion2 = "1.2.3";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName2, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1).Should().BeTrue("Install-Package 1");
                nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2).Should().BeTrue("Install-Package 2");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, packageName1, packageVersion1);
                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, packageName2, packageVersion2);

                VisualStudio.AssertNoErrors();

                nugetConsole.UninstallPackageFromPMC(packageName1).Should().BeTrue("Uninstall package 1");
                nugetConsole.UninstallPackageFromPMC(packageName2).Should().BeTrue("Uninstall package 2");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsNotInstalled(testContext.Project.UniqueName, packageName1);
                GetNuGetTestService().Verify.PackageIsNotInstalled(testContext.Project.UniqueName, packageName2);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void DowngradePackageFromPMC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion2).Should().BeTrue("Install-Package");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, packageName, packageVersion2);

                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion1).Should().BeTrue("Update-Package");
                testContext.Project.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(testContext.Project.UniqueName, packageName, packageVersion1);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetNetCoreTemplates))]
        public void NetCoreTransitivePackageReferenceLimit(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
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
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion).Should().BeTrue("Install-Package");
                testContext.Project.Build();
                project2.Build();
                project3.Build();
                projectX.Build();
                testContext.SolutionService.Build();

                GetNuGetTestService().Verify.PackageIsInstalled(project3.UniqueName, packageName);

                // Verify install from project.assets.json
                var inAssetsFile = Utils.IsPackageInstalledInAssetsFile(nugetConsole, testContext.Project.FullPath, packageName, packageVersion);
                inAssetsFile.Should().BeTrue("package was installed");

                var inAssetsFile2 = Utils.IsPackageInstalledInAssetsFile(nugetConsole, project2.FullPath, packageName, packageVersion);
                inAssetsFile2.Should().BeTrue("package 2 was installed");

                var inAssetsFileX = Utils.IsPackageInstalledInAssetsFile(nugetConsole, projectX.FullPath, packageName, packageVersion);
                inAssetsFileX.Should().BeFalse("package X was installed");
            }
        }

        // There  is a bug with VS or Apex where NetCoreConsoleApp creates a netcore 2.1 project that is not supported by the sdk
        // Commenting out any NetCoreConsoleApp template and swapping it for NetStandardClassLib as both are package ref.
        public static IEnumerable<object[]> GetNetCoreTemplates()
        {
            for (var i = 0; i < Utils.GetIterations(); i++)
            {
                //yield return new object[] { ProjectTemplate.NetCoreConsoleApp };
                yield return new object[] { ProjectTemplate.NetStandardClassLib };
            }
        }

        public static IEnumerable<object[]> GetPackageReferenceTemplates()
        {
            for (var i = 0; i < Utils.GetIterations(); i++)
            {
                //yield return new object[] { ProjectTemplate.NetCoreConsoleApp };
                yield return new object[] { ProjectTemplate.NetStandardClassLib };
            }
        }

        public static IEnumerable<object[]> GetTemplates()
        {
            for (var i = 0; i < Utils.GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.ClassLibrary };
                yield return new object[] { ProjectTemplate.NetStandardClassLib };
            }
        }
    }
}
