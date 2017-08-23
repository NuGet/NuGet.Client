using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;

namespace NuGet.Test.Utility
{
    public class ProjectGenerator
    {
        public static void Main()
        {

            int[] counts = { 5, 10, 20, 50, 100, 125, 150, 175, 200 };

            foreach(var count in counts)
            {
                var path = $@"F:\validation\TransitiveNoWarn\{count}";

                ClearPath(path);
                GenerateSolution(count, path);
            }
        }

        private static void GenerateSolution(int count, string path)
        {
            // Arrange
            var pathContext = new SimpleTestPathContext(path);

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

            for (var i = 0; i < count; i++)
            {
                var project = SimpleTestProjectContext.CreateNETCore(
                    "project_" + i,
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                projects.Add(project);
            }

            for (var i = 1; i < projects.Count() - 1; i++)
            {
                var project = projects[i];
                project.AddPackageToAllFrameworks(packageXWithNoWarn);
                project.Save();
            }

            for (var i = 0; i < projects.Count() - 1; i++)
            {
                var projectA = projects[i];
                for (var j = i + 1; j < projects.Count(); j++)
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

        private static void ClearPath(string path)
        {
            if (Directory.Exists(path))
            {
                var di = new DirectoryInfo(path);

                foreach (var file in di.GetFiles())
                {
                    file.Delete();
                }
                foreach (var dir in di.GetDirectories())
                {
                    dir.Delete(true);
                }

                di.Delete();
            }
        }
    }
}
