using System.Collections.Generic;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.StaFact;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Tests.Apex
{
    public class NetCoreProjectTestCase : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        public NetCoreProjectTestCase(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory)
            : base(visualStudioHostFixtureFactory)
        {
        }

        // basic create for .net core template
        [NuGetWpfTheory]
        [MemberData(nameof(GetNetCoreTemplates))]
        public void CreateNetCoreProject_RestoresNewProject(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                VisualStudio.AssertNoErrors();
            }
        }

        // basic create for .net core template
        [NuGetWpfTheory]
        [MemberData(nameof(GetNetCoreTemplates))]
        public void CreateNetCoreProject_AddProjectReference(ProjectTemplate projectTemplate)
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, projectTemplate))
            {
                var project2 = testContext.SolutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject2");
                project2.Build();

                testContext.Project.References.Dte.AddProjectReference(project2);
                testContext.SolutionService.SaveAll();

                testContext.SolutionService.Build();

                Assert.True(testContext.Project.References.TryFindReferenceByName("TestProject2", out var result));
                VisualStudio.AssertNoErrors();
            }
        }

        // There  is a bug with VS or Apex where NetCoreConsoleApp and NetCoreClassLib create netcore 2.1 projects that are not supported by the sdk
        // Commenting out any NetCoreConsoleApp or NetCoreClassLib template and swapping it for NetStandardClassLib as both are package ref.

        public static IEnumerable<object[]> GetNetCoreTemplates()
        {
            for (var i = 0; i < Utils.GetIterations(); i++)
            {
                //yield return new object[] { ProjectTemplate.NetCoreClassLib };
                //yield return new object[] { ProjectTemplate.NetCoreConsoleApp };
                yield return new object[] { ProjectTemplate.NetStandardClassLib };
            }
        }
    }
}
