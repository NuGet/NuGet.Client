// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
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
        public void InstallPackageFromPMCWithNoAutoRestoreVerifyAssetsFile(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger, noAutoRestore: true))
            {
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
                Utils.AssertPackageInAssetsFile(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void InstallPackageFromPMCVerifyInstallForPR(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

                // Build before the install check to ensure that everything is up to date.
                testContext.Project.Build();

                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
                Utils.AssertPackageInAssetsFile(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public void InstallPackageFromPMCVerifyInstallForPC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

                Utils.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void UninstallPackageFromPMCForPR(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UninstallPackageFromPMC(packageName);
                testContext.Project.Build();

                Utils.AssertPackageReferenceDoesNotExist(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
                Utils.AssertPackageNotInAssetsFile(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public void UninstallPackageFromPMCForPC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                nugetConsole.UninstallPackageFromPMC(packageName);

                Utils.AssetPackageNotInPackagesConfig(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void UpdatePackageFromPMCForPR(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion1);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2);
                testContext.Project.Build();

                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, packageName, packageVersion2, XunitLogger);
                Utils.AssertPackageInAssetsFile(VisualStudio, testContext.Project, packageName, packageVersion2, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public void UpdatePackageFromPMCForPC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion1);
                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2);

                Utils.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, packageName, packageVersion2, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void InstallMultiplePackagesFromPMCForPR(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                var packageName1 = "TestPackage1";
                var packageVersion1 = "1.0.0";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName1, packageVersion1);

                var packageName2 = "TestPackage2";
                var packageVersion2 = "1.2.3";
                Utils.CreatePackageInSource(testContext.PackageSource, packageName2, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2);
                testContext.Project.Build();

                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, packageName1, packageVersion1, XunitLogger);
                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, packageName2, packageVersion2, XunitLogger);

                Utils.AssertPackageInAssetsFile(VisualStudio, testContext.Project, packageName1, packageVersion1, XunitLogger);
                Utils.AssertPackageInAssetsFile(VisualStudio, testContext.Project, packageName2, packageVersion2, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public void InstallMultiplePackagesFromPMCForPC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
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

                Utils.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, packageName1, packageVersion1, XunitLogger);
                Utils.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, packageName2, packageVersion2, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void UninstallMultiplePackagesFromPMCForPR(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();
            var packageName1 = "TestPackage1";
            var packageVersion1 = "1.0.0";
            var packageName2 = "TestPackage2";
            var packageVersion2 = "1.2.3";

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {

                Utils.CreatePackageInSource(testContext.PackageSource, packageName1, packageVersion1);
                Utils.CreatePackageInSource(testContext.PackageSource, packageName2, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UninstallPackageFromPMC(packageName1);
                nugetConsole.UninstallPackageFromPMC(packageName2);
                testContext.Project.Build();

                Utils.AssertPackageReferenceDoesNotExist(VisualStudio, testContext.Project, packageName1, packageVersion1, XunitLogger);
                Utils.AssertPackageReferenceDoesNotExist(VisualStudio, testContext.Project, packageName2, packageVersion2, XunitLogger);

                Utils.AssertPackageNotInAssetsFile(VisualStudio, testContext.Project, packageName1, packageVersion1, XunitLogger);
                Utils.AssertPackageNotInAssetsFile(VisualStudio, testContext.Project, packageName2, packageVersion2, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public void UninstallMultiplePackagesFromPMCForPC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();
            var packageName1 = "TestPackage1";
            var packageVersion1 = "1.0.0";
            var packageName2 = "TestPackage2";
            var packageVersion2 = "1.2.3";

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {

                Utils.CreatePackageInSource(testContext.PackageSource, packageName1, packageVersion1);
                Utils.CreatePackageInSource(testContext.PackageSource, packageName2, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UninstallPackageFromPMC(packageName1);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UninstallPackageFromPMC(packageName2);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                Utils.AssetPackageNotInPackagesConfig(VisualStudio, testContext.Project, packageName1, packageVersion1, XunitLogger);
                Utils.AssetPackageNotInPackagesConfig(VisualStudio, testContext.Project, packageName2, packageVersion2, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void DowngradePackageFromPMCForPR(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();
            var packageName = "TestPackage";
            var packageVersion1 = "1.0.0";
            var packageVersion2 = "2.0.0";

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion2);
                testContext.Project.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion1);
                testContext.Project.Build();

                Utils.AssertPackageReferenceExists(VisualStudio, testContext.Project, packageName, packageVersion1, XunitLogger);
                Utils.AssertPackageInAssetsFile(VisualStudio, testContext.Project, packageName, packageVersion1, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetPackagesConfigTemplates))]
        public void DowngradePackageFromPMCForPC(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();
            var packageName = "TestPackage";
            var packageVersion1 = "1.0.0";
            var packageVersion2 = "2.0.0";

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, XunitLogger))
            {
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion2);
                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion1);

                Utils.AssetPackageInPackagesConfig(VisualStudio, testContext.Project, packageName, packageVersion1, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetNetCoreTemplates))]
        public void NetCoreTransitivePackageReferenceLimit(ProjectTemplate projectTemplate)
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
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion);

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                testContext.Project.Build();
                project2.Build();
                project3.Build();
                projectX.Build();
                testContext.SolutionService.Build();

                Utils.AssertPackageInAssetsFile(VisualStudio, project3, packageName, packageVersion, XunitLogger);
                Utils.AssertPackageInAssetsFile(VisualStudio, testContext.Project, packageName, packageVersion, XunitLogger);
                Utils.AssertPackageInAssetsFile(VisualStudio, project2, packageName, packageVersion, XunitLogger);
                Utils.AssertPackageNotInAssetsFile(VisualStudio, projectX, packageName, packageVersion, XunitLogger);
            }
        }

        [NuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary, false)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp, true)]
        [InlineData(ProjectTemplate.NetStandardClassLib, true)]
        public void InstallAndUpdatePackageWithSourceParameterWarns(ProjectTemplate projectTemplate, bool warns)
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
                testContext.Project.Build();

                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(testContext.PackageSource, packageName, packageVersion2);

                var nugetTestService = GetNuGetTestService();
                var nugetConsole = GetConsole(testContext.Project);

                // Act
                nugetConsole.InstallPackageFromPMC(packageName, packageVersion1, source);
                testContext.Project.Build();

                // Assert
                var expectedMessage = $"The 'Source' parameter is not respected for the transitive package management based project(s) {Path.GetFileNameWithoutExtension(testContext.Project.UniqueName)}. The enabled sources in your NuGet configuration will be used";
                Assert.True(warns == nugetConsole.IsMessageFoundInPMC(expectedMessage), expectedMessage);
                VisualStudio.AssertNuGetOutputDoesNotHaveErrors();
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                // setup again
                nugetConsole.Clear();

                // Act
                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2, source);
                testContext.Project.Build();

                // Assert
                Assert.True(warns == nugetConsole.IsMessageFoundInPMC(expectedMessage), expectedMessage);
                VisualStudio.AssertNuGetOutputDoesNotHaveErrors();
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetNetCoreTemplates))]
        public void NetCoreAutoReferenceCannotBeUpdatedInPMC(ProjectTemplate projectTemplate)
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

                //Preconditions
                var nugetConsole = GetConsole(testContext.Project);
                nugetConsole.InstallPackageFromPMC(packageName, packageVersion1);
                testContext.Project.Build();
                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName, packageVersion1);
                testContext.Project.Unload();


                testContext.SolutionService.SaveAll();
                testContext.SolutionService.Build();

                var csproj = new XmlDocument();
                csproj.LoadXml(testContext.Project.FullPath);


                testContext.Project.Load();
                testContext.SolutionService.Build();
                //Act
                nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2);
                testContext.SolutionService.Build();

                //Assert
                Utils.AssertPackageIsInstalled(GetNuGetTestService(), testContext.Project, packageName, packageVersion1);;
            }
        }

        private static Project GetProject(string projectCSProjPath)
        {
            var projectRootElement = TryOpenProjectRootElement(projectCSProjPath);
            if (projectCSProjPath == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_MsBuildUnableToOpenProject, projectCSProjPath));
            }
            return new Project(projectRootElement);
        }


        private static ProjectRootElement TryOpenProjectRootElement(string filename)
        {
            try
            {
                // There is ProjectRootElement.TryOpen but it does not work as expected
                // I.e. it returns null for some valid projects
                return ProjectRootElement.Open(filename, ProjectCollection.GlobalProjectCollection, preserveFormatting: true);
            }
            catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
            {
                return null;
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

        public static IEnumerable<object[]> GetPackagesConfigTemplates()
        {
            for (var i = 0; i < Utils.GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.ClassLibrary };
            }
        }
    }
}
