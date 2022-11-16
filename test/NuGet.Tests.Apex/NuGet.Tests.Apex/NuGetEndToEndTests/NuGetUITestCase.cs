// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Tests.Apex
{
    public class NuGetUITestCase : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        private const string TestPackageName = "Contoso.A";
        private const string TestPackageVersionV1 = "1.0.0";
        private const string TestPackageVersionV2 = "2.0.0";
        private const string PrimarySourceName = "source";
        private const string SecondarySourceName = "SecondarySource";

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

        private readonly SimpleTestPathContext _pathContext = new SimpleTestPathContext();

        public NuGetUITestCase(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory, ITestOutputHelper output)
            : base(visualStudioHostFixtureFactory, output)
        {
        }

        [StaFact]
        public async Task SearchPackageFromUI()
        {
            // Arrange
            await CommonUtility.CreatePackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV1);

            NuGetApexTestService nugetTestService = GetNuGetTestService();

            SolutionService solutionService = VisualStudio.Get<SolutionService>();
            solutionService.CreateEmptySolution("TestSolution", _pathContext.SolutionRoot);
            ProjectTestExtension project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            VisualStudio.ClearOutputWindow();
            solutionService.SaveAll();

            // Act
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);

            NuGetUIProjectTestExtension uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.SwitchTabToBrowse();
            uiwindow.SearchPackageFromUI(TestPackageName);

            // Assert
            VisualStudio.AssertNoErrors();
        }

        [StaFact]
        public async Task InstallPackageFromUI()
        {
            // Arrange
            await CommonUtility.CreatePackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV1);

            NuGetApexTestService nugetTestService = GetNuGetTestService();

            SolutionService solutionService = VisualStudio.Get<SolutionService>();
            solutionService.CreateEmptySolution("TestSolution", _pathContext.SolutionRoot);
            ProjectTestExtension project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            VisualStudio.ClearOutputWindow();
            solutionService.SaveAll();

            // Act
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);

            NuGetUIProjectTestExtension uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.InstallPackageFromUI(TestPackageName, TestPackageVersionV1);

            // Assert
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, project, TestPackageName, TestPackageVersionV1, XunitLogger);
        }

        [StaFact]
        public async Task InstallPackageToProjectsFromUI()
        {
            // Arrange
            await CommonUtility.CreatePackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV1);

            NuGetApexTestService nugetTestService = GetNuGetTestService();

            SolutionService solutionService = VisualStudio.Get<SolutionService>();
            solutionService.CreateEmptySolution("TestSolution", _pathContext.SolutionRoot);
            ProjectTestExtension project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            ProjectTestExtension nuProject = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "NuProject");
            solutionService.SaveAll();

            // Act
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);

            NuGetUIProjectTestExtension uiwindow = nugetTestService.GetUIWindowfromProject(nuProject);
            uiwindow.InstallPackageFromUI(TestPackageName, TestPackageVersionV1);

            VisualStudio.SelectProjectInSolutionExplorer(project.Name);
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);

            VisualStudio.ClearOutputWindow();
            NuGetUIProjectTestExtension uiwindow2 = nugetTestService.GetUIWindowfromProject(project);
            uiwindow2.InstallPackageFromUI(TestPackageName, TestPackageVersionV1);

            // Assert
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, project, TestPackageName, TestPackageVersionV1, XunitLogger);
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, nuProject, TestPackageName, TestPackageVersionV1, XunitLogger);
        }

        [StaFact]
        public async Task UninstallPackageFromUI()
        {
            // Arrange
            await CommonUtility.CreatePackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV1);

            NuGetApexTestService nugetTestService = GetNuGetTestService();

            SolutionService solutionService = VisualStudio.Get<SolutionService>();
            solutionService.CreateEmptySolution("TestSolution", _pathContext.SolutionRoot);
            ProjectTestExtension project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            solutionService.SaveAll();

            FileInfo packagesConfigFile = GetPackagesConfigFile(project);
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);

            NuGetUIProjectTestExtension uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.InstallPackageFromUI(TestPackageName, TestPackageVersionV1);

            WaitForFileExists(packagesConfigFile);

            VisualStudio.ClearWindows();

            // Act
            uiwindow.UninstallPackageFromUI(TestPackageName);

            WaitForFileNotExists(packagesConfigFile);

            // Assert
            CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, project, TestPackageName, XunitLogger);
        }

        [StaFact]
        public async Task UpdatePackageFromUI()
        {
            // Arrange
            await CommonUtility.CreatePackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV1);
            await CommonUtility.CreatePackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV2);

            NuGetApexTestService nugetTestService = GetNuGetTestService();

            SolutionService solutionService = VisualStudio.Get<SolutionService>();
            solutionService.CreateEmptySolution("TestSolution", _pathContext.SolutionRoot);
            ProjectTestExtension project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            VisualStudio.ClearWindows();
            solutionService.SaveAll();

            // Act
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);

            NuGetUIProjectTestExtension uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.InstallPackageFromUI(TestPackageName, TestPackageVersionV1);

            // Act
            VisualStudio.ClearWindows();
            uiwindow.UpdatePackageFromUI(TestPackageName, TestPackageVersionV2);

            // Assert
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, project, TestPackageName, TestPackageVersionV2, XunitLogger);
        }

        [StaFact]
        public async Task InstallPackageFromUI_PC_PackageSourceMapping_WithSingleFeed_Match_Succeeds()
        {
            // Arrange
            await CommonUtility.CreatePackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV1);

            // Create nuget.config with Package source mapping filtering rules before project is created.
            _pathContext.Settings.AddPackageSourceMapping(PrimarySourceName, "Contoso.*", "Test.*");

            NuGetApexTestService nugetTestService = GetNuGetTestService();

            SolutionService solutionService = VisualStudio.Get<SolutionService>();
            solutionService.CreateEmptySolution("TestSolution", _pathContext.SolutionRoot);
            ProjectTestExtension project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            VisualStudio.ClearOutputWindow();
            solutionService.SaveAll();

            // Act
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);

            NuGetUIProjectTestExtension uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.InstallPackageFromUI(TestPackageName, TestPackageVersionV1);

            // Assert
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, project, TestPackageName, TestPackageVersionV1, XunitLogger);
        }

        [StaFact]
        public async Task InstallPackageToProjectsFromUI_PC_PackageSourceMapping_WithSingleFeed_Match_Succeeds()
        {
            // Arrange
            await CommonUtility.CreatePackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV1);

            // Create nuget.config with Package source mapping filtering rules before project is created.
            _pathContext.Settings.AddPackageSourceMapping(PrimarySourceName, "Contoso.*", "Test.*");

            NuGetApexTestService nugetTestService = GetNuGetTestService();

            SolutionService solutionService = VisualStudio.Get<SolutionService>();
            solutionService.CreateEmptySolution("TestSolution", _pathContext.SolutionRoot);
            ProjectTestExtension project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            ProjectTestExtension nuProject = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "NuProject");
            solutionService.SaveAll();

            // Act
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);
            NuGetUIProjectTestExtension uiwindow = nugetTestService.GetUIWindowfromProject(nuProject);
            uiwindow.InstallPackageFromUI(TestPackageName, TestPackageVersionV1);
            VisualStudio.SelectProjectInSolutionExplorer(project.Name);
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);

            VisualStudio.ClearOutputWindow();
            NuGetUIProjectTestExtension uiwindow2 = nugetTestService.GetUIWindowfromProject(project);
            uiwindow2.InstallPackageFromUI(TestPackageName, TestPackageVersionV1);

            // Assert
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, project, TestPackageName, TestPackageVersionV1, XunitLogger);
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, nuProject, TestPackageName, TestPackageVersionV1, XunitLogger);
        }

        [StaFact]
        public async Task InstallPackageFromUI_PC_PackageSourceMapping_WithMultiFeed_Succeed()
        {
            // Arrange
            string secondarySourcePath = Directory.CreateDirectory(Path.Combine(_pathContext.SolutionRoot, SecondarySourceName)).FullName;

            await CommonUtility.CreateNetFrameworkPackageInSourceAsync(secondarySourcePath, TestPackageName, TestPackageVersionV1);
            await CommonUtility.CreateNetFrameworkPackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV1);

            // Create nuget.config with Package source mapping filtering rules before project is created.
            _pathContext.Settings.AddSource(SecondarySourceName, secondarySourcePath);
            _pathContext.Settings.AddPackageSourceMapping(SecondarySourceName, "External.*", "Others.*");
            _pathContext.Settings.AddPackageSourceMapping(PrimarySourceName, "Contoso.*", "Test.*");

            NuGetApexTestService nugetTestService = GetNuGetTestService();

            SolutionService solutionService = VisualStudio.Get<SolutionService>();
            solutionService.CreateEmptySolution("TestSolution", _pathContext.SolutionRoot);
            ProjectTestExtension project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            VisualStudio.ClearOutputWindow();
            solutionService.SaveAll();

            // Act
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);

            NuGetUIProjectTestExtension uiwindow = nugetTestService.GetUIWindowfromProject(project);

            // Set option to package source option to All
            uiwindow.SetPackageSourceOptionToAll();
            uiwindow.InstallPackageFromUI(TestPackageName, TestPackageVersionV1);

            // Assert
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, project, "contoso.a", XunitLogger);
        }

        [StaFact]
        public async Task InstallPackageFromUI_PC_PackageSourceMapping_WithMultiFeed_Fails()
        {
            // Arrange
            string secondarySourcePath = Directory.CreateDirectory(Path.Combine(_pathContext.SolutionRoot, SecondarySourceName)).FullName;

            await CommonUtility.CreateNetFrameworkPackageInSourceAsync(secondarySourcePath, TestPackageName, TestPackageVersionV1);

            // Create nuget.config with Package source mapping filtering rules before project is created.
            _pathContext.Settings.AddSource(SecondarySourceName, secondarySourcePath);
            _pathContext.Settings.AddPackageSourceMapping(SecondarySourceName, "External.*", "Others.*");
            _pathContext.Settings.AddPackageSourceMapping(PrimarySourceName, "Contoso.*", "Test.*");

            NuGetApexTestService nugetTestService = GetNuGetTestService();

            SolutionService solutionService = VisualStudio.Get<SolutionService>();
            solutionService.CreateEmptySolution("TestSolution", _pathContext.SolutionRoot);
            ProjectTestExtension project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            VisualStudio.ClearOutputWindow();
            solutionService.SaveAll();

            // Act
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);

            NuGetUIProjectTestExtension uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.InstallPackageFromUI(TestPackageName, TestPackageVersionV1);

            // Assert
            // Even though Contoso.a exist in ExternalRepository, but packageSourceMapping filter doesn't let restore from it.
            CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, project, "contoso.a", XunitLogger);
        }

        [StaFact]
        public async Task UpdatePackageFromUI_PC_PackageSourceMapping_WithSingleFeed_Succeeds()
        {
            // Arrange
            await CommonUtility.CreatePackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV1);
            await CommonUtility.CreatePackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV2);

            // Create nuget.config with Package source mapping filtering rules before project is created.
            _pathContext.Settings.AddPackageSourceMapping(PrimarySourceName, "Contoso.*", "Test.*");

            NuGetApexTestService nugetTestService = GetNuGetTestService();

            SolutionService solutionService = VisualStudio.Get<SolutionService>();
            solutionService.CreateEmptySolution("TestSolution", _pathContext.SolutionRoot);
            ProjectTestExtension project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            VisualStudio.ClearOutputWindow();
            solutionService.SaveAll();

            // Act
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);
            NuGetUIProjectTestExtension uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.InstallPackageFromUI(TestPackageName, TestPackageVersionV1);

            // Act
            VisualStudio.ClearWindows();
            uiwindow.UpdatePackageFromUI(TestPackageName, TestPackageVersionV2);

            // Assert
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, project, TestPackageName, TestPackageVersionV2, XunitLogger);
        }


        [StaFact]
        public async Task UpdatePackageFromUI_PC_PackageSourceMapping_WithMultiFeed_Succeed()
        {
            // Arrange
            string secondarySourcePath = Directory.CreateDirectory(Path.Combine(_pathContext.SolutionRoot, SecondarySourceName)).FullName;
            await CommonUtility.CreateNetFrameworkPackageInSourceAsync(secondarySourcePath, TestPackageName, TestPackageVersionV1);
            await CommonUtility.CreateNetFrameworkPackageInSourceAsync(secondarySourcePath, TestPackageName, TestPackageVersionV2);
            await CommonUtility.CreateNetFrameworkPackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV1);
            await CommonUtility.CreateNetFrameworkPackageInSourceAsync(_pathContext.PackageSource, TestPackageName, TestPackageVersionV2);

            // Create nuget.config with Package source mapping filtering rules before project is created.
            _pathContext.Settings.AddSource(SecondarySourceName, secondarySourcePath);
            _pathContext.Settings.AddPackageSourceMapping(SecondarySourceName, "External.*", "Others.*");
            _pathContext.Settings.AddPackageSourceMapping(PrimarySourceName, "Contoso.*", "Test.*");

            NuGetApexTestService nugetTestService = GetNuGetTestService();

            SolutionService solutionService = VisualStudio.Get<SolutionService>();
            solutionService.CreateEmptySolution("TestSolution", _pathContext.SolutionRoot);
            ProjectTestExtension project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            VisualStudio.ClearOutputWindow();
            solutionService.SaveAll();

            // Act
            CommonUtility.OpenNuGetPackageManagerWithDte(VisualStudio, XunitLogger);
            NuGetUIProjectTestExtension uiwindow = nugetTestService.GetUIWindowfromProject(project);

            // Set option to package source option to All
            uiwindow.SetPackageSourceOptionToAll();
            uiwindow.InstallPackageFromUI(TestPackageName, TestPackageVersionV1);

            // Act
            uiwindow.InstallPackageFromUI(TestPackageName, TestPackageVersionV2);

            // Assert
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, project, TestPackageName, TestPackageVersionV2, XunitLogger);
        }

        public override void Dispose()
        {
            _pathContext.Dispose();

            base.Dispose();
        }

        private static FileInfo GetPackagesConfigFile(ProjectTestExtension project)
        {
            var projectFile = new FileInfo(project.FullPath);

            return new FileInfo(Path.Combine(projectFile.DirectoryName, "packages.config"));
        }

        private static void WaitForFileExists(FileInfo file)
        {
            Omni.Common.WaitFor.IsTrue(
                () => File.Exists(file.FullName),
                Timeout,
                Interval,
                $"{file.FullName} did not exist within {Timeout}.");
        }

        private static void WaitForFileNotExists(FileInfo file)
        {
            Omni.Common.WaitFor.IsTrue(
                () => !File.Exists(file.FullName),
                Timeout,
                Interval,
                $"{file.FullName} still existed after {Timeout}.");
        }
    }
}
