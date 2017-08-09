// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using NuGet.Frameworks;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreTransitiveLoggingTests
    {
        [Fact]
        // Tests ProjA -> ProjB[PkgX NoWarn NU1603] -> PkgX[NU1603]
        public void GivenAProjectReferenceNoWarnsVerifyNoWarning()
        {

            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
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

                // B -> X
                projectB.AddPackageToAllFrameworks(packageX);
                projectB.Save();

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        // Tests ProjA -> ProjB[ProjectWide NoWarn NU1603] -> PkgX[NU1603]
        public void GivenAProjectReferenceNoWarnsProjectWideVerifyNoWarning()
        {

            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX11 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.1"
                };

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX11);

                // B -> X
                projectB.AddPackageToAllFrameworks(packageX);
                projectB.Properties.Add("NoWarn", "NU1603");
                projectB.Save();

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        // Tests ProjA -> ProjB[PkgX NoWarn NU1603] -> PkgX[NU1603]
        public void GivenAProjectReferenceWithDifferentFrameworkNoWarnsVerifyNoWarning()
        {

            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp1.1"));

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
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

                // B -> X
                projectB.AddPackageToAllFrameworks(packageX);
                projectB.Save();

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        // Tests ProjA -> ProjB -> ProjC[PkgX NoWarn NU1603] -> PkgX[NU1603]
        public void GivenATransitiveProjectReferenceNoWarnsVerifyNoWarning()
        {

            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
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

                // C -> X
                projectC.AddPackageToAllFrameworks(packageX);
                projectC.Save();

                // B -> C
                projectB.AddProjectToAllFrameworks(projectC);
                projectB.Save();

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        // Tests ProjA -> ProjB -> ProjC[ProjectWide NoWarn NU1603] -> PkgX[NU1603]
        public void GivenATransitiveProjectReferenceNoWarnsProjectWideVerifyNoWarning()
        {

            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
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

                // C -> X
                projectC.AddPackageToAllFrameworks(packageX);
                projectC.Properties.Add("NoWarn", "NU1603");
                projectC.Save();

                // B -> C
                projectB.AddProjectToAllFrameworks(projectC);
                projectB.Save();

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        // Tests ProjA -> ProjB[PkgX NoWarn NU1603] -> PkgX[NU1603]
        //             -> PkgX[NU1603]
        public void GivenAProjectReferenceNoWarnsButDirectReferenceGeneratesWarningVerifyWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                // Referenced but not created
                var packageXWithNoWarn = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    NoWarn = "NU1603"
                };

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX11 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.1"
                };

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX11);

                // B -> X
                projectB.AddPackageToAllFrameworks(packageXWithNoWarn);
                projectB.Save();

                // A -> B
                // A -> X
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.AddPackageToAllFrameworks(packageX);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().Contain("NU1603");
            }
        }

        [Fact]
        // Tests ProjA -> ProjB[ProjectWide NoWarn NU1603] -> PkgX[NU1603]
        //             -> PkgX[NU1603]
        public void GivenAProjectReferenceNoWarnsProjectWideButDirectReferenceGeneratesWarningVerifyWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX11 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.1"
                };

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX11);

                // B -> X
                projectB.AddPackageToAllFrameworks(packageX);
                projectB.Properties.Add("NoWarn", "NU1603");
                projectB.Save();

                // A -> B
                // A -> X
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.AddPackageToAllFrameworks(packageX);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().Contain("NU1603");
            }
        }

        [Fact]
        // Tests ProjA -> ProjB[PkgX NoWarn NU1603] -> PkgX[NU1603]
        //             -> ProjC[PkgX NoWarn NU1603] -> PkgX[NU1603]
        public void GivenMultipleProjectReferencesNoWarnVerifyNoWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

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

                // B -> X
                projectB.AddPackageToAllFrameworks(packageXWithNoWarn);
                projectB.Save();

                // C -> X
                projectC.AddPackageToAllFrameworks(packageXWithNoWarn);
                projectC.Save();

                // A -> B
                // A -> C
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.AddProjectToAllFrameworks(projectC);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        // Tests ProjA -> ProjB[ProjectWide NoWarn NU1603] -> PkgX[NU1603]
        //             -> ProjC[ProjectWide NoWarn NU1603] -> PkgX[NU1603]
        public void GivenMultipleProjectReferencesNoWarnProjectWideVerifyNoWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX11 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.1"
                };

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX11);

                // B -> X
                projectB.AddPackageToAllFrameworks(packageX);
                projectB.Properties.Add("NoWarn", "NU1603");
                projectB.Save();

                // C -> X
                projectC.AddPackageToAllFrameworks(packageX);
                projectC.Properties.Add("NoWarn", "NU1603");
                projectC.Save();

                // A -> B
                // A -> C
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.AddProjectToAllFrameworks(projectC);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        // Tests ProjA -> ProjB[ProjectWide NoWarn NU1603] -> PkgX[NU1603]
        //             -> ProjC[ProjectWide NoWarn NU1605] -> PkgX[NU1603]
        public void GivenMultipleProjectReferencesNoWarnDifferentWarningsProjectWideVerifyWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX11 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.1"
                };

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX11);

                // B -> X
                projectB.AddPackageToAllFrameworks(packageX);
                projectB.Properties.Add("NoWarn", "NU1603");
                projectB.Save();

                // C -> X
                projectC.AddPackageToAllFrameworks(packageX);
                projectC.Properties.Add("NoWarn", "NU1605");
                projectC.Save();

                // A -> B
                // A -> C
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.AddProjectToAllFrameworks(projectC);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.GetSubstringCount("NU1603", ignoreCase: false).Should().Be(3);
            }
        }

        [Fact]
        // Tests ProjA -> ProjB[ProjectWide NoWarn NU1603] -> PkgX[NU1603]
        //             -> ProjC[PkgX NoWarn NU1603]        -> PkgX[NU1603]
        public void GivenMultipleProjectReferencesNoWarnMixedVerifyNoWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                // Referenced but not created
                var packageXWithNoWarn = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    NoWarn = "NU1603"
                };

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };


                // Created in the source
                var packageX11 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.1"
                };

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX11);

                // B -> X
                projectB.AddPackageToAllFrameworks(packageX);
                projectB.Properties.Add("NoWarn", "NU1603");
                projectB.Save();

                // C -> X
                projectC.AddPackageToAllFrameworks(packageXWithNoWarn);
                projectC.Save();

                // A -> B
                // A -> C
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.AddProjectToAllFrameworks(projectC);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        // Tests ProjA -> ProjB[PkgX NoWarn NU1603] -> PkgX[NU1603]
        //             -> ProjC                     -> PkgX[NU1603]
        public void GivenMultipleProjectReferencesAndOnePathWarnsVerifyNoWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                // Referenced but not created
                var packageXWithNoWarn = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    NoWarn = "NU1603"
                };

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX11 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.1"
                };

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX11);

                // B -> X
                projectB.AddPackageToAllFrameworks(packageXWithNoWarn);
                projectB.Save();

                // C -> X
                projectC.AddPackageToAllFrameworks(packageX);
                projectC.Save();

                // A -> B
                // A -> C
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.AddProjectToAllFrameworks(projectC);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().Contain("NU1603");
            }
        }

        [Fact]
        // Tests ProjA -> ProjB[PkgX NoWarn NU1603] -> PkgX[NU1603]
        //             -> ProjC[PkgX NoWarn NU1603] -> PkgX[NU1603]
        //             -> PkgX[NU1603]
        public void GivenMultipleProjectReferencesNoWarnButDirectReferenceWarnsVerifyWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                // Referenced but not created
                var packageXWithNoWarn = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    NoWarn = "NU1603"
                };

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX11 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.1"
                };

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX11);

                // B -> X
                projectB.AddPackageToAllFrameworks(packageXWithNoWarn);
                projectB.Save();

                // C -> X
                projectC.AddPackageToAllFrameworks(packageXWithNoWarn);
                projectC.Save();

                // A -> B
                // A -> C
                // A -> X
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.AddProjectToAllFrameworks(projectC);
                projectA.AddPackageToAllFrameworks(packageX);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.GetSubstringCount("NU1603", ignoreCase: false).Should().Be(1);
            }
        }

        [Fact]
        // Tests ProjA[PkgX NoWarn NU1603] -> ProjB -> PkgX[NU1603]
        //                                 -> ProjC -> PkgX[NU1603]
        //                                 -> PkgX[NU1603]
        public void GivenMultipleProjectReferencesWarnButDirectReferenceNoWarnsVerifyNoWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                // Referenced but not created
                var packageXWithNoWarn = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    NoWarn = "NU1603"
                };

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX11 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.1"
                };

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX11);

                // B -> X
                projectB.AddPackageToAllFrameworks(packageX);
                projectB.Save();

                // C -> X
                projectC.AddPackageToAllFrameworks(packageX);
                projectC.Save();

                // A -> B
                // A -> C
                // A -> X
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.AddProjectToAllFrameworks(projectC);
                projectA.AddPackageToAllFrameworks(packageXWithNoWarn);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.GetSubstringCount("NU1603", ignoreCase: false).Should().Be(2);
            }
        }

        [Fact]
        // Tests ProjA -> ProjB[PkgX NoWarn NU1603] -> PkgX[NU1603]
        //             -> ProjC                     -> ProjB[PkgX NoWarn NU1603] -> PkgX[NU1603]
        public void GivenSinglePointOfReferenceNoWarnsVerifyNoWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

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

                // B -> X
                projectB.AddPackageToAllFrameworks(packageXWithNoWarn);
                projectB.Save();

                // C -> B
                projectC.AddProjectToAllFrameworks(projectB);
                projectC.Save();

                // A -> B
                // A -> C
                // A -> X
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.AddProjectToAllFrameworks(projectC);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        // Tests ProjA -> ProjB[PkgX NoWarn NU1603] -> PkgX[NU1603]
        //             -> ProjC[PkgX NoWarn NU1603] -> PkgX[NU1603]
        //             -> ProjD                     -> ProjE        -> ProjF -> PkgX[NU1603]
        public void GivenOneLongPathWarnsVerifyWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectD = SimpleTestProjectContext.CreateNETCore(
                    "d",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectE = SimpleTestProjectContext.CreateNETCore(
                    "e",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));

                var projectF = SimpleTestProjectContext.CreateNETCore(
                    "f",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp2.0"));


                // Referenced but not created
                var packageXWithNoWarn = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    NoWarn = "NU1603"
                };

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Created in the source
                var packageX11 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.1"
                };

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX11);

                // B -> X
                projectB.AddPackageToAllFrameworks(packageXWithNoWarn);
                projectB.Save();

                // C -> B
                projectC.AddPackageToAllFrameworks(packageXWithNoWarn);
                projectC.Save();

                // F -> X
                projectF.AddPackageToAllFrameworks(packageX);
                projectF.Save();

                // E -> F
                projectE.AddProjectToAllFrameworks(projectF);
                projectE.Save();

                // D -> E
                projectD.AddProjectToAllFrameworks(projectE);
                projectD.Save();

                // A -> B
                // A -> C
                // A -> D
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.AddProjectToAllFrameworks(projectC);
                projectA.AddProjectToAllFrameworks(projectD);
                projectA.Save();

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Projects.Add(projectD);
                solution.Projects.Add(projectE);
                solution.Projects.Add(projectF);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().Contain("NU1603");
            }
        }
    }
}
