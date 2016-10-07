using System;
using System.Collections.Generic;
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
    public class RestoreProject2ProjectTests
    {
        [Fact]
        public async Task RestoreProject2Project_ProjectReferenceOnlyUnderRestoreMetadata()
        {
            // Arrange
            using (var cacheContext = new SourceCacheContext())
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var sources = new List<PackageSource>();
                sources.Add(new PackageSource(pathContext.PackageSource));

                var spec1 = GetProject(projectName: "projectA", framework: "netstandard1.6");
                var spec2 = GetProject(projectName: "projectB", framework: "netstandard1.6");

                var specs = new[] { spec1, spec2 };

                // Create fake projects, the real data is in the specs
                var projects = CreateProjectsFromSpecs(pathContext, specs);

                // Link projects
                spec1.RestoreMetadata.ProjectReferences.Add(new ProjectRestoreReference()
                {
                    ProjectPath = projects[1].ProjectPath,
                    ProjectUniqueName = projects[1].ProjectPath,
                });

                // Create dg file
                var dgFile = new DependencyGraphSpec();
                foreach (var spec in specs)
                {
                    dgFile.AddRestore(spec.RestoreMetadata.ProjectUniqueName);
                    dgFile.AddProject(spec);
                }

                // Act
                var summaries = await RunRestore(pathContext, logger, sources, dgFile, cacheContext);
                var success = summaries.All(s => s.Success);

                // Assert
                Assert.True(success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));

                var targetLib = projects[0].AssetsFile
                    .Targets
                    .Single(e => e.TargetFramework == NuGetFramework.Parse("netstandard1.6"))
                    .Libraries
                    .Single(e => e.Name == "projectB");

                var libraryLib = projects[0].AssetsFile
                    .Libraries
                    .Single(e => e.Name == "projectB");

                Assert.Equal("projectB", targetLib.Name);
                Assert.Equal(NuGetFramework.Parse("netstandard1.6"), NuGetFramework.Parse(targetLib.Framework));
                Assert.Equal("1.0.0", targetLib.Version.ToNormalizedString());
                Assert.Equal("project", targetLib.Type);

                Assert.Equal("projectB", libraryLib.Name);
                Assert.Equal("project", libraryLib.Type);
                Assert.Equal("project", libraryLib.MSBuildProject);
                Assert.Equal("project", libraryLib.Path);
                Assert.Equal("1.0.0", libraryLib.Version.ToNormalizedString());
            }
        }

        private static List<SimpleTestProjectContext> CreateProjectsFromSpecs(SimpleTestPathContext pathContext, params PackageSpec[] specs)
        {
            var projects = new List<SimpleTestProjectContext>();

            foreach (var spec in specs)
            {
                var project = new SimpleTestProjectContext(spec.Name, RestoreOutputType.NETCore, pathContext.SolutionRoot);
                spec.FilePath = project.ProjectPath;
                spec.RestoreMetadata.OutputPath = project.OutputPath;

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
            spec.RestoreMetadata.ProjectPath = $"{projectName}.csproj";
            spec.RestoreMetadata.OutputType = RestoreOutputType.NETCore;
            spec.RestoreMetadata.OriginalTargetFrameworks.Add(framework);
            spec.Name = projectName;

            return spec;
        }
    }
}
