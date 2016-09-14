using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreNetCoreTest
    {
        [Fact]
        public async Task RestoreNetCore_SingleProject()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot.FullName);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot.FullName,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot.FullName);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource.FullName,
                    PackageSaveMode.Defaultv3,
                    packageX);

                string[] args = new string[] {
                    "restore",
                    pathContext.SolutionRoot.FullName,
                    "-Verbosity",
                    "detailed"
                };

                var nugetexe = Util.GetNuGetExePath();

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    pathContext.WorkingDirectory.Path,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.Item2);
                Assert.False(File.Exists(projectA.TargetsOutput), r.Item2);
                Assert.False(File.Exists(projectA.PropsOutput), r.Item2);
            }
        }
    }
}
