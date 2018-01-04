// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class RestoreRunnerTests
    {
        [Fact]
        public async Task RestoreRunner_BasicRestore()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var workingDir = TestDirectory.Create())
            {
                var projectName = "project1";
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", projectName));
                
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var dgPath = Path.Combine(project1.FullName, "project.dg");

                File.WriteAllText(specPath1, project1Json);

                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, projectName , specPath1);

                spec1 = spec1.EnsureRestoreMetadata();
                spec1.RestoreMetadata.Sources = new List<PackageSource> { new PackageSource(packageSource.FullName) };
                spec1.RestoreMetadata.PackagesPath = packagesDir.FullName;
                var dgSpec = new DependencyGraphSpec();
                dgSpec.AddProject(spec1);
                dgSpec.AddRestore(projectName);

                var logger = new TestLogger();
                var lockPath = Path.Combine(project1.FullName, "project.assets.json");

                var sourceRepos = sources.Select(source => Repository.Factory.GetCoreV3(source.Source)).ToList();

                var providerCache = new RestoreCommandProvidersCache();
                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        GlobalPackagesFolder = packagesDir.FullName,
                        Sources = new List<string>() { packageSource.FullName },
                        Log = logger,
                        CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(sources)),
                        PreLoadedRequestProviders = new List<IPreLoadedRestoreRequestProvider>()
                        {
                            new DependencyGraphSpecRequestProvider(providerCache, dgSpec)
                        }
                    };

                    // Act
                    var summaries = await RestoreRunner.RunAsync(restoreContext);
                    var summary = summaries.Single();

                    // Assert
                    Assert.True(summary.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                    Assert.Equal(1, summary.FeedsUsed.Count);
                    Assert.True(File.Exists(lockPath), lockPath);
                    Assert.False(File.Exists(Path.Combine(project1.FullName, "project1.nuget.targets")));
                    Assert.False(File.Exists(Path.Combine(project1.FullName, "project1.nuget.props")));
                }
            }
        }

        [Fact]
        public async Task RestoreRunner_BasicRestore_BuildAssetsChanged()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var workingDir = TestDirectory.Create())
            {
                var projectName = "project1";
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", projectName));

                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var dgPath = Path.Combine(project1.FullName, "project.dg");

                File.WriteAllText(specPath1, project1Json);

                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, projectName, specPath1);

                spec1 = spec1.EnsureRestoreMetadata();
                spec1.RestoreMetadata.Sources = new List<PackageSource> { new PackageSource(packageSource.FullName) };
                spec1.RestoreMetadata.PackagesPath = packagesDir.FullName;
                var dgSpec = new DependencyGraphSpec();
                dgSpec.AddProject(spec1);
                dgSpec.AddRestore(projectName);

                var logger = new TestLogger();
                var lockPath = Path.Combine(project1.FullName, "project.assets.json");

                var sourceRepos = sources.Select(source => Repository.Factory.GetCoreV3(source.Source)).ToList();

                var providerCache = new RestoreCommandProvidersCache();
                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        GlobalPackagesFolder = packagesDir.FullName,
                        Sources = new List<string>() { packageSource.FullName },
                        Log = logger,
                        CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(sources)),
                        PreLoadedRequestProviders = new List<IPreLoadedRestoreRequestProvider>()
                        {
                            new DependencyGraphSpecRequestProvider(providerCache, dgSpec)
                        },
                        AllowNoOp = true
                    };

                    // Act
                    var summaries = await RestoreRunner.RunAsync(restoreContext);
                    var summary = summaries.Single();

                    // Assert
                    Assert.True(summary.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                    Assert.True(summary.BuildFilesChanged, "Build files did not change");
                    Assert.Equal(1, summary.FeedsUsed.Count);
                    Assert.True(File.Exists(lockPath), lockPath);
                    Assert.False(File.Exists(Path.Combine(project1.FullName, "project1.nuget.targets")));
                    Assert.False(File.Exists(Path.Combine(project1.FullName, "project1.nuget.props")));

                    // Act again
                    var noopsummaries = await RestoreRunner.RunAsync(restoreContext);
                    var noopsummary = noopsummaries.Single();

                    // Assert
                    Assert.True(noopsummary.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                    Assert.True(noopsummary.NoOpRestore);
                    Assert.False(noopsummary.BuildFilesChanged, "Build files did change");
                    Assert.Equal(1, noopsummary.FeedsUsed.Count);
                    Assert.True(File.Exists(lockPath), lockPath);
                    Assert.False(File.Exists(Path.Combine(project1.FullName, "project1.nuget.targets")));
                    Assert.False(File.Exists(Path.Combine(project1.FullName, "project1.nuget.props")));
                }
            }
        }

        [Fact]
        public async Task RestoreRunner_BasicRestore_VerifyFailureWritesFiles()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                    ""dependencies"": {
                        ""x"": ""1.0.0""
                    }
                }
              }
            }";

            using (var workingDir = TestDirectory.Create())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                spec1 = spec1.EnsureRestoreMetadata();
                spec1.RestoreMetadata.Sources = new List<PackageSource> { new PackageSource(packageSource.FullName) };
                spec1.RestoreMetadata.PackagesPath = packagesDir.FullName;
                var dgFile = new DependencyGraphSpec();
                dgFile.AddProject(spec1);
                dgFile.AddRestore("project1");

                var logger = new TestLogger();
                var lockPath = Path.Combine(project1.FullName, "project.assets.json");

                var sourceRepos = sources.Select(source => Repository.Factory.GetCoreV3(source.Source)).ToList();

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("build/net45/x.targets");

                var packageY = new SimpleTestPackageContext("y");
                packageX.Dependencies.Add(packageY);

                var yPath = SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, packageY);
                SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, packageX);

                // y does not exist
                yPath.Delete();

                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        GlobalPackagesFolder = packagesDir.FullName,
                        Sources = new List<string>() { packageSource.FullName },
                        Log = logger,
                        CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(sources)),
                        PreLoadedRequestProviders = new List<IPreLoadedRestoreRequestProvider>()
                        {
                            new DependencyGraphSpecRequestProvider(providerCache, dgFile)
                        }
                    };

                    var targetsPath = Path.Combine(project1.FullName, "project1.csproj.nuget.g.targets");
                    var propsPath = Path.Combine(project1.FullName, "project1.nuget.props");

                    // Act
                    var summaries = await RestoreRunner.RunAsync(restoreContext);
                    var summary = summaries.Single();

                    var targets = TargetsUtility.GetMSBuildPackageImports(targetsPath);

                    // Assert
                    Assert.False(summary.Success);
                    Assert.True(File.Exists(lockPath), lockPath);
                    Assert.True(File.Exists(targetsPath));
                    Assert.False(File.Exists(propsPath));
                    Assert.Equal(1, targets.Count);
                }
            }
        }

        [Fact]
        public async Task RestoreRunner_BasicRestore_VerifyFailureWritesFiles_NETCore()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                    ""dependencies"": {
                        ""x"": ""1.0.0""
                    }
                }
              }
            }";

            using (var workingDir = TestDirectory.Create())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                spec1.RestoreMetadata = new ProjectRestoreMetadata();
                spec1.RestoreMetadata.OutputPath = Path.Combine(project1.FullName, "obj");
                spec1.RestoreMetadata.ProjectStyle = ProjectStyle.PackageReference;
                spec1.RestoreMetadata.ProjectName = "project1";
                spec1.RestoreMetadata.ProjectPath = Path.Combine(project1.FullName, "project1.csproj");
                spec1.RestoreMetadata.ProjectUniqueName = spec1.RestoreMetadata.ProjectPath;
                spec1.RestoreMetadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(NuGetFramework.Parse("net45")));
                spec1.RestoreMetadata.OriginalTargetFrameworks.Add("net45");
                spec1.FilePath = spec1.RestoreMetadata.ProjectPath;

                var dgSpec = new DependencyGraphSpec();
                dgSpec.AddProject(spec1);
                dgSpec.AddRestore(spec1.RestoreMetadata.ProjectUniqueName);

                var logger = new TestLogger();
                var assetsPath = Path.Combine(project1.FullName, "obj", "project.assets.json");

                var sourceRepos = sources.Select(source => Repository.Factory.GetCoreV3(source.Source)).ToList();

                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        GlobalPackagesFolder = packagesDir.FullName,
                        Sources = new List<string>() { packageSource.FullName },
                        Log = logger,
                        CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(sources)),
                        PreLoadedRequestProviders = new List<IPreLoadedRestoreRequestProvider>()
                        {
                            new DependencyGraphSpecRequestProvider(providerCache, dgSpec)
                        }
                    };

                    // Act
                    var summaries = await RestoreRunner.RunAsync(restoreContext);
                    var summary = summaries.Single();

                    // Assert
                    Assert.False(summary.Success);
                    Assert.True(File.Exists(assetsPath), assetsPath);
                    Assert.True(File.Exists(Path.Combine(project1.FullName, "obj", "project1.csproj.nuget.g.props")));
                    Assert.True(File.Exists(Path.Combine(project1.FullName, "obj", "project1.csproj.nuget.g.targets")));
                }
            }
        }

        [Fact]
        public async Task RestoreRunner_BasicRestoreWithConfigFile()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var configFile = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""{0}"" />
    </packageSources>
</configuration>
";

            using (var workingDir = TestDirectory.Create())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(workingDir, "NuGet.Config"), String.Format(configFile, packageSource.FullName));

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var configPath = Path.Combine(workingDir, "NuGet.Config");

                var dgFile = new DependencyGraphSpec();
                spec1 = spec1.EnsureRestoreMetadata();
                spec1.RestoreMetadata.ConfigFilePaths = new List<string> { configPath };
                spec1.RestoreMetadata.Sources = new List<PackageSource> { new PackageSource(packageSource.FullName) };
                spec1.RestoreMetadata.PackagesPath = packagesDir.FullName;

                dgFile.AddProject(spec1);
                dgFile.AddRestore("project1");

                var logger = new TestLogger();
                var lockPath = Path.Combine(project1.FullName, "project.assets.json");

                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        GlobalPackagesFolder = spec1.RestoreMetadata.PackagesPath,
                        ConfigFile = configPath,
                        Log = logger,
                        CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(new List<PackageSource>())),
                        PreLoadedRequestProviders = new List<IPreLoadedRestoreRequestProvider>
                        {
                            new DependencyGraphSpecRequestProvider(providerCache, dgFile)
                        }
                    };

                    // Act
                    var summaries = await RestoreRunner.RunAsync(restoreContext);
                    var summary = summaries.Single();

                    // Assert
                    Assert.True(summary.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                    Assert.Equal(1, summary.FeedsUsed.Count);
                    Assert.True(File.Exists(lockPath), lockPath);
                }
            }
        }

        [Fact]
        public async Task RestoreRunner_RestoreWithExternalFile()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var targetFrameworkInfo1 = new TargetFrameworkInformation();
            targetFrameworkInfo1.FrameworkName = NuGetFramework.Parse("net45");
            var frameworks1 = new[] { targetFrameworkInfo1 };

            var targetFrameworkInfo2 = new TargetFrameworkInformation();
            targetFrameworkInfo2.FrameworkName = NuGetFramework.Parse("net45");
            var frameworks2 = new[] { targetFrameworkInfo2 };

            // Create two net45 projects
            var spec1 = new PackageSpec(frameworks1);
            spec1.RestoreMetadata = new ProjectRestoreMetadata();
            spec1.RestoreMetadata.ProjectUniqueName = "project1";
            spec1.RestoreMetadata.ProjectName = "project1";
            spec1.RestoreMetadata.ProjectStyle = ProjectStyle.PackageReference;
            spec1.RestoreMetadata.OriginalTargetFrameworks.Add("net45");

            var spec2 = new PackageSpec(frameworks2);
            spec2.RestoreMetadata = new ProjectRestoreMetadata();
            spec2.RestoreMetadata.ProjectUniqueName = "project2";
            spec2.RestoreMetadata.ProjectName = "project2";
            spec2.RestoreMetadata.ProjectStyle = ProjectStyle.PackageReference;
            spec2.RestoreMetadata.OriginalTargetFrameworks.Add("net45");

            var specs = new[] { spec1, spec2 };

            using (var workingDir = TestDirectory.Create())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                project2.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var projPath1 = Path.Combine(project1.FullName, "project1.csproj");
                var projPath2 = Path.Combine(project2.FullName, "project2.csproj");
                File.WriteAllText(projPath1, string.Empty);
                File.WriteAllText(projPath2, string.Empty);

                spec1.RestoreMetadata.ProjectPath = projPath1;
                spec1.FilePath = projPath1;
                spec1.Name = "project1";
                spec2.RestoreMetadata.ProjectPath = projPath2;
                spec2.FilePath = projPath1;
                spec2.Name = "project2";

                var logger = new TestLogger();
                var objPath1 = Path.Combine(project1.FullName, "obj");
                var objPath2 = Path.Combine(project2.FullName, "obj");

                spec1.RestoreMetadata.OutputPath = objPath1;
                spec2.RestoreMetadata.OutputPath = objPath2;

                spec1.RestoreMetadata.OriginalTargetFrameworks.Add("net45");
                spec2.RestoreMetadata.OriginalTargetFrameworks.Add("net45");

                var lockPath1 = Path.Combine(objPath1, "project.assets.json");
                var lockPath2 = Path.Combine(objPath2, "project.assets.json");

                // Link projects
                spec1.TargetFrameworks.Single().Dependencies.Add(new LibraryDependency()
                {
                    LibraryRange = new LibraryRange()
                    {
                        Name = "project2",
                        TypeConstraint = LibraryDependencyTarget.ExternalProject
                    }
                });

                spec1.RestoreMetadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(NuGetFramework.Parse("net45")));
                spec1.RestoreMetadata.TargetFrameworks
                    .Single()
                    .ProjectReferences
                    .Add(new ProjectRestoreReference()
                {
                    ProjectPath = projPath2,
                    ProjectUniqueName = "project2"
                });

                // Create dg file
                var dgFile = new DependencyGraphSpec();

                foreach (var spec in specs)
                {
                    dgFile.AddRestore(spec.RestoreMetadata.ProjectName);
                    dgFile.AddProject(spec);
                }

                var dgPath = Path.Combine(workingDir, "input.dg");
                dgFile.Save(dgPath);

                var sourceRepos = sources.Select(source => Repository.Factory.GetCoreV3(source.Source)).ToList();

                var providerCache = new RestoreCommandProvidersCache();

                var restoreContext = new RestoreArgs()
                {
                    CacheContext = new SourceCacheContext(),
                    DisableParallel = true,
                    GlobalPackagesFolder = packagesDir.FullName,
                    Sources = new List<string>() { packageSource.FullName },
                    Log = logger,
                    CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(sources)),
                    RequestProviders = new List<IRestoreRequestProvider>()
                    {
                        new DependencyGraphFileRequestProvider(providerCache)
                    }
                };

                // add file path as input
                restoreContext.Inputs.Add(dgPath);

                // Act
                var summaries = await RestoreRunner.RunAsync(restoreContext);
                var success = summaries.All(s => s.Success);

                var lockFormat = new LockFileFormat();
                var lockFile1 = lockFormat.Read(lockPath1);
                var project2Lib = lockFile1.Libraries.First();

                // Assert
                Assert.True(success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                Assert.True(File.Exists(lockPath1), lockPath1);
                Assert.True(File.Exists(lockPath2), lockPath2);
                Assert.Equal("project2", project2Lib.Name);
            }
        }

        [Fact]
        public async Task RestoreRunner_RestoreWithExternalFile_NetCoreOutput()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var targetFrameworkInfo1 = new TargetFrameworkInformation();
            targetFrameworkInfo1.FrameworkName = NuGetFramework.Parse("net45");
            var frameworks1 = new[] { targetFrameworkInfo1 };

            var targetFrameworkInfo2 = new TargetFrameworkInformation();
            targetFrameworkInfo2.FrameworkName = NuGetFramework.Parse("net45");
            var frameworks2 = new[] { targetFrameworkInfo2 };

            // Create two net45 projects
            var spec1 = new PackageSpec(frameworks1);
            spec1.RestoreMetadata = new ProjectRestoreMetadata();
            spec1.RestoreMetadata.ProjectUniqueName = "project1";
            spec1.RestoreMetadata.ProjectName = "project1";
            spec1.RestoreMetadata.ProjectStyle = ProjectStyle.PackageReference;
            spec1.RestoreMetadata.OriginalTargetFrameworks.Add("net45");

            var spec2 = new PackageSpec(frameworks2);
            spec2.RestoreMetadata = new ProjectRestoreMetadata();
            spec2.RestoreMetadata.ProjectUniqueName = "project2";
            spec2.RestoreMetadata.ProjectName = "project2";
            spec2.RestoreMetadata.ProjectStyle = ProjectStyle.PackageReference;
            spec2.RestoreMetadata.OriginalTargetFrameworks.Add("net45");

            var specs = new[] { spec1, spec2 };

            using (var workingDir = TestDirectory.Create())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                project2.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var projPath1 = Path.Combine(project1.FullName, "project1.csproj");
                var projPath2 = Path.Combine(project2.FullName, "project2.csproj");
                File.WriteAllText(projPath1, string.Empty);
                File.WriteAllText(projPath2, string.Empty);

                spec1.RestoreMetadata.ProjectPath = projPath1;
                spec1.FilePath = projPath1;
                spec1.Name = "project1";
                spec2.RestoreMetadata.ProjectPath = projPath2;
                spec2.FilePath = projPath1;
                spec2.Name = "project2";

                var logger = new TestLogger();
                var objPath1 = Path.Combine(project1.FullName, "obj");
                var objPath2 = Path.Combine(project2.FullName, "obj");

                spec1.RestoreMetadata.OutputPath = objPath1;
                spec2.RestoreMetadata.OutputPath = objPath2;

                spec1.RestoreMetadata.OriginalTargetFrameworks.Add("net45");
                spec2.RestoreMetadata.OriginalTargetFrameworks.Add("net45");

                var lockPath1 = Path.Combine(objPath1, "project.assets.json");
                var lockPath2 = Path.Combine(objPath2, "project.assets.json");

                // Link projects
                spec1.TargetFrameworks.Single().Dependencies.Add(new LibraryDependency()
                {
                    LibraryRange = new LibraryRange()
                    {
                        Name = "project2",
                        TypeConstraint = LibraryDependencyTarget.ExternalProject
                    }
                });

                spec1.RestoreMetadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(NuGetFramework.Parse("net45")));
                spec1.RestoreMetadata.TargetFrameworks
                    .Single()
                    .ProjectReferences
                    .Add(new ProjectRestoreReference()
                    {
                        ProjectPath = projPath2,
                        ProjectUniqueName = "project2"
                    });

                // Create dg file
                var dgFile = new DependencyGraphSpec();

                foreach (var spec in specs)
                {
                    dgFile.AddRestore(spec.RestoreMetadata.ProjectName);
                    dgFile.AddProject(spec);
                }

                var dgPath = Path.Combine(workingDir, "input.dg");
                dgFile.Save(dgPath);

                var sourceRepos = sources.Select(source => Repository.Factory.GetCoreV3(source.Source)).ToList();

                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        GlobalPackagesFolder = packagesDir.FullName,
                        Sources = new List<string>() { packageSource.FullName },
                        Inputs = new List<string>() { dgPath },
                        Log = logger,
                        CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(sources)),
                        RequestProviders = new List<IRestoreRequestProvider>()
                        {
                            new DependencyGraphFileRequestProvider(providerCache)
                        }
                    };

                    // Act
                    var summaries = await RestoreRunner.RunAsync(restoreContext);
                    var success = summaries.All(s => s.Success);

                    var lockFormat = new LockFileFormat();
                    var lockFile1 = lockFormat.Read(lockPath1);
                    var project2Lib = lockFile1.Libraries.First();

                    // Assert
                    Assert.True(success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                    Assert.True(File.Exists(lockPath1), lockPath1);
                    Assert.True(File.Exists(lockPath2), lockPath2);
                    Assert.Equal("project2", project2Lib.Name);
                }
            }
        }

        [Fact]
        public async Task RestoreRunner_RestoreWithRuntime()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var workingDir = TestDirectory.Create())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                spec1 = spec1.EnsureRestoreMetadata();
                spec1.RestoreMetadata.Sources = new List<PackageSource> { new PackageSource(packageSource.FullName) };
                spec1.RestoreMetadata.PackagesPath = packagesDir.FullName;
                var dgSpec = new DependencyGraphSpec();
                dgSpec.AddProject(spec1);
                dgSpec.AddRestore("project1");

                var logger = new TestLogger();
                var lockPath1 = Path.Combine(project1.FullName, "project.assets.json");

                var sourceRepos = sources.Select(source => Repository.Factory.GetCoreV3(source.Source)).ToList();

                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        GlobalPackagesFolder = spec1.RestoreMetadata.PackagesPath,
                        Sources = new List<string>() { packageSource.FullName },
                        Log = logger,
                        CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(sources)),
                        PreLoadedRequestProviders = new List<IPreLoadedRestoreRequestProvider>()
                        {
                             new DependencyGraphSpecRequestProvider(providerCache, dgSpec)
                        }
                    };

                    restoreContext.Runtimes.Add("linux-x86");

                    // Act
                    var summaries = await RestoreRunner.RunAsync(restoreContext);
                    var success = summaries.All(s => s.Success);

                    var lockFormat = new LockFileFormat();
                    var lockFile = lockFormat.Read(lockPath1);

                    // Assert
                    Assert.True(success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                    Assert.True(lockFile.Targets.Any(graph => graph.RuntimeIdentifier == "linux-x86"));
                }
            }
        }

        [Fact]
        public async Task RestoreRunner_WarnIfNoProject()
        {
            // If an input folder is provided to RestoreRunner that doesn't contain a project,
            // it should report an error.

            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var logger = new TestLogger();
                var providerCache = new RestoreCommandProvidersCache();
                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        Inputs = new List<string>() { workingDir },
                        Log = logger,
                        RequestProviders = new List<IRestoreRequestProvider>()
                        {
                            new ProjectJsonRestoreRequestProvider(providerCache)
                        }
                    };

                    // Act
                    var summaries = await RestoreRunner.RunAsync(restoreContext);

                    // Assert
                    Assert.Equal(0, summaries.Count);
                    var matchingError = logger.Messages.ToList().Find(error => error.Contains(workingDir));
                    Assert.NotNull(matchingError);
                }
            }
        }
    }
}
