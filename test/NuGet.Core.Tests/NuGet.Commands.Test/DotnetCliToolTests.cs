using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
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

                var spec = GetSpec(
                    Path.Combine(pathContext.SolutionRoot, "tool", "fake.csproj"),
                    "a",
                    VersionRange.Parse("1.0.0"));

                spec.RestoreMetadata.OutputPath = Path.Combine(pathContext.SolutionRoot, "project", "obj");

                dgFile.AddProject(spec);
                dgFile.AddRestore(spec.Name);

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };

                packageA.PackageTypes.Add(PackageType.DotnetCliTool);
                packageA.AddFile("lib/netcoreapp1.0/a.deps.json", GetDepsJson("a"));

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

                var outputPath = DotnetCliToolPathResolver.GetFilePath(spec.RestoreMetadata.OutputPath, "a");
                var outputFile = DotnetCliToolFile.Load(outputPath);

                // Assert
                Assert.True(result.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                Assert.True(File.Exists(Path.Combine(pathContext.UserPackagesFolder, "b", "1.0.0", "b.nuspec")));
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

                var spec = GetSpec(
                    Path.Combine(pathContext.SolutionRoot, "project", "fake.csproj"),
                    "a",
                    VersionRange.Parse("1.0.0"));

                spec.RestoreMetadata.OutputPath = Path.Combine(pathContext.SolutionRoot, "project", "obj");

                dgFile.AddProject(spec);
                dgFile.AddRestore(spec.Name);

                var toolContext = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };

                toolContext.PackageTypes.Add(PackageType.DotnetCliTool);
                toolContext.AddFile("lib/netcoreapp1.0/a.deps.json", GetDepsJson("a"));

                var bContext = new SimpleTestPackageContext()
                {
                    Id = "b",
                    Version = "1.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    toolContext,
                    bContext);

                // Act
                var result = await CommandsTestUtility.RunSingleRestore(dgFile, pathContext, logger);

                var outputPath = DotnetCliToolPathResolver.GetFilePath(spec.RestoreMetadata.OutputPath, "a");
                var outputFile = DotnetCliToolFile.Load(outputPath);

                // Assert
                Assert.True(result.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                Assert.True(File.Exists(outputPath));
                Assert.True(outputFile.Success);
                Assert.Equal(pathContext.UserPackagesFolder, outputFile.PackageFolders.First());
                Assert.Equal(pathContext.FallbackFolder, outputFile.PackageFolders.Skip(1).First());
                Assert.Equal(VersionRange.Parse("1.0.0"), outputFile.DependencyRange);
                Assert.Equal(NuGetVersion.Parse("1.0.0"), outputFile.ToolVersion);
                Assert.Equal("a", outputFile.ToolId);
            }
        }

        [Fact]
        public async Task DotnetCliTool_VerifyToolIsIgnoredAsDependency()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var dgFile = new DependencyGraphSpec();

                var spec = GetSpec(
                    Path.Combine(pathContext.SolutionRoot, "project", "fake.csproj"),
                    "myTool",
                    VersionRange.Parse("1.0.0"));

                spec.RestoreMetadata.OutputPath = Path.Combine(pathContext.SolutionRoot, "project", "obj");

                dgFile.AddProject(spec);
                dgFile.AddRestore(spec.Name);

                var toolContext = new SimpleTestPackageContext()
                {
                    Id = "myTool",
                    Version = "1.0.0"
                };

                toolContext.PackageTypes.Add(PackageType.DotnetCliTool);
                toolContext.AddFile("lib/netcoreapp1.0/a.dll");
                toolContext.AddFile("lib/netcoreapp1.0/a.deps.json", GetDepsJson("a"));

                var bContext = new SimpleTestPackageContext()
                {
                    Id = "b",
                    Version = "1.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    toolContext,
                    bContext);

                // Act
                var result = await CommandsTestUtility.RunSingleRestore(dgFile, pathContext, logger);

                var outputPath = DotnetCliToolPathResolver.GetFilePath(spec.RestoreMetadata.OutputPath, "myTool");
                var outputFile = DotnetCliToolFile.Load(outputPath);

                // Assert
                Assert.True(result.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                Assert.True(File.Exists(outputPath));
                Assert.True(outputFile.Success);
                Assert.Equal("myTool", outputFile.ToolId);
            }
        }

        [Fact]
        public async Task DotnetCliTool_BasicToolRestore_MultipleToolsInPackage()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var dgFile = new DependencyGraphSpec();

                var spec = GetSpec(
                    Path.Combine(pathContext.SolutionRoot, "project", "fake.csproj"),
                    "a",
                    VersionRange.Parse("1.0.0"));

                spec.RestoreMetadata.OutputPath = Path.Combine(pathContext.SolutionRoot, "project", "obj");

                dgFile.AddProject(spec);
                dgFile.AddRestore(spec.Name);

                var toolContext = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };

                toolContext.PackageTypes.Add(PackageType.DotnetCliTool);
                toolContext.AddFile("lib/netcoreapp1.0/dotnet-x.deps.json", GetDepsJson("x"));
                toolContext.AddFile("lib/netcoreapp1.0/dotnet-y.deps.json", GetDepsJson("y"));
                toolContext.AddFile("lib/netcoreapp1.0/dotnet-p.deps.json", GetDepsJson("p"));

                var bContext = new SimpleTestPackageContext()
                {
                    Id = "b",
                    Version = "1.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    toolContext,
                    bContext);

                // Act
                var result = await CommandsTestUtility.RunSingleRestore(dgFile, pathContext, logger);

                var outputPath = DotnetCliToolPathResolver.GetFilePath(spec.RestoreMetadata.OutputPath, "a");
                var outputFile = DotnetCliToolFile.Load(outputPath);

                // Assert
                Assert.True(result.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                Assert.True(File.Exists(outputPath));
                Assert.True(outputFile.Success);
                Assert.Equal(3, outputFile.DepsFiles.Single().Value.Count);
                Assert.EndsWith("dotnet-p.deps.json", outputFile.DepsFiles.Single().Value[0]);
                Assert.EndsWith("dotnet-x.deps.json", outputFile.DepsFiles.Single().Value[1]);
                Assert.EndsWith("dotnet-y.deps.json", outputFile.DepsFiles.Single().Value[2]);
            }
        }

        [Fact]
        public async Task DotnetCliTool_BasicToolRestore_MissingDependency()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var dgFile = new DependencyGraphSpec();

                var spec = GetSpec(
                    Path.Combine(pathContext.SolutionRoot, "project", "fake.csproj"),
                    "a",
                    VersionRange.Parse("1.0.0"));

                spec.RestoreMetadata.OutputPath = Path.Combine(pathContext.SolutionRoot, "project", "obj");

                dgFile.AddProject(spec);
                dgFile.AddRestore(spec.Name);

                var toolContext = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };

                toolContext.PackageTypes.Add(PackageType.DotnetCliTool);
                toolContext.AddFile("lib/netcoreapp1.0/a.deps.json", GetDepsJson("a"));

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    toolContext);

                // Act
                var result = await CommandsTestUtility.RunSingleRestore(dgFile, pathContext, logger);

                var outputPath = DotnetCliToolPathResolver.GetFilePath(spec.RestoreMetadata.OutputPath, "a");
                var outputFile = DotnetCliToolFile.Load(outputPath);

                // Assert
                Assert.False(result.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                Assert.True(File.Exists(outputPath));
                Assert.False(outputFile.Success);
                Assert.Equal(pathContext.UserPackagesFolder, outputFile.PackageFolders.First());
                Assert.Equal(pathContext.FallbackFolder, outputFile.PackageFolders.Skip(1).First());
                Assert.Equal(VersionRange.Parse("1.0.0"), outputFile.DependencyRange);
                Assert.Equal(NuGetVersion.Parse("1.0.0"), outputFile.ToolVersion);
                Assert.Equal("a", outputFile.ToolId);
                Assert.Equal(FileLogEntryType.Error, outputFile.Log.Single().Type);
                Assert.Contains("b (= 1.0.0)", outputFile.Log.Single().Message);
            }
        }

        [Fact]
        public async Task DotnetCliTool_BasicToolRestore_WithDuplicates_VerifyFileForAll()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var dgFile = new DependencyGraphSpec();

                for (int i = 0; i < 10; i++)
                {
                    var spec = GetSpec(
                        Path.Combine(pathContext.SolutionRoot, "fake.csproj"),
                        "a",
                        VersionRange.Parse("1.0.0"));

                    spec.RestoreMetadata.OutputPath = Path.Combine(pathContext.SolutionRoot, $"project{i}", "obj");

                    dgFile.AddProject(spec);
                    dgFile.AddRestore(spec.Name);
                }

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };

                packageA.PackageTypes.Add(PackageType.DotnetCliTool);
                packageA.AddFile("lib/netcoreapp1.0/a.deps.json", GetDepsJson("a"));

                var bContext = new SimpleTestPackageContext()
                {
                    Id = "b",
                    Version = "1.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, PackageSaveMode.Defaultv3, packageA, bContext);

                // Act
                var results = await CommandsTestUtility.RunRestore(dgFile, pathContext, logger);

                // Assert
                Assert.Equal(10, results.Count);
                Assert.All(results, e => Assert.True(e.Success));

                for (int i = 0; i < 10; i++)
                {
                    var path = DotnetCliToolPathResolver.GetFilePath(
                        Path.Combine(pathContext.SolutionRoot, $"project{i}", "obj"),
                        "a");

                    var file = DotnetCliToolFile.Load(path);

                    Assert.Equal("1.0.0", file.ToolVersion.ToString());
                }
            }
        }

        [Fact]
        public async Task DotnetCliTool_BasicToolRestore_DifferentVersionRanges_VerifyEachProjectHasItsOwnVersion()
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

                    var spec = GetSpec(
                        Path.Combine(pathContext.SolutionRoot, $"fake{i}.csproj"),
                        "a",
                        version);

                    spec.RestoreMetadata.OutputPath = Path.Combine(pathContext.SolutionRoot, $"project{i}", "obj");

                    dgFile.AddProject(spec);
                    dgFile.AddRestore(spec.Name);
                }

                foreach (var version in versions)
                {
                    var packageA = new SimpleTestPackageContext()
                    {
                        Id = "a",
                        Version = version.MinVersion.ToString()
                    };

                    packageA.PackageTypes.Add(PackageType.DotnetCliTool);
                    packageA.AddFile("lib/netcoreapp1.0/a.deps.json", GetDepsJson("a"));

                    await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, PackageSaveMode.Defaultv3, packageA);
                }

                var bContext = new SimpleTestPackageContext()
                {
                    Id = "b",
                    Version = "1.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3(pathContext.PackageSource, PackageSaveMode.Defaultv3, bContext);

                // Act
                var results = await CommandsTestUtility.RunRestore(dgFile, pathContext, logger);

                // Assert
                Assert.Equal(limit, results.Count);

                foreach (var result in results)
                {
                    Assert.True(result.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                }

                for (int i = 0; i < limit; i++)
                {
                    var path = DotnetCliToolPathResolver.GetFilePath(
                        Path.Combine(pathContext.SolutionRoot, $"project{i}", "obj"),
                        "a");

                    var file = DotnetCliToolFile.Load(path);

                    Assert.Equal($"{i + 1}.0.0", file.ToolVersion.ToString());
                }
            }
        }

        public static PackageSpec GetSpec(string projectFilePath, string id, VersionRange versionRange)
        {
            var name = $"{id}-{Guid.NewGuid().ToString()}";

            return new PackageSpec(new List<TargetFrameworkInformation>())
            {
                Name = name, // make sure this package never collides with a dependency
                FilePath = projectFilePath,
                Tools = new List<ToolDependency>(),
                Dependencies = new List<LibraryDependency>
                {
                    new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(id, versionRange, LibraryDependencyTarget.Package)
                    }
                },
                RestoreMetadata = new ProjectRestoreMetadata()
                {
                    OutputType = RestoreOutputType.DotnetCliTool,
                    ProjectName = name,
                    ProjectUniqueName = name,
                    ProjectPath = projectFilePath
                }
            };
        }

        private static string GetDepsJson(string name = "a", string dependencyName = "b")
        {
            return DepsJson.Replace("$TOOLNAME", name)
                .Replace("$DEPENDENCYNAME$", dependencyName);
        }

        private static string DepsJson = @"{
                      ""runtimeTarget"": {
                        ""name"": "".NETCoreApp,Version=v1.0"",
                        ""signature"": ""09db60146a5b8a0d40c5ea0fb7485ab3bbdd4a1a""
                      },
                      ""compilationOptions"": {},
                      ""targets"": {
                        "".NETCoreApp,Version=v1.0"": {
                          ""$TOOLNAME$/1.0.0"": {
                            ""dependencies"": {
                              ""$DEPENDENCYNAME$"": ""1.0.0""
                            },
                            ""runtime"": {
                              ""dotnetnew.dll"": {}
                            }
                          },
                          ""$DEPENDENCYNAME$/1.0.0"": {
                            ""dependencies"": {
                              ""System.Runtime.Serialization.Primitives"": ""4.1.1""
                            },
                            ""runtime"": {
                              ""lib/netstandard1.0/Newtonsoft.Json.dll"": {}
                            }
                          },
                          ""System.Runtime.Serialization.Primitives/4.1.1"": {
                            ""runtime"": {
                              ""lib/netstandard1.3/System.Runtime.Serialization.Primitives.dll"": {}
                            }
                          }
                        }
                      },
                      ""libraries"": {
                        ""$TOOLNAME$/1.0.0"": {
                          ""type"": ""project"",
                          ""serviceable"": false,
                          ""sha512"": """"
                        },
                        ""$DEPENDENCYNAME$/1.0.0"": {
                          ""type"": ""package"",
                          ""serviceable"": true,
                          ""sha512"": ""sha512-U82mHQSKaIk+lpSVCbWYKNavmNH1i5xrExDEquU1i6I5pV6UMOqRnJRSlKO3cMPfcpp0RgDY+8jUXHdQ4IfXvw==""
                        }
                      }
                    }";
    }
}