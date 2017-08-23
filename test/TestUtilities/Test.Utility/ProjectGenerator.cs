using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Test.Utility;

namespace Test.Utility
{
    public class ProjectGenerator
    {
        public static void Main()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projects = new List<SimpleTestProjectContext>();

                // Referenced but not created
                var packageXWithNoWarn = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    NoWarn = "NU1603"
                };

                // Created in the source
                var packageX11 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.1"
                };

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX11);

                for (var i = 0; i < 5; i++)
                {
                    var project = SimpleTestProjectContext.CreateNETCore(
                        "project_" + i,
                        pathContext.SolutionRoot,
                        NuGetFramework.Parse("netcoreapp2.0"));

                    // B -> X
                    project.AddPackageToAllFrameworks(packageXWithNoWarn);
                    project.Save();

                    projects.Add(project);
                }

                for (var i = 0; i < projects.Count() - 1; i++)
                {
                    var projectA = projects[i];
                    for (var j = 1; j < projects.Count(); j++)
                    {
                        var projectB = projects[j];
                        projectA.AddProjectToAllFrameworks(projectB);
                    }

                }

                foreach (var project in projects)
                {
                    project.Save();
                    solution.Projects.Add(project);
                }

                solution.Create(pathContext.SolutionRoot);
            }
        }
    }
}
