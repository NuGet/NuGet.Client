using System.Collections.Generic;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NuGet.Tests.Apex
{
    [TestClass]
    public class NetCoreProjectTestCase : SharedVisualStudioHostTestClass
    {
        private const int Timeout = 5 * 60 * 1000; // 5 minutes

        public NetCoreProjectTestCase()
            : base()
        {
        }

        // basic create for .net core template
        [TestMethod]
        [Timeout(Timeout)]
        public void CreateNetCoreProject_RestoresNewProject()
        {
            // Arrange
            EnsureVisualStudioHost();

            ProjectTemplate projectTemplate = ProjectTemplate.NetStandardClassLib;
            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, addNetStandardFeeds: true))
            {
                VisualStudio.AssertNoErrors();
            }
        }

        // basic create for .net core template
        [Ignore] //https://github.com/NuGet/Home/issues/9410
        [TestMethod]
        [Timeout(Timeout)]
        public void CreateNetCoreProject_AddProjectReference()
        {
            // Arrange
            EnsureVisualStudioHost();

            ProjectTemplate projectTemplate = ProjectTemplate.NetStandardClassLib;
            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, addNetStandardFeeds: true))
            {
                var project2 = testContext.SolutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject2");
                project2.Build();

                testContext.Project.References.Dte.AddProjectReference(project2);
                testContext.SolutionService.SaveAll();

                testContext.SolutionService.Build();
                testContext.NuGetApexTestService.WaitForAutoRestore();

                VisualStudio.AssertNoErrors();
                CommonUtility.AssertPackageInAssetsFile(VisualStudio, testContext.Project, "TestProject2", "1.0.0");
            }
        }

        // There  is a bug with VS or Apex where NetCoreConsoleApp and NetCoreClassLib create netcore 2.1 projects that are not supported by the sdk
        // Commenting out any NetCoreConsoleApp or NetCoreClassLib template and swapping it for NetStandardClassLib as both are package ref.

        public static IEnumerable<object[]> GetNetCoreTemplates()
        {
            yield return new object[] { ProjectTemplate.NetStandardClassLib };
        }
    }
}
