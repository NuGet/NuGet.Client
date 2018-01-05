// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Build.Tasks.Pack.Test
{
    public class PackTaskTests
    {
        [Fact]
        public void PackTask_DelegatesToPackLogic()
        {
            // Arrange
            var packArgs = new PackArgs();
            var packageBuilder = new PackageBuilder();
            var packCommandRunner = new PackCommandRunner(null, null);
            IPackTaskRequest<IMSBuildItem> request = null;

            var logic = new Mock<IPackTaskLogic>();
            logic
                .Setup(x => x.GetPackArgs(It.IsAny<IPackTaskRequest<IMSBuildItem>>()))
                .Returns(packArgs)
                .Callback<IPackTaskRequest<IMSBuildItem>>(r => request = r);
            logic
                .Setup(x => x.GetPackageBuilder(It.IsAny<IPackTaskRequest<IMSBuildItem>>()))
                .Returns(packageBuilder);
            logic
                .Setup(x => x.GetPackCommandRunner(It.IsAny<IPackTaskRequest<IMSBuildItem>>(), packArgs, packageBuilder))
                .Returns(packCommandRunner);

            var target = new PackTask();
            target.PackTaskLogic = logic.Object;
            
            // Act
            var result = target.Execute();

            // Assert
            Assert.True(result);
            Assert.NotNull(request);
            logic.Verify(x => x.GetPackArgs(request));
            logic.Verify(x => x.GetPackageBuilder(request));
            logic.Verify(x => x.GetPackCommandRunner(request, packArgs, packageBuilder));
            logic.Verify(x => x.BuildPackage(packCommandRunner));
        }

        [Fact]
        public void PackTask_Dispose()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);

            var nuspecPath = Path.Combine(dir, "test.nuspec");
            File.WriteAllText(nuspecPath, @"
<package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
  <metadata>
    <id>Test</id>
    <summary>Summary</summary>
    <description>Description</description>
    <version>1.0.0</version>
    <authors>Microsoft</authors>
    <dependencies>
      <dependency id=""System.Collections.Immutable"" version=""4.3.0"" />
    </dependencies>
  </metadata>
</package>
");

            var builder = new PackageBuilder();

            var runner = new PackCommandRunner(
                new PackArgs
                {
                    CurrentDirectory = dir,
                    OutputDirectory = dir,
                    Path = nuspecPath,
                    Exclude = Array.Empty<string>(),
                    Symbols = true,
                    Logger = NullLogger.Instance
                },
                MSBuildProjectFactory.ProjectCreator,
                builder);

            runner.BuildPackage();

            // It should be possible to delete the entire directory.
            // If this fails the runner left some files open.
            Directory.Delete(dir, recursive: true);
        }

        [Fact]
        public void PackTask_TrimsWhitespace()
        {
            // Arrange
            var target = new PackTask
            {
                AssemblyName = " AssemblyName \t ",
                BuildOutputFolder = " BuildOutputFolder \t ",
                Copyright = " Copyright \t ",
                Description = " Description \t ",
                IconUrl = " IconUrl \t ",
                LicenseUrl = " LicenseUrl \t ",
                MinClientVersion = " MinClientVersion \t ",
                NuspecOutputPath = " NuspecOutputPath \t ",
                PackageId = " PackageId \t ",
                PackageOutputPath = " PackageOutputPath \t ",
                PackageVersion = " PackageVersion \t ",
                ProjectUrl = " ProjectUrl \t ",
                ReleaseNotes = " ReleaseNotes \t ",
                RepositoryType = " RepositoryType \t ",
                RepositoryUrl = " RepositoryUrl \t ",
                RepositoryCommit = " RepositoryCommit \t ",
                RepositoryBranch = " RepositoryBranch \t "
            };

            // Act
            var actual = GetRequest(target);

            // Assert
            Assert.Equal("AssemblyName", actual.AssemblyName);
            Assert.Equal("BuildOutputFolder", actual.BuildOutputFolder);
            Assert.Equal("Copyright", actual.Copyright);
            Assert.Equal("Description", actual.Description);
            Assert.Equal("IconUrl", actual.IconUrl);
            Assert.Equal("LicenseUrl", actual.LicenseUrl);
            Assert.Equal("MinClientVersion", actual.MinClientVersion);
            Assert.Equal("NuspecOutputPath", actual.NuspecOutputPath);
            Assert.Equal("PackageId", actual.PackageId);
            Assert.Equal("PackageOutputPath", actual.PackageOutputPath);
            Assert.Equal("PackageVersion", actual.PackageVersion);
            Assert.Equal("ProjectUrl", actual.ProjectUrl);
            Assert.Equal("ReleaseNotes", actual.ReleaseNotes);
            Assert.Equal("RepositoryType", actual.RepositoryType);
            Assert.Equal("RepositoryUrl", actual.RepositoryUrl);
            Assert.Equal("RepositoryCommit", actual.RepositoryCommit);
            Assert.Equal("RepositoryBranch", actual.RepositoryBranch);
        }

        [Fact]
        public void PackTask_CoalescesWhitespaceToNull()
        {
            // Arrange
            var target = new PackTask
            {
                AssemblyName = " \t ",
                BuildOutputFolder = " \t ",
                Copyright = " \t ",
                Description = " \t ",
                IconUrl = " \t ",
                LicenseUrl = " \t ",
                MinClientVersion = " \t ",
                NuspecOutputPath = " \t ",
                PackageId = " \t ",
                PackageOutputPath = " \t ",
                PackageVersion = " \t ",
                ProjectUrl = " \t ",
                ReleaseNotes = " \t ",
                RepositoryType = " \t ",
                RepositoryUrl = " \t ",
                RepositoryCommit = " \t ",
                RepositoryBranch = " \t ",
            };

            // Act
            var actual = GetRequest(target);

            // Assert
            Assert.Null(actual.AssemblyName);
            Assert.Null(actual.BuildOutputFolder);
            Assert.Null(actual.Copyright);
            Assert.Null(actual.Description);
            Assert.Null(actual.IconUrl);
            Assert.Null(actual.LicenseUrl);
            Assert.Null(actual.MinClientVersion);
            Assert.Null(actual.NuspecOutputPath);
            Assert.Null(actual.PackageId);
            Assert.Null(actual.PackageOutputPath);
            Assert.Null(actual.PackageVersion);
            Assert.Null(actual.ProjectUrl);
            Assert.Null(actual.ReleaseNotes);
            Assert.Null(actual.RepositoryType);
            Assert.Null(actual.RepositoryUrl);
            Assert.Null(actual.RepositoryCommit);
            Assert.Null(actual.RepositoryBranch);
        }

        [Fact]
        public void PackTask_CleanUpArraysOfStrings()
        {
            // Arrange
            var target = new PackTask
            {
                Authors = new[] { "", "  ", " Authors \t ", null },
                PackageTypes = new[] { "", "  ", " PackageTypes \t ", null },
                Tags = new[] { "", "  ", " Tags \t ", null },
                TargetFrameworks = new[] { "", "  ", " TargetFrameworks \t ", null }
            };

            // Act
            var actual = GetRequest(target);

            // Assert
            Assert.Equal(new[] { "Authors" }, actual.Authors);
            Assert.Equal(new[] { "PackageTypes" }, actual.PackageTypes);
            Assert.Equal(new[] { "Tags" }, actual.Tags);
            Assert.Equal(new[] { "TargetFrameworks" }, actual.TargetFrameworks);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PackTask_CopiesBooleanValues(bool value)
        {
            // Arrange
            var target = new PackTask
            {
                ContinuePackingAfterGeneratingNuspec = value,
                IncludeBuildOutput = value,
                IncludeSource = value,
                IncludeSymbols = value,
                InstallPackageToOutputPath = value,
                IsTool = value,
                NoPackageAnalysis = value,
                OutputFileNamesWithoutVersion = value,
                RequireLicenseAcceptance = value,
                DevelopmentDependency = value,
                Serviceable = value
            };

            // Act
            var actual = GetRequest(target);

            // Assert
            Assert.Equal(value, actual.ContinuePackingAfterGeneratingNuspec);
            Assert.Equal(value, actual.IncludeBuildOutput);
            Assert.Equal(value, actual.IncludeSource);
            Assert.Equal(value, actual.IncludeSymbols);
            Assert.Equal(value, actual.InstallPackageToOutputPath);
            Assert.Equal(value, actual.IsTool);
            Assert.Equal(value, actual.NoPackageAnalysis);
            Assert.Equal(value, actual.OutputFileNamesWithoutVersion);
            Assert.Equal(value, actual.RequireLicenseAcceptance);
            Assert.Equal(value, actual.DevelopmentDependency);
            Assert.Equal(value, actual.Serviceable);
        }

        [Fact]
        public void PackTask_WrapsTaskItems()
        {
            // Arrange
            var target = new PackTask
            {
                FrameworkAssemblyReferences = new[] { null, new Mock<ITaskItem>().Object },
                PackageFiles = new[] { null, new Mock<ITaskItem>().Object },
                PackageFilesToExclude = new[] { null, new Mock<ITaskItem>().Object },
                PackItem = new Mock<ITaskItem>().Object,
                SourceFiles = new[] { null, new Mock<ITaskItem>().Object }
            };

            // Act
            var actual = GetRequest(target);

            // Assert
            Assert.Equal(1, actual.FrameworkAssemblyReferences.OfType<MSBuildTaskItem>().Count());
            Assert.Equal(1, actual.PackageFiles.OfType<MSBuildTaskItem>().Count());
            Assert.Equal(1, actual.PackageFilesToExclude.OfType<MSBuildTaskItem>().Count());
            Assert.NotNull(actual.PackItem);
            Assert.Equal(1, actual.SourceFiles.OfType<MSBuildTaskItem>().Count());
        }

        [Fact]
        public void PackTask_ConvertsNullArraysToEmptyArrays()
        {
            // Arrange
            var target = new PackTask
            {
                FrameworkAssemblyReferences = null,
                Authors = null,
                PackageFiles = null,
                PackageFilesToExclude = null,
                PackageTypes = null,
                SourceFiles = null,
                Tags = null,
                TargetFrameworks = null,
                BuildOutputInPackage = null,
                TargetPathsToSymbols = null
            };

            // Act
            var actual = GetRequest(target);

            // Assert
            Assert.Equal(0, actual.FrameworkAssemblyReferences.Length);
            Assert.Equal(0, actual.Authors.Length);
            Assert.Equal(0, actual.PackageFiles.Length);
            Assert.Equal(0, actual.PackageFilesToExclude.Length);
            Assert.Equal(0, actual.PackageTypes.Length);
            Assert.Equal(0, actual.SourceFiles.Length);
            Assert.Equal(0, actual.Tags.Length);
            Assert.Equal(0, actual.TargetFrameworks.Length);
            Assert.Equal(0, actual.BuildOutputInPackage.Length);
            Assert.Equal(0, actual.TargetPathsToSymbols.Length);
        }

        [Fact]
        public void PackTask_MapsAllProperties()
        {
            // Arrange
            var target = new PackTask
            {
                AssemblyName = "AssemblyName",
                FrameworkAssemblyReferences = new ITaskItem[0],
                Authors = Array.Empty<string>(),
                AllowedOutputExtensionsInPackageBuildOutputFolder = Array.Empty<string>(),
                AllowedOutputExtensionsInSymbolsPackageBuildOutputFolder = Array.Empty<string>(),
                BuildOutputFolder = "BuildOutputFolder",
                ContentTargetFolders = new string[] { "ContentTargetFolders" } ,
                ContinuePackingAfterGeneratingNuspec = true,
                Copyright = "Copyright",
                Description = "Description",
                DevelopmentDependency = true,
                IconUrl = "IconUrl",
                IncludeBuildOutput = true,
                IncludeSource = true,
                IncludeSymbols = true,
                IsTool = true,
                LicenseUrl = "LicenseUrl",
                MinClientVersion = "MinClientVersion",
                NoPackageAnalysis = true,
                NuspecOutputPath = "NuspecOutputPath",
                NuspecProperties = Array.Empty<string>(),
                PackItem = null, // This is asserted by other tests. It does not serialize well.
                PackageFiles = new ITaskItem[0],
                PackageFilesToExclude = new ITaskItem[0],
                PackageId = "PackageId",
                PackageOutputPath = "PackageOutputPath",
                PackageTypes = Array.Empty<string>(),
                PackageVersion = "PackageVersion",
                ProjectReferencesWithVersions = new ITaskItem[0],
                ProjectUrl = "ProjectUrl",
                ReleaseNotes = "ReleaseNotes",
                RepositoryType = "RepositoryType",
                RepositoryUrl = "RepositoryUrl",
                RepositoryCommit = "RepositoryCommit",
                RepositoryBranch = "RepositoryBranch",
                RequireLicenseAcceptance = true,
                Serviceable = true,
                SourceFiles = new ITaskItem[0],
                Tags = Array.Empty<string>(),
                TargetFrameworks = Array.Empty<string>(),
                BuildOutputInPackage = new ITaskItem[0],
                TargetPathsToSymbols = new ITaskItem[0],
                FrameworksWithSuppressedAssemblyReferences = new ITaskItem[0],
                FrameworksWithSuppressedDependencies = new ITaskItem[0]
    };

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new OrderedContractResolver(),
                Formatting = Formatting.Indented
            };

            // Exclude properties on the build task but not used for the pack task request.
            var excludedProperties = new[]
            {
                "BuildEngine",
                "BuildEngine2",
                "BuildEngine3",
                "BuildEngine4",
                "HostObject",
                "Log",
                "PackTaskLogic",
            };

            var jsonModelBefore = JObject.FromObject(target, JsonSerializer.Create(settings));
            foreach (var property in excludedProperties)
            {
                jsonModelBefore.Remove(property);
            }

            var expectedJson = JsonConvert.SerializeObject(jsonModelBefore, settings);

            // Act
            var actual = GetRequest(target);

            // Assert
            var actualJson = JsonConvert.SerializeObject(actual, settings);
            Assert.Equal(expectedJson, actualJson);
        }

        private IPackTaskRequest<IMSBuildItem> GetRequest(PackTask target)
        {
            // Arrange
            IPackTaskRequest<IMSBuildItem> request = null;

            var logic = new Mock<IPackTaskLogic>();
            logic
                .Setup(x => x.GetPackArgs(It.IsAny<IPackTaskRequest<IMSBuildItem>>()))
                .Callback<IPackTaskRequest<IMSBuildItem>>(r => request = r);
            
            target.PackTaskLogic = logic.Object;

            // Act
            var result = target.Execute();

            // Assert
            return request;
        }

        /// <summary>
        /// Source: http://stackoverflow.com/a/11309106
        /// </summary>
        private class OrderedContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, memberSerialization)
                    .OrderBy(p => p.PropertyName)
                    .ToList();
            }
        }
    }
}
