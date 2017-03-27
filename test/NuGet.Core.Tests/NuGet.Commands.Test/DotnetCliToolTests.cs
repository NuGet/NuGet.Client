// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
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
        public async Task DotnetCliTool_VerifyProjectsAreNotAllowed()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var dgFile = new DependencyGraphSpec();

                var spec = ToolRestoreUtility.GetSpec(
                    Path.Combine(pathContext.SolutionRoot, "tool", "fake.csproj"),
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

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };

                var packageB = new SimpleTestPackageContext()
                {
                    Id = "b",
                    Version = "1.0.0"
                };

                packageA.Dependencies.Add(packageB);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA,
                    packageB);

                var projectYRoot = Path.Combine(pathContext.SolutionRoot, "b");
                Directory.CreateDirectory(projectYRoot);
                var projectYJson = Path.Combine(projectYRoot, "project.json");

                var projectJsonContent = JObject.Parse(@"{
                                                    'dependencies': {
                                                    },
                                                    'frameworks': {
                                                        'netstandard1.0': {
                                                    }
                                                  }
                                               }");

                File.WriteAllText(projectYJson, projectJsonContent.ToString());

                // Act
                var result = await CommandsTestUtility.RunSingleRestore(dgFile, pathContext, logger);

                // Assert
                Assert.True(result.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                Assert.True(File.Exists(path));

                var lockFormat = new LockFileFormat();
                var lockFile = lockFormat.Read(path);

                // Verify only packages
                Assert.Empty(lockFile.Libraries.Where(e => e.Type != "package"));
            }
        }

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

        [Fact]
        public async Task DotnetCliTool_BasicToolRestore_DifferentVersionRanges()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var dgFile = new DependencyGraphSpec();

                var versions = new List<VersionRange>();

                var limit = 100;

                for (int i = 0; i < limit; i++)
                {
                    var version = VersionRange.Parse($"{i + 1}.0.0");
                    versions.Add(version);

                    var spec = ToolRestoreUtility.GetSpec(
                        Path.Combine(pathContext.SolutionRoot, $"fake{i}.csproj"),
                        "a",
                        version,
                        NuGetFramework.Parse("netcoreapp1.0"));

                    dgFile.AddProject(spec);
                    dgFile.AddRestore(spec.Name);
                }

                var pathResolver = new ToolPathResolver(pathContext.UserPackagesFolder);

                foreach (var version in versions)
                {
                    await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, new PackageIdentity("a", version.MinVersion));
                }

                // Act
                var results = await CommandsTestUtility.RunRestore(dgFile, pathContext, logger);

                // Assert
                Assert.Equal(limit, results.Count);

                foreach (var result in results)
                {
                    Assert.True(result.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                }

                foreach (var version in versions)
                {
                    var path = pathResolver.GetLockFilePath(
                        "a",
                        version.MinVersion,
                        NuGetFramework.Parse("netcoreapp1.0"));

                    Assert.True(File.Exists(path));
                }
            }
        }
    }
}
