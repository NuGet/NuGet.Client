// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreLoggingTests
    {
        [Fact]
        public async Task RestoreLogging_VerifyWarningsAsErrorsFailsRestore()
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
                r.Output.Should().Contain("NU1603");
            }
        }

        [Fact]
        public async Task RestoreLogging_VerifyNoWarnRemovesWarning()
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
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);

                // Assert
                r.Success.Should().BeTrue();
                r.Output.Should().NotContain("NU1603");
            }
        }

        [Fact]
        public async Task RestoreLogging_VerifyWarningsAsErrorsForSpecificWarningFails()
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
                projectA.Properties.Add("WarningsAsErrors", "NU1603");

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
                r.Output.Should().Contain("NU1603");
            }
        }

        [Fact]
        public async Task RestoreLogging_VerifyWarningsAsErrorsForSpecificWarningOfAnotherTypeIgnored()
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
                projectA.Properties.Add("WarningsAsErrors", "NU1602");

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
                r.Success.Should().BeTrue();
                r.Output.Should().Contain("NU1603");
            }
        }
    }
}
