// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
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
        public async Task RestoreNetCore_SingleTFM_SameIdMultiPackageDownload()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX1 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageX2 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "2.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX1,
                    packageX2);

                projectA.AddPackageDownloadToAllFrameworks(packageX1);
                projectA.AddPackageDownloadToAllFrameworks(packageX2);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(2, lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Count);
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                Assert.Equal($"[{packageX2.Version}, {packageX2.Version}]", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().VersionRange.ToNormalizedString());
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.Last().Name);
                Assert.Equal($"[{packageX1.Version}, {packageX1.Version}]", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.Last().VersionRange.ToNormalizedString());

                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX1.Identity.Id, packageX1.Version)), $"{packageX1.ToString()} is not installed");
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX2.Identity.Id, packageX2.Version)), $"{packageX2.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_SameIdSameVersionMultiDeclaration_MultiPackageDownload()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX1 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageX2 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "2.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX1,
                    packageX2);

                projectA.AddPackageDownloadToAllFrameworks(packageX1);
                projectA.AddPackageDownloadToAllFrameworks(packageX2);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Add one more - Adding them through the Test Context adds only exact versions.

                var xml = projectA.GetXML();

                var props = new Dictionary<string, string>();
                var attributes = new Dictionary<string, string>();
                attributes.Add("Version", "[1.0.0, 1.0.0]");
                ProjectFileUtils.AddItem(
                                    xml,
                                    "PackageDownload",
                                    packageX1.Id,
                                    NuGetFramework.AnyFramework,
                                    props,
                                    attributes);

                xml.Save(projectA.ProjectPath);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(2, lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Count);
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                Assert.Equal($"[{packageX2.Version}, {packageX2.Version}]", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().VersionRange.ToNormalizedString());
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.Last().Name);
                Assert.Equal($"[{packageX1.Version}, {packageX1.Version}]", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.Last().VersionRange.ToNormalizedString());

                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX1.Identity.Id, packageX1.Version)), $"{packageX1.ToString()} is not installed");
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX2.Identity.Id, packageX2.Version)), $"{packageX2.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultiTfm_PackageDownloadAndPackageReference()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net45;net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX1 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageX2 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "2.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX1,
                    packageX2);

                projectA.AddPackageToFramework("net45", packageX1);

                projectA.AddPackageDownloadToFramework("net46", packageX2);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);

                Assert.Equal(1, lockFile.Libraries.Count);
                Assert.Equal(2, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.First().Dependencies.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.Count);
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                Assert.Equal($"[{packageX2.Version}, {packageX2.Version}]",
                    lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().VersionRange.ToNormalizedString());
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.First().Dependencies.Last().Name);
                Assert.Equal($"[{packageX1.Version}, )",
                    lockFile.PackageSpec.TargetFrameworks.First().Dependencies.First().LibraryRange.VersionRange.ToNormalizedString());
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX1.Identity.Id, packageX1.Version)),
                    $"{packageX1.ToString()} is not installed");
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX2.Identity.Id, packageX2.Version)),
                    $"{packageX2.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultiTfm_MultiPackageDownload()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net45;net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX1 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageX2 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "2.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX1,
                    packageX2);

                projectA.AddPackageDownloadToFramework("net45", packageX1);

                projectA.AddPackageDownloadToFramework("net46", packageX2);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);

                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(2, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(0, lockFile.PackageSpec.TargetFrameworks.First().Dependencies.Count);
                Assert.Equal(0, lockFile.PackageSpec.TargetFrameworks.Last().Dependencies.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.Count);

                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                Assert.Equal($"[{packageX2.Version}, {packageX2.Version}]",
                    lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().VersionRange.ToNormalizedString());
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Last().Name);
                Assert.Equal($"[{packageX1.Version}, {packageX1.Version}]",
                    lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.First().VersionRange.ToNormalizedString());
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX1.Identity.Id, packageX1.Version)),
                    $"{packageX1.ToString()} is not installed");
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX2.Identity.Id, packageX2.Version)),
                    $"{packageX2.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_PackageDownload_NonExactVersion_Fails()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX1 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX1);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var xml = projectA.GetXML();

                var props = new Dictionary<string, string>();
                var attributes = new Dictionary<string, string>();
                attributes.Add("Version", "1.0.0");
                ProjectFileUtils.AddItem(
                                    xml,
                                    "PackageDownload",
                                    packageX1.Id,
                                    NuGetFramework.AnyFramework,
                                    props,
                                    attributes);

                xml.Save(projectA.ProjectPath);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);

                // Assert
                Assert.False(r.Success, r.AllOutput);
                Assert.False(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);
            }
        }

        [Fact]
        public async Task RestoreNetCore_PackageDownload_NoOpAccountsForMissingPackages()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                projectA.AddPackageDownloadToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var r = Util.RestoreSolution(pathContext);

                // Preconditions
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);
                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Count);
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                var packagePath = Path.Combine(pathContext.UserPackagesFolder, packageX.Identity.Id, packageX.Version);
                Assert.True(Directory.Exists(packagePath), $"{packageX.ToString()} is not installed");

                Directory.Delete(packagePath, true);

                Assert.False(Directory.Exists(packagePath), $"{packageX.ToString()} should not be installed anymore.");

                // Act
                r = Util.RestoreSolution(pathContext);
                Assert.True(r.Success, r.AllOutput);

                Assert.True(Directory.Exists(packagePath), $"{packageX.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_PackageDownload_DoesNotAFfectNoOp()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                projectA.AddPackageDownloadToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var r = Util.RestoreSolution(pathContext);

                // Preconditions
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);
                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Count);
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                var packagePath = Path.Combine(pathContext.UserPackagesFolder, packageX.Identity.Id, packageX.Version);
                Assert.True(Directory.Exists(packagePath), $"{packageX.ToString()} is not installed");
                Assert.Contains("Writing cache file", r.Item2);

                // Act
                r = Util.RestoreSolution(pathContext);
                Assert.True(r.Success, r.AllOutput);

                Assert.True(Directory.Exists(packagePath), $"{packageX.ToString()} is not installed");

                Assert.Equal(0, r.Item1);
                Assert.DoesNotContain("Writing cache file", r.Item2);
                Assert.Contains("No further actions are required to complete", r.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_FrameworkReferenceFromPackage()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.FrameworkReferences.Add(NuGetFramework.Parse("net45"), new string[] { "FrameworkRef" });
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(1, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(1, lockFile.Targets.First().Libraries.Count);
                Assert.Equal(1, lockFile.Targets.First().Libraries.Single().FrameworkReferences.Count);
                Assert.Equal("FrameworkRef", lockFile.Targets.First().Libraries.Single().FrameworkReferences.Single());
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX.Identity.Id, packageX.Version)), $"{packageX.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_FrameworkReference_TransitivePackageToPackage()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.FrameworkReferences.Add(NuGetFramework.Parse("net45"), new string[] { "FrameworkRef" });
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };
                packageY.Files.Clear();
                packageY.UseDefaultRuntimeAssemblies = false;
                packageY.AddFile("lib/net45/y.dll");
                packageY.FrameworkReferences.Add(NuGetFramework.Parse("net45"), new string[] { "FrameworkRefY" });

                packageX.Dependencies.Add(packageY);


                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX,
                    packageY);

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(2, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(2, lockFile.Targets.First().Libraries.Count);
                Assert.Equal("FrameworkRef", lockFile.Targets.First().Libraries.First().FrameworkReferences.Single());
                Assert.Equal("FrameworkRefY", lockFile.Targets.First().Libraries.Last().FrameworkReferences.Single());
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX.Identity.Id, packageX.Version)), $"{packageX.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_FrameworkReference_TransitiveProjectToProject()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.FrameworkReferences.Add(NuGetFramework.Parse("net45"), new string[] { "FrameworkRef" });
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                projectA.AddPackageToAllFrameworks(packageX);


                var projectB = SimpleTestProjectContext.CreateNETCoreWithSDK(
                    projectName: "b",
                    solutionRoot: pathContext.SolutionRoot,
                    frameworks: MSBuildStringUtility.Split(projectFrameworks));

                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);

                solution.Create(pathContext.SolutionRoot);

                var xml = projectB.GetXML();

                var props = new Dictionary<string, string>();
                var attributes = new Dictionary<string, string>();
                ProjectFileUtils.AddItem(
                                    xml,
                                    "FrameworkReference",
                                    "FrameworkRefY",
                                    NuGetFramework.AnyFramework,
                                    props,
                                    attributes);

                xml.Save(projectB.ProjectPath);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(2, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(2, lockFile.Targets.First().Libraries.Count);
                Assert.Equal("FrameworkRef", lockFile.Targets.First().Libraries.First().FrameworkReferences.Single());
                Assert.Equal("FrameworkRefY", lockFile.Targets.First().Libraries.Last().FrameworkReferences.Single());
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX.Identity.Id, packageX.Version)), $"{packageX.ToString()} is not installed");

                // Assert 2
                Assert.True(File.Exists(projectB.AssetsFileOutputPath), r.AllOutput);

                lockFile = LockFileUtilities.GetLockFile(projectB.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(0, lockFile.Targets.First().Libraries.Count);
                Assert.Equal("FrameworkRefY", lockFile.PackageSpec.TargetFrameworks.Single().FrameworkReferences.Single().Name);
                Assert.Equal("none", FrameworkDependencyFlagsUtils.GetFlagString(lockFile.PackageSpec.TargetFrameworks.Single().FrameworkReferences.Single().PrivateAssets));
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_FrameworkReference_TransitiveProjectToProject_PrivateAssets_SuppressesReference()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.FrameworkReferences.Add(NuGetFramework.Parse("net45"), new string[] { "FrameworkRef" });
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                projectA.AddPackageToAllFrameworks(packageX);


                var projectB = SimpleTestProjectContext.CreateNETCoreWithSDK(
                    projectName: "b",
                    solutionRoot: pathContext.SolutionRoot,
                    frameworks: MSBuildStringUtility.Split(projectFrameworks));

                projectB.AddProjectToAllFrameworks(projectA);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);

                solution.Create(pathContext.SolutionRoot);

                var xml = projectA.GetXML();
                var props = new Dictionary<string, string>();
                var attributes = new Dictionary<string, string>();
                ProjectFileUtils.AddItem(
                                    xml,
                                    "FrameworkReference",
                                    "FrameworkRefY",
                                    NuGetFramework.AnyFramework,
                                    props,
                                    attributes);
                attributes.Add("PrivateAssets", "all");
                ProjectFileUtils.AddItem(
                                    xml,
                                    "FrameworkReference",
                                    "FrameworkRefSupressed",
                                    NuGetFramework.AnyFramework,
                                    props,
                                    attributes);

                xml.Save(projectA.ProjectPath);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(1, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(1, lockFile.Targets.First().Libraries.Count);
                Assert.Equal("FrameworkRef", string.Join(",", lockFile.Targets.First().Libraries.First().FrameworkReferences));
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX.Identity.Id, packageX.Version)), $"{packageX.ToString()} is not installed");
                Assert.Equal("all", FrameworkDependencyFlagsUtils.GetFlagString(lockFile.PackageSpec.TargetFrameworks.Single().FrameworkReferences.First().PrivateAssets));
                Assert.Equal("none", FrameworkDependencyFlagsUtils.GetFlagString(lockFile.PackageSpec.TargetFrameworks.Single().FrameworkReferences.Last().PrivateAssets));

                // Assert 2
                Assert.True(File.Exists(projectB.AssetsFileOutputPath), r.AllOutput);

                lockFile = LockFileUtilities.GetLockFile(projectB.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(2, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(2, lockFile.Targets.First().Libraries.Count);
                Assert.Equal("FrameworkRef", string.Join(",", lockFile.Targets.First().Libraries.First().FrameworkReferences));
                Assert.Equal("FrameworkRefY", string.Join(",", lockFile.Targets.First().Libraries.Last().FrameworkReferences));

            }
        }
    }
}
