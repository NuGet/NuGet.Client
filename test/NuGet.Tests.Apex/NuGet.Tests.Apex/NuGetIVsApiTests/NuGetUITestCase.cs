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
            Assert.True(VisualStudio.HasNoErrorsInErrorList());
            Assert.True(VisualStudio.HasNoErrorsInOutputWindows());
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

            // Assert
            Assert.True(VisualStudio.HasNoErrorsInErrorList());
            Assert.True(VisualStudio.HasNoErrorsInOutputWindows());
            Assert.True(uiwindow.IsPackageInstalled("newtonsoft.json", "9.0.1"));
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

            // Assert
            Assert.True(VisualStudio.HasNoErrorsInErrorList());
            Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

            VisualStudio.ClearOutputWindow();
            VisualStudio.SelectProjectInSolutionExplorer(project.Name);
            dte.ExecuteCommand("Project.ManageNuGetPackages");
            var uiwindow2 = nugetTestService.GetUIWindowfromProject(project);
            uiwindow2.InstallPackageFromUI("newtonsoft.json", "9.0.1");

            // Assert
            Assert.True(VisualStudio.HasNoErrorsInErrorList());
            Assert.True(VisualStudio.HasNoErrorsInOutputWindows());
            Assert.True(uiwindow.IsPackageInstalled("newtonsoft.json", "9.0.1"));
            Assert.True(uiwindow2.IsPackageInstalled("newtonsoft.json", "9.0.1"));
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
            VisualStudio.ClearOutputWindow();

            // Act
            dte.ExecuteCommand("Project.ManageNuGetPackages");
            var nugetTestService = GetNuGetTestService();
            var uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.InstallPackageFromUI("newtonsoft.json", "9.0.1");

            // Assert
            Assert.True(uiwindow.IsPackageInstalled("newtonsoft.json", "9.0.1"));

            // Act
            uiwindow.UninstallPackageFromUI("newtonsoft.json");

            // Assert
            VisualStudio.HasNoErrorsInErrorList();
            VisualStudio.HasNoErrorsInOutputWindows();
            Assert.False(uiwindow.IsPackageInstalled("newtonsoft.json", "9.0.1"));
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
            VisualStudio.ClearOutputWindow();

            // Act
            dte.ExecuteCommand("Project.ManageNuGetPackages");
            var nugetTestService = GetNuGetTestService();
            var uiwindow = nugetTestService.GetUIWindowfromProject(project);
            uiwindow.InstallPackageFromUI("newtonsoft.json", "9.0.1");

            // Assert
            VisualStudio.HasNoErrorsInErrorList();
            VisualStudio.HasNoErrorsInOutputWindows();
            Assert.True(uiwindow.IsPackageInstalled("newtonsoft.json", "9.0.1"));

            // Act
            VisualStudio.ClearOutputWindow();
            uiwindow.UpdatePackageFromUI("newtonsoft.json", "10.0.3");

            // Assert
            VisualStudio.HasNoErrorsInErrorList();
            VisualStudio.HasNoErrorsInOutputWindows();
            Assert.True(uiwindow.IsPackageInstalled("newtonsoft.json", "10.0.3"));
        }
    }
}
