// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreLoggingTests
    {
        [Fact]
        public void RestoreLogging_VerifyNU1605DowngradeWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp1 = NuGetFramework.Parse("netcoreapp1.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp1);

                var packageZ1 = new SimpleTestPackageContext("z", "1.5.0");
                var packageZ2 = new SimpleTestPackageContext("z", "2.0.0");
                var packageX = new SimpleTestPackageContext("x", "1.0.0");
                packageX.Dependencies.Add(packageZ2);

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageX, packageZ1, packageZ2, packageZ1);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var doc = projectA.GetXML();

                // z 1.*
                ProjectFileUtils.AddItem(doc,
                    "PackageReference", "z",
                    NuGetFramework.Parse("netcoreapp1.0"),
                    new Dictionary<string, string>(),
                    new Dictionary<string, string>() { { "Version", "1.*" } });

                // x *
                ProjectFileUtils.AddItem(doc,
                    "PackageReference", "x",
                    NuGetFramework.Parse("netcoreapp1.0"),
                    new Dictionary<string, string>(),
                    new Dictionary<string, string>() { { "Version", "*" } });

                doc.Save(projectA.ProjectPath);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
                var message = projectA.AssetsFile.LogMessages.Single(e => e.Code == NuGetLogCode.NU1605);
                message.Level.Should().Be(LogLevel.Warning);

                // Verify message contains the actual 1.5.0 version instead of the lower bound of 1.0.0.
                message.Message.Should().Contain("Detected package downgrade: z from 2.0.0 to 1.5.0. Reference the package directly from the project to select a different version.");

                // Verify that x display the version instead of the range which is >= 0.0.0
                message.Message.Should().Contain("a -> x 1.0.0 -> z (>= 2.0.0)");

                // Verify non-snapshot range is displayed for the downgradedBy path.
                message.Message.Should().Contain("a -> z (>= 1.0.0)");
            }
        }

        [Fact]
        public async Task RestoreLogging_VerifyNU1608Message()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp1 = NuGetFramework.Parse("netcoreapp1.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp1);

                var packageX = new SimpleTestPackageContext("x", "1.0.0")
                {
                    Nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>x</id>
                            <version>1.0.0</version>
                            <title />
                            <dependencies>
                                <group>
                                    <dependency id=""z"" version=""[1.0.0]"" />
                                </group>
                            </dependencies>
                        </metadata>
                        </package>")
                };

                var packageZ1 = new SimpleTestPackageContext("z", "1.0.0");
                var packageZ2 = new SimpleTestPackageContext("z", "2.0.0");

                await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, packageX, packageZ1, packageZ2);

                projectA.AddPackageToAllFrameworks(packageX);
                projectA.AddPackageToAllFrameworks(packageZ2);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);
                var log = projectA.AssetsFile.LogMessages.SingleOrDefault(e => e.Code == NuGetLogCode.NU1608);

                // Assert
                r.Success.Should().BeTrue();
                r.AllOutput.Should().NotContain("NU1107");
                r.AllOutput.Should().Contain("NU1608");
                log.FilePath.Should().Be(projectA.ProjectPath);
                log.LibraryId.Should().Be("z");
                log.Level.Should().Be(LogLevel.Warning);
                log.TargetGraphs.Select(e => string.Join(",", e)).Should().Contain(netcoreapp1.DotNetFrameworkName);
                log.Message.Should().Contain("Detected package version outside of dependency constraint: x 1.0.0 requires z (= 1.0.0) but version z 2.0.0 was resolved.");
            }
        }

        [Fact]
        public async Task RestoreLogging_VerifyNU1107DoesNotDisplayNU1608Also()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp1 = NuGetFramework.Parse("netcoreapp1.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp1);
                projectA.Properties.Add("WarningsAsErrors", "NU1608");

                var packageX = new SimpleTestPackageContext("x", "1.0.0")
                {
                    Nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>x</id>
                            <version>1.0.0</version>
                            <title />
                            <dependencies>
                                <group>
                                    <dependency id=""z"" version=""[1.0.0]"" />
                                </group>
                            </dependencies>
                        </metadata>
                        </package>")
                };

                var packageY = new SimpleTestPackageContext("y", "1.0.0")
                {
                    Nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>y</id>
                            <version>1.0.0</version>
                            <title />
                            <dependencies>
                                <group>
                                    <dependency id=""z"" version=""[2.0.0]"" />
                                </group>
                            </dependencies>
                        </metadata>
                        </package>")
                };

                var packageZ1 = new SimpleTestPackageContext("z", "1.0.0");
                var packageZ2 = new SimpleTestPackageContext("z", "2.0.0");

                await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, packageX, packageY, packageZ1, packageZ2);

                projectA.AddPackageToAllFrameworks(packageX);
                projectA.AddPackageToAllFrameworks(packageY);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);

                // Assert
                r.Success.Should().BeFalse();
                r.AllOutput.Should().Contain("NU1107");
                r.AllOutput.Should().NotContain("NU1608");
            }
        }

        [Fact]
        public void RestoreLogging_VerifyCompatErrorNU1201Properties()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp1 = NuGetFramework.Parse("netcoreapp1.0");
                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp1);

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);
                var log = projectA.AssetsFile.LogMessages.SingleOrDefault(e => e.Code == NuGetLogCode.NU1201 && e.TargetGraphs.All(g => !g.Contains("/")));

                // Assert
                r.Success.Should().BeFalse();
                r.AllOutput.Should().Contain("NU1201");
                log.FilePath.Should().Be(projectA.ProjectPath);
                log.LibraryId.Should().Be("b");
                log.Level.Should().Be(LogLevel.Error);
                log.TargetGraphs.ShouldBeEquivalentTo(new[] { netcoreapp1.DotNetFrameworkName });
                log.Message.Should().Be("Project b is not compatible with netcoreapp1.0 (.NETCoreApp,Version=v1.0). Project b supports: netcoreapp2.0 (.NETCoreApp,Version=v2.0)");
            }
        }

        [Fact]
        public async Task RestoreLogging_VerifyCompatErrorNU1202Properties()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp1 = NuGetFramework.Parse("netcoreapp1.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp1);

                var packageX = new SimpleTestPackageContext("x", "1.0.0");
                packageX.Files.Clear();
                packageX.AddFile("lib/netcoreapp2.0/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, packageX);

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);
                var log = projectA.AssetsFile.LogMessages.SingleOrDefault(e => e.Code == NuGetLogCode.NU1202 && e.TargetGraphs.All(g => !g.Contains("/")));

                // Assert
                r.Success.Should().BeFalse();
                r.AllOutput.Should().Contain("NU1202");
                log.FilePath.Should().Be(projectA.ProjectPath);
                log.LibraryId.Should().Be("x");
                log.Level.Should().Be(LogLevel.Error);
                log.TargetGraphs.ShouldBeEquivalentTo(new[] { netcoreapp1.DotNetFrameworkName });
                log.Message.Should().Be("Package x 1.0.0 is not compatible with netcoreapp1.0 (.NETCoreApp,Version=v1.0). Package x 1.0.0 supports: netcoreapp2.0 (.NETCoreApp,Version=v2.0)");
            }
        }

        [Fact]
        public async Task RestoreLogging_VerifyCompatErrorNU1203Properties()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp1 = NuGetFramework.Parse("netcoreapp1.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp1);
                projectA.Properties.Add("ValidateRuntimeIdentifierCompatibility", "true");
                projectA.Properties.Add("RuntimeIdentifiers", "win10-x64");

                var packageX = new SimpleTestPackageContext("x", "1.0.0");
                packageX.Files.Clear();
                packageX.AddFile("ref/netcoreapp1.0/a.dll"); // ref with a missing runtime

                await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, packageX);

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);
                var log = projectA.AssetsFile.LogMessages.OrderBy(e => e.Message, StringComparer.Ordinal).FirstOrDefault(e => e.Code == NuGetLogCode.NU1203);

                // Assert
                r.Success.Should().BeFalse();
                r.AllOutput.Should().Contain("NU1203");
                log.FilePath.Should().Be(projectA.ProjectPath);
                log.LibraryId.Should().Be("x");
                log.Level.Should().Be(LogLevel.Error);
                log.TargetGraphs.Single().Should().Contain(netcoreapp1.DotNetFrameworkName);
                log.Message.Should().Contain("x 1.0.0 provides a compile-time reference assembly for a on .NETCoreApp,Version=v1.0, but there is no run-time assembly compatible with");
            }
        }

        [Fact]
        public async Task RestoreLogging_VerifyCircularDependencyErrorNU1106Properties()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp1 = NuGetFramework.Parse("netcoreapp1.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp1);

                var packageX = new SimpleTestPackageContext("x", "1.0.0");
                var packageY = new SimpleTestPackageContext("y", "1.0.0");
                packageX.Dependencies.Add(packageY);
                packageY.Dependencies.Add(packageX);

                await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, packageX, packageY);

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);
                var log = projectA.AssetsFile.LogMessages.SingleOrDefault(e => e.Code == NuGetLogCode.NU1108 && e.TargetGraphs.All(g => !g.Contains("/")));

                // Assert
                r.Success.Should().BeFalse();
                r.AllOutput.Should().Contain("NU1108");
                log.FilePath.Should().Be(projectA.ProjectPath);
                log.LibraryId.Should().Be("x");
                log.Level.Should().Be(LogLevel.Error);
                log.TargetGraphs.Single().Should().Contain(netcoreapp1.DotNetFrameworkName);
                log.Message.Should().Contain("a -> x 1.0.0 -> y 1.0.0 -> x (>= 1.0.0)");
            }
        }

        [Fact]
        public async Task RestoreLogging_VerifyConflictErrorNU1107Properties()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp1 = NuGetFramework.Parse("netcoreapp1.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp1);

                var packageX = new SimpleTestPackageContext("x", "1.0.0")
                {
                    Nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>x</id>
                            <version>1.0.0</version>
                            <title />
                            <dependencies>
                                <group>
                                    <dependency id=""z"" version=""[1.0.0]"" />
                                </group>
                            </dependencies>
                        </metadata>
                        </package>")
                };

                var packageY = new SimpleTestPackageContext("y", "1.0.0")
                {
                    Nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>y</id>
                            <version>1.0.0</version>
                            <title />
                            <dependencies>
                                <group>
                                    <dependency id=""z"" version=""[2.0.0]"" />
                                </group>
                            </dependencies>
                        </metadata>
                        </package>")
                };

                var packageZ1 = new SimpleTestPackageContext("z", "1.0.0");
                var packageZ2 = new SimpleTestPackageContext("z", "2.0.0");

                await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, packageX, packageY, packageZ1, packageZ2);

                projectA.AddPackageToAllFrameworks(packageX);
                projectA.AddPackageToAllFrameworks(packageY);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);
                var log = projectA.AssetsFile.LogMessages.SingleOrDefault(e => e.Code == NuGetLogCode.NU1107 && e.TargetGraphs.All(g => !g.Contains("/")));

                // Assert
                r.Success.Should().BeFalse();
                r.AllOutput.Should().Contain("NU1107");
                log.FilePath.Should().Be(projectA.ProjectPath);
                log.LibraryId.Should().Be("z");
                log.Level.Should().Be(LogLevel.Error);
                log.TargetGraphs.Single().Should().Contain(netcoreapp1.DotNetFrameworkName);
                log.Message.Should().Contain("Version conflict detected for z");
                log.Message.Should().Contain("a -> y 1.0.0 -> z (= 2.0.0)");
                log.Message.Should().Contain("a -> x 1.0.0 -> z (= 1.0.0).");
            }
        }

        [Fact]
        public async Task RestoreLogging_VerifyConflictErrorNU1107IsResolvedByTopLevelReference()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp1 = NuGetFramework.Parse("netcoreapp1.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp1);

                var packageX = new SimpleTestPackageContext("x", "1.0.0")
                {
                    Nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>x</id>
                            <version>1.0.0</version>
                            <title />
                            <dependencies>
                                <group>
                                    <dependency id=""z"" version=""[1.0.0]"" />
                                </group>
                            </dependencies>
                        </metadata>
                        </package>")
                };

                var packageY = new SimpleTestPackageContext("y", "1.0.0")
                {
                    Nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>y</id>
                            <version>1.0.0</version>
                            <title />
                            <dependencies>
                                <group>
                                    <dependency id=""z"" version=""[2.0.0]"" />
                                </group>
                            </dependencies>
                        </metadata>
                        </package>")
                };

                var packageZ1 = new SimpleTestPackageContext("z", "1.0.0");
                var packageZ2 = new SimpleTestPackageContext("z", "2.0.0");

                await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, packageX, packageY, packageZ1, packageZ2);

                projectA.AddPackageToAllFrameworks(packageX);
                projectA.AddPackageToAllFrameworks(packageY);

                // This reference solves the conflict
                projectA.AddPackageToAllFrameworks(packageZ1);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                // Assert
                r.Success.Should().BeTrue();
            }
        }

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
        public async Task RestoreLogging_NetCore_WarningsAsErrorsFailsRestore()
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
        public async Task RestoreLogging_NetCore_NoWarnRemovesWarning(string noWarn)
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
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Theory]
        [InlineData("NU1603")]
        [InlineData("$(NoWarn);NU1603")]
        [InlineData("NU1603;$(NoWarn);")]
        [InlineData("NU1603;NU1701")]
        [InlineData("NU1603,NU1701")]
        public async Task RestoreLogging_NetCore_WarningsAsErrorsForSpecificWarningFails(string warnAsError)
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
        public async Task RestoreLogging_NetCore_WarningsAsErrorsForSpecificWarningOfAnotherTypeIgnored()
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
                r.AllOutput.Should().Contain("NU1603");
                r.AllOutput.Should().NotContain("NU1602");
                r.AllOutput.Should().NotContain("NU1701");
            }
        }

        [Fact]
        public async Task RestoreLogging_NetCore_NoWarnWithTreatWarningsAsErrorRemovesWarning()
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
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        public async Task RestoreLogging_NetCore_DifferentNoWarnWithTreatWarningsAsErrorFailsRestore()
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
                projectA.Properties.Add("NoWarn", "NU1107");

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
        public async Task RestoreLogging_NetCore_NoWarnWithWarnSpecificAsErrorRemovesWarning()
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
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        public async Task RestoreLogging_NetCore_DifferentNoWarnWithWarnSpecificAsErrorFailsRestore()
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
                projectA.Properties.Add("NoWarn", "NU1107");

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
        public async Task RestoreLogging_NetCore_PackageSpecificNoWarnRemovesWarning()
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
        public async Task RestoreLogging_NetCore_WithMultiTargeting_AllTfmPackageSpecificNoWarnRemovesWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net45 = NuGetFramework.Parse("net45");
                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2,
                    net45);

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
        public async Task RestoreLogging_NetCore_WithMultiTargeting_PartialTfmPackageSpecificNoWarnRemovesWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net45 = NuGetFramework.Parse("net45");
                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2,
                    net45);

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

                projectA.AddPackageToFramework("net45", packageX);

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
        public async Task RestoreLogging_NetCore_PackageSpecificDifferentNoWarnDoesNotRemoveWarning()
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
                    NoWarn = "NU1107"
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
        public async Task RestoreLogging_NetCore_PackageSpecificNoWarnAndTreatWarningsAsErrors()
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
        public async Task RestoreLogging_NetCore_PackageSpecificNoWarnAndTreatSpecificWarningsAsErrors()
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

        [Fact]
        public async Task RestoreLogging_Legacy_WarningsContainNuGetLogCodes()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
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
        public async Task RestoreLogging_Legacy_WarningsAsErrorsFailsRestore()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
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
        public async Task RestoreLogging_Legacy_NoWarnRemovesWarning(string noWarn)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
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
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Theory]
        [InlineData("NU1603")]
        [InlineData("$(NoWarn);NU1603")]
        [InlineData("NU1603;$(NoWarn);")]
        [InlineData("NU1603;NU1701")]
        [InlineData("NU1603,NU1701")]
        public async Task RestoreLogging_Legacy_WarningsAsErrorsForSpecificWarningFails(string warnAsError)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
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
        public async Task RestoreLogging_Legacy_WarningsAsErrorsForSpecificWarningOfAnotherTypeIgnored()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
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
                r.AllOutput.Should().Contain("NU1603");
                r.AllOutput.Should().NotContain("NU1602");
                r.AllOutput.Should().NotContain("NU1701");
            }
        }

        [Fact]
        public async Task RestoreLogging_Legacy_NoWarnWithTreatWarningsAsErrorRemovesWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
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
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        public async Task RestoreLogging_Legacy_DifferentNoWarnWithTreatWarningsAsErrorFailsRestore()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("TreatWarningsAsErrors", "true");
                projectA.Properties.Add("NoWarn", "NU1107");

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
        public async Task RestoreLogging_Legacy_NoWarnWithWarnSpecificAsErrorRemovesWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
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
                r.AllOutput.Should().NotContain("NU1603");
            }
        }

        [Fact]
        public async Task RestoreLogging_Legacy_DifferentNoWarnWithWarnSpecificAsErrorFailsRestore()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("WarningsAsErrors", "NU1603");
                projectA.Properties.Add("NoWarn", "NU1107");

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
        public async Task RestoreLogging_Legacy_PackageSpecificNoWarnRemovesWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
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
        public async Task RestoreLogging_Legacy_PackageSpecificDifferentNoWarnDoesNotRemoveWarning()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                // Referenced but not created
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    NoWarn = "NU1107"
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
        public async Task RestoreLogging_Legacy_PackageSpecificNoWarnAndTreatWarningsAsErrors()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
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
        public async Task RestoreLogging_Legacy_PackageSpecificNoWarnAndTreatSpecificWarningsAsErrors()
        {

            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
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
