// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Test.Apex.VisualStudio.Solution;
using Xunit;

namespace NuGet.Tests.Apex
{
    public class NuGetUITestCase : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        public NuGetUITestCase(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory) 
            : base(visualStudioHostFixtureFactory)
        {
        }

        [StaFact]
        public void SearchPackageFromUI()
        {
            // Arrange
            EnsureVisualStudioHost();
            var dte = VisualStudio.Dte;
            var solutionService = VisualStudio.Get<SolutionService>();

            solutionService.CreateEmptySolution();
            var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            VisualStudio.ClearOutputWindow();

            // Act
            dte.ExecuteCommand("Project.ManageNuGetPackages");
            var nugetTestService = GetNuGetTestService();
            var uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.SwitchTabToBrowse();
            uiwindow.SeachPackgeFromUI("newtonsoft.json");

            // Assert
            VisualStudio.AssertNoErrors();
        }

        [StaFact]
        public void InstallPackageFromUI()
        {
            // Arrange
            EnsureVisualStudioHost();
            var dte = VisualStudio.Dte;
            var solutionService = VisualStudio.Get<SolutionService>();

            solutionService.CreateEmptySolution();
            var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            VisualStudio.ClearOutputWindow();

            // Act
            dte.ExecuteCommand("Project.ManageNuGetPackages");
            var nugetTestService = GetNuGetTestService();
            var uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.InstallPackageFromUI("newtonsoft.json","9.0.1");
            project.Build();

            // Assert
            Assert.True(uiwindow.IsPackageInstalled("newtonsoft.json", "9.0.1"));
            VisualStudio.AssertNoErrors();
        }

        [StaFact]
        public void InstallPackageToProjectsFromUI()
        {
            // Arrange
            EnsureVisualStudioHost();
            var dte = VisualStudio.Dte;
            var solutionService = VisualStudio.Get<SolutionService>();

            solutionService.CreateEmptySolution();
            var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            var nuProject = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "NuProject");
            VisualStudio.ClearOutputWindow();

            // Act
            dte.ExecuteCommand("Project.ManageNuGetPackages");
            var nugetTestService = GetNuGetTestService();
            var uiwindow = nugetTestService.GetUIWindowfromProject(nuProject);
            uiwindow.InstallPackageFromUI("newtonsoft.json", "9.0.1");
            project.Build();

            // Assert
            VisualStudio.AssertNoErrors();

            VisualStudio.ClearOutputWindow();
            VisualStudio.SelectProjectInSolutionExplorer(project.Name);
            dte.ExecuteCommand("Project.ManageNuGetPackages");
            var uiwindow2 = nugetTestService.GetUIWindowfromProject(project);
            uiwindow2.InstallPackageFromUI("newtonsoft.json", "9.0.1");
            project.Build();

            // Assert
            Assert.True(uiwindow.IsPackageInstalled("newtonsoft.json", "9.0.1"));
            Assert.True(uiwindow2.IsPackageInstalled("newtonsoft.json", "9.0.1"));
            VisualStudio.AssertNoErrors();
        }

        [StaFact]
        public void UninstallPackageFromUI()
        {
            // Arrange
            EnsureVisualStudioHost();
            var dte = VisualStudio.Dte;
            var solutionService = VisualStudio.Get<SolutionService>();

            solutionService.CreateEmptySolution();
            var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            VisualStudio.ClearWindows();

            // Act
            dte.ExecuteCommand("Project.ManageNuGetPackages");
            var nugetTestService = GetNuGetTestService();
            var uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.InstallPackageFromUI("newtonsoft.json", "9.0.1");
            project.Build();

            // Assert
            Assert.True(uiwindow.IsPackageInstalled("newtonsoft.json", "9.0.1"));

            // Act
            uiwindow.UninstallPackageFromUI("newtonsoft.json");
            project.Build();

            // Assert
            Assert.False(uiwindow.IsPackageInstalled("newtonsoft.json", "9.0.1"));
            VisualStudio.AssertNoErrors();
        }

        [StaFact]
        public void UpdatePackageFromUI()
        {
            // Arrange
            EnsureVisualStudioHost();
            var dte = VisualStudio.Dte;
            var solutionService = VisualStudio.Get<SolutionService>();

            solutionService.CreateEmptySolution();
            var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject");
            VisualStudio.ClearWindows();

            // Act
            dte.ExecuteCommand("Project.ManageNuGetPackages");
            var nugetTestService = GetNuGetTestService();
            var uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.InstallPackageFromUI("newtonsoft.json", "9.0.1");
            project.Build();

            // Assert
            Assert.True(uiwindow.IsPackageInstalled("newtonsoft.json", "9.0.1"));
            VisualStudio.AssertNoErrors();

            // Act
            VisualStudio.ClearWindows();
            uiwindow.UpdatePackageFromUI("newtonsoft.json", "10.0.3");
            project.Build();

            // Assert
            Assert.True(uiwindow.IsPackageInstalled("newtonsoft.json", "10.0.3"));
            VisualStudio.AssertNoErrors();
        }
    }
}
