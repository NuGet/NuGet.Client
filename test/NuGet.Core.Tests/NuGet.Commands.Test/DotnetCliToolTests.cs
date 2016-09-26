using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class DotnetCliToolTests
    {
        [Fact]
        public async Task DotnetCliTool_BasicToolRestore()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var dgFile = new DependencyGraphSpec();

                var spec = ToolRestoreUtility.GetSpec(
                    Path.Combine(pathContext.SolutionRoot, "fake.csproj"),
                    "a",
                    VersionRange.Parse("1.0.0"),
                    NuGetFramework.Parse("netcoreapp1.0"));

                dgFile.AddProject(spec);
                dgFile.AddRestore(spec.Name);

                var pathResolver = new ToolPathResolver(pathContext.UserPackagesFolder);
                var path = pathResolver.GetLockFilePath(
                    "a",
                    NuGetVersion.Parse("1.0.0"),
                    NuGetFramework.Parse("netcoreapp1.0"));

                await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, new PackageIdentity("a", NuGetVersion.Parse("1.0.0")));

                // Act
                var result = await CommandsTestUtility.RunSingleRestore(dgFile, pathContext, logger);

                // Assert
                Assert.True(result.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                Assert.True(File.Exists(path));
            }
        }

        [Fact]
        public async Task DotnetCliTool_BasicToolRestore_WithDuplicates()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var dgFile = new DependencyGraphSpec();

                for (int i = 0; i < 10; i++)
                {
                    var spec = ToolRestoreUtility.GetSpec(
                        Path.Combine(pathContext.SolutionRoot, "fake.csproj"),
                        "a",
                        VersionRange.Parse("1.0.0"),
                        NuGetFramework.Parse("netcoreapp1.0"));

                    dgFile.AddProject(spec);
                    dgFile.AddRestore(spec.Name);
                }

                var pathResolver = new ToolPathResolver(pathContext.UserPackagesFolder);
                var path = pathResolver.GetLockFilePath(
                    "a",
                    NuGetVersion.Parse("1.0.0"),
                    NuGetFramework.Parse("netcoreapp1.0"));

                await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, new PackageIdentity("a", NuGetVersion.Parse("1.0.0")));

                // Act
                var results = await CommandsTestUtility.RunRestore(dgFile, pathContext, logger);

                // Assert
                // This should have been de-duplicated
                Assert.Equal(1, results.Count);
                var result = results.Single();

                Assert.True(result.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                Assert.True(File.Exists(path));
            }
        }
    }
}
