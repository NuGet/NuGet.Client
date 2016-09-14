using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class TestUtilityTests
    {
        [Fact]
        public async Task TestUtility_Solution()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange && Act
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Assert
                Assert.True(File.Exists(Path.Combine(pathContext.SolutionRoot, "solution.sln")));
                Assert.True(File.Exists(Path.Combine(pathContext.SolutionRoot, "a", "a.csproj")));
            }
        }
    }
}
