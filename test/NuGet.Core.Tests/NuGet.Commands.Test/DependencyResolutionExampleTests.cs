// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#if DEBUG
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    /// <summary>
    /// These tests represent the scenarios contained in the dependency resolution description document.
    /// These scenarios are tested in other tests as well, but it is incredibly convenient to have these tests matching the exactly documented scenarios.
    /// </summary>
    public class DependencyResolutionExampleTests
    {
        /// <summary>
        /// Project -> A 1.0.0 -> B 1.0.0
        ///            B 2.0.0
        /// </summary>
        [Fact]
        public async Task DirectDependencyWins1()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            var project1Json = @"
                {
                  ""version"": ""1.0.0"",
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                            ""A"": ""1.0.0"",
                            ""B"": ""2.0.0""
                        }
                    }
                  }
                }";

            var A = new SimpleTestPackageContext("A", "1.0.0");
            var B100 = new SimpleTestPackageContext("B", "1.0.0");
            var B200 = new SimpleTestPackageContext("B", "2.0.0");

            A.Dependencies.Add(B100);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                A,
                B100,
                B200
                );

            // set up the project
            var spec = ProjectTestHelpers.GetPackageSpecWithProjectNameAndSpec("project", pathContext.SolutionRoot, project1Json);
            var request = ProjectTestHelpers.CreateRestoreRequest(spec, pathContext, new TestLogger());
            var command = new RestoreCommand(request);

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue();
            result.LogMessages.Should().HaveCount(0);
            result.LockFile.Libraries.Count.Should().Be(2);
            result.LockFile.Libraries.Single(e => e.Name.Equals("B")).Version.ToString().Should().Be("2.0.0");
            string[] files = Directory.GetFiles(pathContext.UserPackagesFolder, "*.nupkg", SearchOption.AllDirectories);
            files.Should().HaveCount(2);
        }

        /// <summary>
        /// Project -> A 1.0.0 -> B 1.0.0 -> C 1.0.0
        ///                       C 2.0.0
        /// </summary>
        [Fact]
        public async Task DirectDependencyWins2()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            var project1Json = @"
                {
                  ""version"": ""1.0.0"",
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                            ""A"": ""1.0.0"",
                        }
                    }
                  }
                }";

            var A = new SimpleTestPackageContext("A", "1.0.0");
            var B100 = new SimpleTestPackageContext("B", "1.0.0");
            var C100 = new SimpleTestPackageContext("C", "1.0.0");
            var C200 = new SimpleTestPackageContext("C", "2.0.0");

            A.Dependencies.Add(B100);
            A.Dependencies.Add(C200);
            B100.Dependencies.Add(C100);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                A,
                B100,
                C100,
                C200
                );

            // set up the project
            var spec = ProjectTestHelpers.GetPackageSpecWithProjectNameAndSpec("project", pathContext.SolutionRoot, project1Json);
            var request = ProjectTestHelpers.CreateRestoreRequest(spec, pathContext, new TestLogger());
            var command = new RestoreCommand(request);

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue();
            result.LogMessages.Should().HaveCount(0);
            result.LockFile.Libraries.Count.Should().Be(3);
            result.LockFile.Libraries.Single(e => e.Name.Equals("C")).Version.ToString().Should().Be("2.0.0");
            string[] files = Directory.GetFiles(pathContext.UserPackagesFolder, "*.nupkg", SearchOption.AllDirectories);
            files.Should().HaveCount(3);
        }

        /// <summary>
        /// Project -> A 1.0.0 -> B 1.0.0 -> C 2.0.0
        ///                       C 1.0.0
        /// </summary>
        [Fact]
        public async Task DirectDependencyWins3()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            var project1Json = @"
                {
                  ""version"": ""1.0.0"",
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                            ""A"": ""1.0.0"",
                        }
                    }
                  }
                }";

            var A = new SimpleTestPackageContext("A", "1.0.0");
            var B100 = new SimpleTestPackageContext("B", "1.0.0");
            var C100 = new SimpleTestPackageContext("C", "1.0.0");
            var C200 = new SimpleTestPackageContext("C", "2.0.0");

            A.Dependencies.Add(B100);
            A.Dependencies.Add(C100);
            B100.Dependencies.Add(C200);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                A,
                B100,
                C100,
                C200
                );

            // set up the project
            var spec = ProjectTestHelpers.GetPackageSpecWithProjectNameAndSpec("project", pathContext.SolutionRoot, project1Json);
            var request = ProjectTestHelpers.CreateRestoreRequest(spec, pathContext, new TestLogger());
            var command = new RestoreCommand(request);

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue();
            result.LogMessages.Should().HaveCount(1);
            result.LogMessages[0].Code.Should().Be(NuGetLogCode.NU1605);
            result.LogMessages[0].AsRestoreLogMessage().LibraryId.Should().Be("C");
            result.LockFile.Libraries.Count.Should().Be(3);
            result.LockFile.Libraries.Single(e => e.Name.Equals("C")).Version.ToString().Should().Be("1.0.0");
            string[] files = Directory.GetFiles(pathContext.UserPackagesFolder, "*.nupkg", SearchOption.AllDirectories);
            files.Should().HaveCount(3);
        }

        /// <summary>
        /// Project -> A 1.0.0 -> B 1.0.0 -> C 2.0.0
        ///                       C 1.0.0
        /// Project -> C 3.0.0
        /// </summary>
        [Fact]
        public async Task DirectDependencyWins4()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            var project1Json = @"
                {
                  ""version"": ""1.0.0"",
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                            ""A"": ""1.0.0"",
                            ""C"": ""3.0.0"",
                        }
                    }
                  }
                }";

            var A = new SimpleTestPackageContext("A", "1.0.0");
            var B100 = new SimpleTestPackageContext("B", "1.0.0");
            var C100 = new SimpleTestPackageContext("C", "1.0.0");
            var C200 = new SimpleTestPackageContext("C", "2.0.0");
            var C300 = new SimpleTestPackageContext("C", "3.0.0");

            A.Dependencies.Add(B100);
            A.Dependencies.Add(C100);
            B100.Dependencies.Add(C200);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                A,
                B100,
                C100,
                C200,
                C300
                );

            // set up the project
            var spec = ProjectTestHelpers.GetPackageSpecWithProjectNameAndSpec("project", pathContext.SolutionRoot, project1Json);
            var request = ProjectTestHelpers.CreateRestoreRequest(spec, pathContext, new TestLogger());
            var command = new RestoreCommand(request);

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue();
            result.LogMessages.Should().HaveCount(0);
            result.LockFile.Libraries.Count.Should().Be(3);
            result.LockFile.Libraries.Single(e => e.Name.Equals("C")).Version.ToString().Should().Be("3.0.0");
            string[] files = Directory.GetFiles(pathContext.UserPackagesFolder, "*.nupkg", SearchOption.AllDirectories);
            files.Should().HaveCount(3);
        }

        /// <summary>
        /// Project -> A 1.0.0 -> B 1.0.0
        /// Project -> C 2.0.0 -> B 2.0.0
        /// </summary>
        [Fact]
        public async Task CousinDependencies1()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            var project1Json = @"
                {
                  ""version"": ""1.0.0"",
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                            ""A"": ""1.0.0"",
                            ""C"": ""2.0.0"",
                        }
                    }
                  }
                }";

            var A = new SimpleTestPackageContext("A", "1.0.0");
            var B100 = new SimpleTestPackageContext("B", "1.0.0");
            var B200 = new SimpleTestPackageContext("B", "2.0.0");
            var C200 = new SimpleTestPackageContext("C", "2.0.0");

            A.Dependencies.Add(B100);
            C200.Dependencies.Add(B200);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                A,
                B100,
                B200,
                C200
                );

            // set up the project
            var spec = ProjectTestHelpers.GetPackageSpecWithProjectNameAndSpec("project", pathContext.SolutionRoot, project1Json);
            var request = ProjectTestHelpers.CreateRestoreRequest(spec, pathContext, new TestLogger());
            var command = new RestoreCommand(request);

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue();
            result.LogMessages.Should().HaveCount(0);
            result.LockFile.Libraries.Count.Should().Be(3);
            result.LockFile.Libraries.Single(e => e.Name.Equals("B")).Version.ToString().Should().Be("2.0.0");
            string[] files = Directory.GetFiles(pathContext.UserPackagesFolder, "*.nupkg", SearchOption.AllDirectories);
            files.Should().HaveCount(4);
        }

        /// <summary>
        /// Project -> A 1.0.0 -> B 1.0.0 -> D 3.0.0
        /// Project -> C 2.0.0 -> D 2.0.0
        /// </summary>
        [Fact]
        public async Task CousinDependencies2()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            var project1Json = @"
                {
                  ""version"": ""1.0.0"",
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                            ""A"": ""1.0.0"",
                            ""C"": ""2.0.0"",
                        }
                    }
                  }
                }";

            var A = new SimpleTestPackageContext("A", "1.0.0");
            var B100 = new SimpleTestPackageContext("B", "1.0.0");
            var C200 = new SimpleTestPackageContext("C", "2.0.0");
            var D200 = new SimpleTestPackageContext("D", "2.0.0");
            var D300 = new SimpleTestPackageContext("D", "3.0.0");

            A.Dependencies.Add(B100);
            C200.Dependencies.Add(D200);
            B100.Dependencies.Add(D300);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                A,
                B100,
                C200,
                D200,
                D300
                );

            // set up the project
            var spec = ProjectTestHelpers.GetPackageSpecWithProjectNameAndSpec("project", pathContext.SolutionRoot, project1Json);
            var request = ProjectTestHelpers.CreateRestoreRequest(spec, pathContext, new TestLogger());
            var command = new RestoreCommand(request);

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeTrue();
            result.LogMessages.Should().HaveCount(0);
            result.LockFile.Libraries.Count.Should().Be(4);
            result.LockFile.Libraries.Single(e => e.Name.Equals("D")).Version.ToString().Should().Be("3.0.0");
            string[] files = Directory.GetFiles(pathContext.UserPackagesFolder, "*.nupkg", SearchOption.AllDirectories);
            files.Should().HaveCount(5);
        }

        /// <summary>
        /// Project -> A 1.0.0 = B 1.0.0
        /// Project -> C 2.0.0 = B 2.0.0
        /// </summary>
        [Fact]
        public async Task CousinDependencies3()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            var project1Json = @"
                {
                  ""version"": ""1.0.0"",
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                            ""A"": ""1.0.0"",
                            ""C"": ""2.0.0"",
                        }
                    }
                  }
                }";

            var A = new SimpleTestPackageContext("A", "1.0.0");
            var B100 = new SimpleTestPackageContext("B", "1.0.0");
            var B200 = new SimpleTestPackageContext("B", "2.0.0");
            var C200 = new SimpleTestPackageContext("C", "2.0.0");

            A.Dependencies.Add(new SimpleTestPackageContext("B", "[1.0.0]")); // TODO NK - Add a range dependency
            C200.Dependencies.Add(new SimpleTestPackageContext("B", "[2.0.0]"));

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                A,
                B100,
                B200,
                C200
                );

            // set up the project
            var spec = ProjectTestHelpers.GetPackageSpecWithProjectNameAndSpec("project", pathContext.SolutionRoot, project1Json);
            var request = ProjectTestHelpers.CreateRestoreRequest(spec, pathContext, new TestLogger());
            var command = new RestoreCommand(request);

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            result.Success.Should().BeFalse();
            result.LogMessages.Should().HaveCount(1);
            result.LockFile.Libraries.Count.Should().Be(2);
            result.LockFile.Libraries.Single(e => e.Name.Equals("B")).Version.ToString().Should().Be("2.0.0");
            string[] files = Directory.GetFiles(pathContext.UserPackagesFolder, "*.nupkg", SearchOption.AllDirectories);
            files.Should().HaveCount(2);
        }
    }
}
#endif