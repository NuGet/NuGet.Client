using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class NETCoreProject2ProjectTests
    {
        [Fact]
        public async Task NETCoreProject2Project_IgnoreXproj()
        {
            // Arrange
            using (var cacheContext = new SourceCacheContext())
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var sources = new List<PackageSource>();
                sources.Add(new PackageSource(pathContext.PackageSource));

                var spec = GetProject(projectName: "projectA", framework: "netstandard1.6");
                var specs = new[] { spec };

                spec.TargetFrameworks.Single().Dependencies.Add(new LibraryDependency()
                {
                    LibraryRange = new LibraryRange("x", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package)
                });

                // Create fake projects, the real data is in the specs
                var projects = CreateProjectsFromSpecs(pathContext, spec);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };

                packageX.Dependencies.Add(packageY);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageY);

                // Create dg file
                var dgFile = new DependencyGraphSpec();

                // Only add projectA
                dgFile.AddProject(spec);
                dgFile.AddRestore(spec.RestoreMetadata.ProjectUniqueName);

                dgFile.Save(Path.Combine(pathContext.WorkingDirectory, "out.dg"));

                var projectYRoot = Path.Combine(pathContext.SolutionRoot, "y");
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
                var summaries = await RunRestore(pathContext, logger, sources, dgFile, cacheContext);
                var success = summaries.All(s => s.Success);

                // Assert
                Assert.True(success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));

                // Verify only packages
                Assert.Empty(projects[0].AssetsFile.Libraries.Where(e => e.Type != "package"));
            }
        }

        [Fact]
        public async Task NETCoreProject2Project_ProjectReferenceOnlyUnderRestoreMetadata()
        {
            // Arrange
            using (var cacheContext = new SourceCacheContext())
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var sources = new List<PackageSource>();
                sources.Add(new PackageSource(pathContext.PackageSource));

                var spec1 = GetProject(projectName: "projectA", framework: "netstandard1.6");
                var spec2 = GetProject(projectName: "projectB", framework: "netstandard1.3");

                var specs = new[] { spec1, spec2 };

                // Create fake projects, the real data is in the specs
                var projects = CreateProjectsFromSpecs(pathContext, specs);

                // Link projects
                spec1.RestoreMetadata.TargetFrameworks.Single().ProjectReferences.Add(new ProjectRestoreReference()
                {
                    ProjectPath = projects[1].ProjectPath,
                    ProjectUniqueName = spec2.RestoreMetadata.ProjectUniqueName,
                });

                // Create dg file
                var dgFile = new DependencyGraphSpec();
                foreach (var spec in specs)
                {
                    dgFile.AddProject(spec);
                }

                // Restore only the first one
                dgFile.AddRestore(spec1.RestoreMetadata.ProjectUniqueName);

                dgFile.Save(Path.Combine(pathContext.WorkingDirectory, "out.dg"));

                // Act
                var summaries = await RunRestore(pathContext, logger, sources, dgFile, cacheContext);
                var success = summaries.All(s => s.Success);

                // Assert
                Assert.True(success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));

                var targetLib = projects[0].AssetsFile
                    .Targets
                    .Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("netstandard1.6")))
                    .Libraries
                    .Single(e => e.Name == "projectB");

                var libraryLib = projects[0].AssetsFile
                    .Libraries
                    .Single(e => e.Name == "projectB");

                Assert.Equal("projectB", targetLib.Name);
                Assert.Equal(NuGetFramework.Parse("netstandard1.3"), NuGetFramework.Parse(targetLib.Framework));
                Assert.Equal("1.0.0", targetLib.Version.ToNormalizedString());
                Assert.Equal("project", targetLib.Type);

                Assert.Equal("projectB", libraryLib.Name);
                Assert.Equal("project", libraryLib.Type);
                Assert.Equal("../projectB/projectB.csproj", libraryLib.MSBuildProject);
                Assert.Equal("../projectB/projectB.csproj", libraryLib.Path);
                Assert.Equal("1.0.0", libraryLib.Version.ToNormalizedString());
            }
        }

        [Fact]
        public async Task NETCoreProject2Project_ProjectReference_IgnoredForTFM()
        {
            // Arrange
            using (var cacheContext = new SourceCacheContext())
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var sources = new List<PackageSource>();
                sources.Add(new PackageSource(pathContext.PackageSource));

                var spec1 = GetProject(projectName: "projectA", framework: "netstandard1.6");
                var spec2 = GetProject(projectName: "projectB", framework: "netstandard1.3");

                var specs = new[] { spec1, spec2 };

                // Create fake projects, the real data is in the specs
                var projects = CreateProjectsFromSpecs(pathContext, specs);

                // Remove valid link
                spec1.RestoreMetadata.TargetFrameworks.Clear();

                // Add invalid link, net45 is not a project tfm
                spec1.RestoreMetadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(NuGetFramework.Parse("net45")));
                spec1.RestoreMetadata.TargetFrameworks.Single().ProjectReferences.Add(new ProjectRestoreReference()
                {
                    ProjectPath = projects[1].ProjectPath,
                    ProjectUniqueName = spec2.RestoreMetadata.ProjectUniqueName
                });

                // Create dg file
                var dgFile = new DependencyGraphSpec();
                foreach (var spec in specs)
                {
                    dgFile.AddProject(spec);
                }

                // Restore only the first one
                dgFile.AddRestore(spec1.RestoreMetadata.ProjectUniqueName);

                dgFile.Save(Path.Combine(pathContext.WorkingDirectory, "out.dg"));

                // Act
                var summaries = await RunRestore(pathContext, logger, sources, dgFile, cacheContext);
                var success = summaries.All(s => s.Success);

                // Assert
                Assert.True(success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                Assert.Equal(0, projects[0].AssetsFile.Libraries.Count);
            }
        }

        [Fact]
        public async Task NETCoreProject2Project_ProjectMissing()
        {
            // Arrange
            using (var cacheContext = new SourceCacheContext())
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var sources = new List<PackageSource>();
                sources.Add(new PackageSource(pathContext.PackageSource));

                var spec1 = GetProject(projectName: "projectA", framework: "netstandard1.6");
                var spec2 = GetProject(projectName: "projectB", framework: "netstandard1.3");

                var specs = new[] { spec1, spec2 };

                // Create fake projects, the real data is in the specs
                var projects = CreateProjectsFromSpecs(pathContext, specs);

                // Link projects
                spec1.RestoreMetadata.TargetFrameworks.Single().ProjectReferences.Add(new ProjectRestoreReference()
                {
                    ProjectPath = projects[1].ProjectPath,
                    ProjectUniqueName = spec2.RestoreMetadata.ProjectUniqueName,
                });
                
                // Create dg file
                var dgFile = new DependencyGraphSpec();

                // Only add projectA
                dgFile.AddProject(spec1);
                dgFile.AddRestore(spec1.RestoreMetadata.ProjectUniqueName);

                dgFile.Save(Path.Combine(pathContext.WorkingDirectory, "out.dg"));

                // Act
                var summaries = await RunRestore(pathContext, logger, sources, dgFile, cacheContext);
                var success = summaries.All(s => s.Success);

                // Assert
                Assert.False(success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                Assert.Contains("Unable to resolve", string.Join(Environment.NewLine, logger.Messages));
                Assert.Contains(projects[1].ProjectPath, string.Join(Environment.NewLine, logger.Messages));
            }
        }

        [Fact]
        public async Task NETCoreProject2Project_ProjectFileDependencyGroupsForNETCore()
        {
            // Arrange
            using (var cacheContext = new SourceCacheContext())
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var sources = new List<PackageSource>();
                sources.Add(new PackageSource(pathContext.PackageSource));

                var spec1 = GetProject(projectName: "projectA", framework: "netstandard1.6");
                var spec2 = GetProject(projectName: "projectB", framework: "netstandard1.3");
                var spec3 = GetProject(projectName: "projectC", framework: "netstandard1.0");

                var specs = new[] { spec1, spec2, spec3 };

                // Create fake projects, the real data is in the specs
                var projects = CreateProjectsFromSpecs(pathContext, specs);

                // Link projects
                spec1.RestoreMetadata.TargetFrameworks.Single().ProjectReferences.Add(new ProjectRestoreReference()
                {
                    ProjectPath = projects[1].ProjectPath,
                    ProjectUniqueName = spec2.RestoreMetadata.ProjectUniqueName,
                });

                spec2.RestoreMetadata.TargetFrameworks.Single().ProjectReferences.Add(new ProjectRestoreReference()
                {
                    ProjectPath = projects[2].ProjectPath,
                    ProjectUniqueName = spec3.RestoreMetadata.ProjectUniqueName,
                });

                // Create dg file
                var dgFile = new DependencyGraphSpec();
                foreach (var spec in specs)
                {
                    dgFile.AddProject(spec);
                }

                // Restore only the first one
                dgFile.AddRestore(spec1.RestoreMetadata.ProjectUniqueName);

                dgFile.Save(Path.Combine(pathContext.WorkingDirectory, "out.dg"));

                // Act
                var summaries = await RunRestore(pathContext, logger, sources, dgFile, cacheContext);
                var success = summaries.All(s => s.Success);

                // Assert
                Assert.True(success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                var dependencies = projects[0].AssetsFile.ProjectFileDependencyGroups.SelectMany(e => e.Dependencies).ToArray();

                // Ensure ProjectC does not show up
                Assert.Equal(1, dependencies.Length);

                // Ensure ProjectC is in the libraries
                Assert.Equal(2, projects[0].AssetsFile.Libraries.Count);

                // Verify the project name is used not the path or unique name
                Assert.Equal("projectB", dependencies[0]);
            }
        }

        private static List<SimpleTestProjectContext> CreateProjectsFromSpecs(SimpleTestPathContext pathContext, params PackageSpec[] specs)
        {
            var projects = new List<SimpleTestProjectContext>();

            foreach (var spec in specs)
            {
                var project = new SimpleTestProjectContext(spec.Name, RestoreOutputType.NETCore, pathContext.SolutionRoot); 

                // Set proj properties
                spec.FilePath = project.ProjectPath;
                spec.RestoreMetadata.OutputPath = project.OutputPath;
                spec.RestoreMetadata.ProjectPath = project.ProjectPath;

                projects.Add(project);
            }

            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot, projects.ToArray());
            solution.Create(pathContext.SolutionRoot);

            return projects;
        }

        private static async Task<IReadOnlyList<RestoreSummary>> RunRestore(
            SimpleTestPathContext pathContext,
            TestLogger logger,
            List<PackageSource> sources,
            DependencyGraphSpec dgFile,
            SourceCacheContext cacheContext)
        {
            var restoreContext = new RestoreArgs()
            {
                CacheContext = cacheContext,
                DisableParallel = true,
                GlobalPackagesFolder = pathContext.UserPackagesFolder,
                Sources = new List<string>() { pathContext.PackageSource },
                Log = logger,
                CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(sources)),
                PreLoadedRequestProviders = new List<IPreLoadedRestoreRequestProvider>()
                {
                    new DependencyGraphSpecRequestProvider(new RestoreCommandProvidersCache(), dgFile)
                }
            };

            return await RestoreRunner.Run(restoreContext);
        }

        private static PackageSpec GetProject(string projectName, string framework)
        {
            var targetFrameworkInfo = new TargetFrameworkInformation();
            targetFrameworkInfo.FrameworkName = NuGetFramework.Parse(framework);
            var frameworks = new[] { targetFrameworkInfo };

            // Create two net45 projects
            var spec = new PackageSpec(frameworks);
            spec.RestoreMetadata = new ProjectRestoreMetadata();
            spec.RestoreMetadata.ProjectUniqueName = $"{projectName}-UNIQUENAME";
            spec.RestoreMetadata.ProjectName = projectName;
            spec.RestoreMetadata.OutputType = RestoreOutputType.NETCore;
            spec.RestoreMetadata.OriginalTargetFrameworks.Add(framework);
            spec.Name = projectName;
            spec.RestoreMetadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(targetFrameworkInfo.FrameworkName));

            return spec;
        }
    }
}
