// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Frameworks;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreLoggingTests
    {
        [Fact]
        public async Task RestoreLogging_WarningsContainNuGetLogCodes()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX9 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "9.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    packageX9);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().Contain("WARNING: NU1603:");
            }
        }

        [Fact]
        public async Task RestoreLogging_WarningsAsErrorsFailsRestore()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("TreatWarningsAsErrors", "true");

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX9 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "9.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    packageX9);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);

                // Assert
                r.Success.Should().BeFalse();
                r.AllOutput.Should().Contain("NU1603");
            }
        }

        [Theory]
        [InlineData("NU1603")]
        [InlineData("$(NoWarn);NU1603")]
        [InlineData("NU1603;$(NoWarn);")]
        [InlineData("NU1603;NU1701")]
        [InlineData("NU1603,NU1701")]
        public async Task RestoreLogging_NoWarnRemovesWarning(string noWarn)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("NoWarn", noWarn);

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX9 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "9.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    packageX9);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.Output.Should().NotContain("NU1603");
            }
        }

        [Theory]
        [InlineData("NU1603")]
        [InlineData("$(NoWarn);NU1603")]
        [InlineData("NU1603;$(NoWarn);")]
        [InlineData("NU1603;NU1701")]
        [InlineData("NU1603,NU1701")]
        public async Task RestoreLogging_WarningsAsErrorsForSpecificWarningFails(string warnAsError)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("TreatWarningsAsErrors", "false");
                projectA.Properties.Add("WarningsAsErrors", warnAsError);

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX9 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "9.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    packageX9);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);

                // Assert
                r.Success.Should().BeFalse();
                r.AllOutput.Should().Contain("NU1603");
            }
        }

        [Fact]
        public async Task RestoreLogging_WarningsAsErrorsForSpecificWarningOfAnotherTypeIgnored()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("TreatWarningsAsErrors", "false");
                projectA.Properties.Add("WarningsAsErrors", "NU1602;NU1701");

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX9 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "9.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    packageX9);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.Output.Should().Contain("NU1603");
                r.Output.Should().NotContain("NU1602");
                r.Output.Should().NotContain("NU1701");
            }
        }

        [Fact]
        public async Task RestoreLogging_NoWarnWithWarnAsErrorRemovesWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("TreatWarningsAsErrors", "true");
                projectA.Properties.Add("NoWarn", "NU1603");

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX9 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "9.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    packageX9);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.Output.Should().NotContain("NU1603");
            }
        }

        [Fact]
        public async Task RestoreLogging_NoWarnWithWarnSpecificAsErrorRemovesWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("WarningsAsErrors", "NU1603");
                projectA.Properties.Add("NoWarn", "NU1603");

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX9 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "9.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    packageX9);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.Output.Should().NotContain("NU1603");
            }
        }

        [Fact]
        public async Task RestoreLogging_PackageSpecificNoWarnRemovesWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);                

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    NoWarn = "NU1603"
                };

                // Created in the source
                var packageX9 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "9.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    packageX9);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        public async Task RestoreLogging_PackageSpecificDifferentNoWarnDonesNotRemoveWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    NoWarn = "NU1607"
                };

                // Created in the source
                var packageX9 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "9.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    packageX9);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().Contain("NU1603");
            }
        }


        [Fact]
        public async Task RestoreLogging_PackageSpecificNoWarnAndTreatWarningsAsErrors()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("TreatWarningsAsErrors", "true");

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    NoWarn = "NU1603"
                };

                // Created in the source
                var packageX9 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "9.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    packageX9);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        public async Task RestoreLogging_PackageSpecificNoWarnAndTreatSpecificWarningsAsErrors()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("WarningsAsErrors", "NU1603");

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    NoWarn = "NU1603"
                };

                // Created in the source
                var packageX9 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "9.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    packageX9);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1603");
            }
        }
    }
}
