using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Xunit;

namespace NuGet.Tests.Apex
{
    public class IVsPackageInstallerTestCase : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        public IVsPackageInstallerTestCase(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory) 
            : base(visualStudioHostFixtureFactory)
        {
        }

        [StaFact]
        public void SimpleInstallFromIVsInstaller()
        {
            // Arrange
            EnsureVisualStudioHost();
            var dte = VisualStudio.Dte;
            var solutionService = VisualStudio.Get<SolutionService>();
            var nugetTestService = GetNuGetTestService();

            solutionService.CreateEmptySolution();
            solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary, ProjectTargetFramework.V46, "TestProject"); 

            var project = dte.Solution.Projects.Item(1);

            // Act
            nugetTestService.InstallPackage(project.UniqueName, "newtonsoft.json");

            // Assert
            nugetTestService.Verify.PackageIsInstalled(project.UniqueName, "newtonsoft.json");
        }

        [StaFact]
        public void SimpleUninstallFromIVsInstaller()
        {
            // Arrange
            EnsureVisualStudioHost();
            var solutionService = VisualStudio.Get<SolutionService>();
            var nugetTestService = GetNuGetTestService();
            solutionService.CreateEmptySolution();
            solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary,ProjectTargetFramework.V46, "TestProject");
            var dte = VisualStudio.Dte;
            var project = dte.Solution.Projects.Item(1);

            // Act & Assert
            nugetTestService.InstallPackage(project.UniqueName, "newtonsoft.json");
            nugetTestService.Verify.PackageIsInstalled(project.UniqueName, "newtonsoft.json");

            // Act & Assert
            nugetTestService.UninstallPackage(project.UniqueName, "newtonsoft.json");
            nugetTestService.Verify.PackageIsNotInstalled(project.UniqueName, "newtonsoft.json");
        }
    }
}
