// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
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

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName, packageVersion);
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

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

                // Build before the install check to ensure that everything is up to date.
                testContext.Project.Build();

                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName, packageVersion);
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

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                testContext.Project.Build();

                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName, packageVersion);

                nugetConsole.UninstallPackageFromPMC(packageName);
                testContext.Project.Build();

                Utils.AssertPackageIsNotInstalled(GetNuGetTestService(), testContext.Project, packageName, packageVersion);
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

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion1);
                testContext.Project.Build();

                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName, packageVersion1);

                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2);
                testContext.Project.Build();

                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName, packageVersion2);
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

                nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1);
                nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2);
                testContext.Project.Build();

                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName1, packageVersion1);
                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName2, packageVersion2);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UninstallMultiplePackagesFromPMC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();
            var packageName1 = "TestPackage1";
            var packageVersion1 = "1.0.0";
            var packageName2 = "TestPackage2";
            var packageVersion2 = "1.2.3";

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {

                Utils.CreatePackageInSource(testContext.PackageSource, packageName1, packageVersion1);
                Utils.CreatePackageInSource(testContext.PackageSource, packageName2, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1);
                nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2);
                testContext.Project.Build();

                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName1, packageVersion1);
                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName2, packageVersion2);

                nugetConsole.UninstallPackageFromPMC(packageName1);
                nugetConsole.UninstallPackageFromPMC(packageName2);
                testContext.Project.Build();

                Utils.AssertPackageIsNotInstalled(GetNuGetTestService(), testContext.Project, packageName1, packageVersion1);
                Utils.AssertPackageIsNotInstalled(GetNuGetTestService(), testContext.Project, packageName2, packageVersion2);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void DowngradePackageFromPMC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();
            var packageName = "TestPackage";
            var packageVersion1 = "1.0.0";
            var packageVersion2 = "2.0.0";

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion2);
                testContext.Project.Build();

                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName, packageVersion2);


                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion1);
                testContext.Project.Build();

                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName, packageVersion1);
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

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                testContext.Project.Build();
                project2.Build();
                project3.Build();
                projectX.Build();
                testContext.SolutionService.Build();

                Utils.AssertPackageIsInstalled(GetNuGetTestService(), project3, packageName, packageVersion);

                // Verify install from project.assets.json
                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName, packageVersion);
                Utils.AssertPackageIsInstalled(GetNuGetTestService(), project2, packageName, packageVersion);
                Utils.AssertPackageIsNotInstalled(GetNuGetTestService(), projectX, packageName, packageVersion);
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

        [NuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary, false, false)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp, true, true)]
        [InlineData(ProjectTemplate.NetStandardClassLib, true, true)]
        public void InstallAndUpdatePackageWithSourceParameterWarns(ProjectTemplate projectTemplate, bool warns, bool installationStatus)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());
                
                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion2);
                var source = "https://api.nuget.org/v3/index.json";

                var expectedMessage = $"The 'Source' parameter is not respected for the transitive package management based project(s) {Path.GetFileNameWithoutExtension(project.UniqueName)}. The enabled sources in your NuGet configuration will be used";

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion1, source));
                Assert.True(warns == nugetConsole.IsMessageFoundInPMC(expectedMessage), expectedMessage);

                Assert.True(installationStatus == Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion1));
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
                Assert.True(nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2, source));
                Assert.True(installationStatus == Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion2));
                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion1));
                Assert.True(warns == nugetConsole.IsMessageFoundInPMC(expectedMessage), expectedMessage);
                project.Build();

                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
                solutionService.Save();
            }
        }
    }
}
