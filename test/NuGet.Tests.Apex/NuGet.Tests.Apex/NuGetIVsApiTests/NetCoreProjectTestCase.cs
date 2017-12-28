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
        [InlineData(ProjectTemplate.NetCoreClassLib)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        public void CreateNetCoreProject_RestoresNewProject(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();
                solutionService.SaveAll();

                solutionService.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
            }
        }

        // basic create for .net core template
        [NuGetWpfTheory]
        [InlineData(ProjectTemplate.NetCoreClassLib)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        public void CreateNetCoreProject_AddProjectReference(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project1 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject1");
                project1.Build();
                var project2 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject2");
                project2.Build();
                project1.References.Dte.AddProjectReference(project2);
                solutionService.SaveAll();

                solutionService.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(project1.References.TryFindReferenceByName("TestProject2", out var result));
            }
        }
    }
}
