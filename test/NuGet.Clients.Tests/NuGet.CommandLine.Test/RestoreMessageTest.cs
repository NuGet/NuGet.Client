// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreMessageTest
    {
        [Fact]
        public void GivenAProjectIsUsedOverAPackageVerifyNoDowngradeWarning()
        {
            DebuggerUtils.WaitForDebugger();
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                var packageX = new SimpleTestPackageContext("x", "9.0.0");
                var packageB = new SimpleTestPackageContext("b", "9.0.0");
                packageX.Dependencies.Add(packageB);

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX, packageB);

                // PackageB will be overridden by ProjectB
                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.Restore(pathContext, projectA.ProjectPath);
                var output = r.Item2 + " " + r.Item3;

                var reader = new LockFileFormat();
                var lockFileObj = reader.Read(projectA.AssetsFileOutputPath);
                var logMessage = lockFileObj?.LogMessages?.First();


                // Assert
                Assert.NotNull(lockFileObj);
                Assert.NotNull(logMessage);
                Assert.Equal(1, lockFileObj.LogMessages.Count());
                Assert.Equal(LogLevel.Error, logMessage.Level);
                Assert.Equal(NuGetLogCode.NU1000, logMessage.Code);
                Assert.Null(logMessage.FilePath);
                Assert.Equal(-1, logMessage.StartLineNumber);
                Assert.Equal(-1, logMessage.EndLineNumber);
                Assert.Equal(-1, logMessage.StartColumnNumber);
                Assert.Equal(-1, logMessage.EndColumnNumber);
                Assert.NotNull(logMessage.TargetGraphs);
                Assert.Equal(0, logMessage.TargetGraphs.Count);
                Assert.Equal("test log message", logMessage.Message);
                // Assert
                Assert.DoesNotContain("downgrade", output, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void GivenAPackageDowngradeVerifyDowngradeWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                var packageB = new SimpleTestPackageContext("b", "9.0.0");
                var packageI1 = new SimpleTestPackageContext("i", "9.0.0");
                var packageI2 = new SimpleTestPackageContext("i", "1.0.0");
                packageB.Dependencies.Add(packageI1);

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageB, packageI1, packageI2);

                projectA.AddPackageToAllFrameworks(packageB);
                projectA.AddPackageToAllFrameworks(packageI2);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.Restore(pathContext, projectA.ProjectPath);
                var output = r.Item2 + " " + r.Item3;

                // Assert
                Assert.Contains("Detected package downgrade: i from 9.0.0 to 1.0.0", output, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void GivenAPackageWithAHigherMinClientVersionVerifyErrorCodeDisplayed()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                var packageB = new SimpleTestPackageContext("b", "1.0.0")
                {
                    MinClientVersion = "99.0.0"
                };

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageB);

                projectA.AddPackageToAllFrameworks(packageB);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.Restore(pathContext, projectA.ProjectPath, expectedExitCode: 1);
                var output = r.Item2 + " " + r.Item3;

                // Assert
                Assert.Contains("NU1901", output, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
