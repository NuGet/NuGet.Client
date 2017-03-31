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
            var solutionService = VisualStudio.Get<SolutionService>();
            var nugetTestService = GetNuGetTestService();
            solutionService.CreateEmptySolution();
            var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary);

            // Act
            nugetTestService.InstallPackage(project, "newtonsoft.json");

            // Assert
            nugetTestService.Verify.PackageIsInstalled(project, "newtonsoft.json");
        }

        [StaFact]
        public void SimpleUninstallFromIVsInstaller()
        {
            // Arrange
            EnsureVisualStudioHost();
            var solutionService = VisualStudio.Get<SolutionService>();
            var nugetTestService = GetNuGetTestService();
            solutionService.CreateEmptySolution();
            var project = solutionService.AddProject(ProjectLanguage.CSharp, ProjectTemplate.ClassLibrary);

            // Act & Assert
            nugetTestService.InstallPackage(project, "newtonsoft.json");
            nugetTestService.Verify.PackageIsInstalled(project, "newtonsoft.json");

            // Act & Assert
            nugetTestService.UninstallPackage(project, "newtonsoft.json");
            nugetTestService.Verify.PackageIsNotInstalled(project, "newtonsoft.json");
        }
    }
}
