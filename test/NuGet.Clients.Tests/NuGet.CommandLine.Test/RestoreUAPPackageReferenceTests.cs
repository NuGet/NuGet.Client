﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreUAPPackageReferenceTests
    {
        [Fact]
        public async Task RestoreUAP_BasicRestore()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.AnyFramework);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                projectA.Properties.Add("TargetPlatformIdentifier", "UAP");
                projectA.Properties.Add("TargetPlatformVersion", "10.0.14393.0");
                projectA.Properties.Add("TargetPlatformMinVersion", "10.0.10586.0");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = RestoreSolution(pathContext);

                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var dgSpec = DependencyGraphSpec.Load(dgPath);

                var propsXML = XDocument.Load(projectA.PropsOutput);
                var styleNode = propsXML.Root.Elements().First().Elements(XName.Get("NuGetProjectStyle", "http://schemas.microsoft.com/developer/msbuild/2003")).FirstOrDefault();

                var projectSpec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(ProjectStyle.PackageReference, projectSpec.RestoreMetadata.ProjectStyle);
                Assert.Equal("PackageReference", styleNode.Value);
                Assert.Equal(NuGetFramework.Parse("UAP10.0.10586.0"), projectSpec.TargetFrameworks.Single().FrameworkName);
            }
        }

        [Fact]
        public async Task RestoreUAP_VerifyTargetPlatformVersionIsUsed()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.AnyFramework);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                projectA.Properties.Add("TargetPlatformIdentifier", "UAP");
                projectA.Properties.Add("TargetPlatformVersion", "10.0.14393.0");
                projectA.Properties.Add("TargetPlatformMinVersion", "");
                projectA.Properties.Add("RestoreProjectStyle", "PackageReference");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = RestoreSolution(pathContext);

                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var dgSpec = DependencyGraphSpec.Load(dgPath);

                var propsXML = XDocument.Load(projectA.PropsOutput);
                var styleNode = propsXML.Root.Elements().First().Elements(XName.Get("NuGetProjectStyle", "http://schemas.microsoft.com/developer/msbuild/2003")).FirstOrDefault();

                var projectSpec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(ProjectStyle.PackageReference, projectSpec.RestoreMetadata.ProjectStyle);
                Assert.Equal("PackageReference", styleNode.Value);
                Assert.Equal(NuGetFramework.Parse("UAP10.0.14393.0"), projectSpec.TargetFrameworks.Single().FrameworkName);
            }
        }

        [Fact]
        public async Task RestoreUAP_VerifyNoContentFiles()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.AnyFramework);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("contentFiles/any/any/a.txt");

                projectA.AddPackageToAllFrameworks(packageX);

                projectA.Properties.Add("TargetPlatformIdentifier", "UAP");
                projectA.Properties.Add("TargetPlatformVersion", "10.0.14393.0");
                projectA.Properties.Add("TargetPlatformMinVersion", "10.0.10586.0");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = RestoreSolution(pathContext);

                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var dgSpec = DependencyGraphSpec.Load(dgPath);

                var propsXML = XDocument.Load(projectA.PropsOutput);
                var styleNode = propsXML.Root.Elements().First().Elements(XName.Get("NuGetProjectStyle", "http://schemas.microsoft.com/developer/msbuild/2003")).FirstOrDefault();

                var projectSpec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(ProjectStyle.PackageReference, projectSpec.RestoreMetadata.ProjectStyle);
                Assert.Equal("PackageReference", styleNode.Value);
                Assert.Equal(NuGetFramework.Parse("UAP10.0.10586.0"), projectSpec.TargetFrameworks.Single().FrameworkName);

                Assert.DoesNotContain("a.txt", propsXML.ToString());
            }
        }

        [Fact]
        public void RestoreUAP_NoPackageReferences_VerifyRestoreStyleIsUsed()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.AnyFramework);

                projectA.Properties.Add("TargetPlatformIdentifier", "UAP");
                projectA.Properties.Add("TargetPlatformVersion", "10.0.14393.0");
                projectA.Properties.Add("TargetPlatformMinVersion", "10.0.10586.0");
                projectA.Properties.Add("RestoreProjectStyle", "PackageReference");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = RestoreSolution(pathContext);

                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var dgSpec = DependencyGraphSpec.Load(dgPath);

                var propsXML = XDocument.Load(projectA.PropsOutput);
                var styleNode = propsXML.Root.Elements().First().Elements(XName.Get("NuGetProjectStyle", "http://schemas.microsoft.com/developer/msbuild/2003")).FirstOrDefault();

                var projectSpec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(ProjectStyle.PackageReference, projectSpec.RestoreMetadata.ProjectStyle);
                Assert.Equal("PackageReference", styleNode.Value);
                Assert.Equal(NuGetFramework.Parse("UAP10.0.10586.0"), projectSpec.TargetFrameworks.Single().FrameworkName);
            }
        }

        [Fact]
        public async Task RestoreUAP_VerifyProjectToProjectRestore()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.AnyFramework);

                var projectB = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.AnyFramework);

                projectA.Properties.Add("TargetPlatformIdentifier", "UAP");
                projectA.Properties.Add("TargetPlatformVersion", "10.0.14393.0");
                projectA.Properties.Add("TargetPlatformMinVersion", "10.0.10586.0");

                // Set style for A since it has no references
                projectA.Properties.Add("RestoreProjectStyle", "PackageReference");


                projectB.Properties.Add("TargetPlatformIdentifier", "UAP");
                projectB.Properties.Add("TargetPlatformVersion", "10.0.14393.0");
                projectB.Properties.Add("TargetPlatformMinVersion", "10.0.10586.0");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectB.AddPackageToAllFrameworks(packageX);
                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = RestoreSolution(pathContext);

                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var dgSpec = DependencyGraphSpec.Load(dgPath);

                var assetsFile = projectA.AssetsFile;

                // Assert
                Assert.Equal("1.0.0", assetsFile.Libraries.Single(p => p.Name == "x").Version.ToNormalizedString());
            }
        }

        private static CommandRunnerResult RestoreSolution(SimpleTestPathContext pathContext, int exitCode = 0)
        {
            var nugetexe = Util.GetNuGetExePath();

            // Store the dg file for debugging
            var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
            var envVars = new Dictionary<string, string>()
                {
                    { "NUGET_PERSIST_DG", "true" },
                    { "NUGET_PERSIST_DG_PATH", dgPath }
                };

            string[] args = new string[] {
                    "restore",
                    pathContext.SolutionRoot,
                    "-Verbosity",
                    "detailed"
                };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                pathContext.WorkingDirectory.Path,
                string.Join(" ", args),
                waitForExit: true,
                environmentVariables: envVars);

            // Assert
            Assert.True(exitCode == r.Item1, r.Item3 + "\n\n" + r.Item2);

            return r;
        }
    }
}