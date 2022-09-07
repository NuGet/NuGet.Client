// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using NuGet.Commands.Test;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    /// <summary>
    /// Factory helpers to create CPS and Legacy PackageReference test projects
    /// </summary>
    internal static class ProjectFactories
    {
        internal static CpsPackageReferenceProject CreateCpsPackageReferenceProject(string projectName, string projectFullPath, ProjectSystemCache projectSystemCache)
        {
            var projectServices = new TestProjectSystemServices();

            return new CpsPackageReferenceProject(
                    projectName: projectName,
                    projectUniqueName: projectName,
                    projectFullPath: projectFullPath,
                    projectSystemCache: projectSystemCache,
                    unconfiguredProject: null,
                    projectServices: projectServices,
                    projectId: projectName);
        }

        internal static LegacyPackageReferenceProject CreateLegacyPackageReferenceProject(TestDirectory testDirectory, string projectId, IVsProjectThreadingService threadingService, LibraryDependency[] pkgDependencies)
        {
            var framework = NuGetFramework.Parse("netstandard13");
            IVsProjectAdapter projectAdapter = CreateProjectAdapter(testDirectory);

            var projectServices = new TestProjectSystemServices();
            projectServices.SetupInstalledPackages(
                framework,
                pkgDependencies);

            var testProject = new LegacyPackageReferenceProject(
                projectAdapter,
                projectId,
                projectServices,
                threadingService);

            return testProject;
        }

        internal static LegacyPackageReferenceProject CreateLegacyPackageReferenceProject(TestDirectory testDirectory, string projectId, string range, IVsProjectThreadingService threadingService)
        {
            var onedep = new[]
            {
                new LibraryDependency
                {
                    LibraryRange = new LibraryRange(
                        "packageA",
                        VersionRange.Parse(range),
                        LibraryDependencyTarget.Package)
                }
            };

            return CreateLegacyPackageReferenceProject(testDirectory: testDirectory, projectId: projectId, threadingService: threadingService, pkgDependencies: onedep);
        }

        internal static IVsProjectAdapter CreateProjectAdapter(string fullPath)
        {
            var projectBuildProperties = new Mock<IProjectBuildProperties>();
            return CreateProjectAdapter(fullPath, projectBuildProperties);
        }

        internal static IVsProjectAdapter CreateProjectAdapter(string fullPath, Mock<IProjectBuildProperties> projectBuildProperties)
        {
            var projectAdapter = CreateProjectAdapter(projectBuildProperties);

            projectAdapter
                .Setup(x => x.FullProjectPath)
                .Returns(Path.Combine(fullPath, "foo.csproj"));
            projectAdapter
                .Setup(x => x.GetTargetFrameworkAsync())
                .ReturnsAsync(NuGetFramework.Parse("netstandard13"));

            var testMSBuildProjectExtensionsPath = Path.Combine(fullPath, "obj");
            Directory.CreateDirectory(testMSBuildProjectExtensionsPath);
            projectAdapter
                .Setup(x => x.GetMSBuildProjectExtensionsPathAsync())
                .Returns(Task.FromResult(testMSBuildProjectExtensionsPath));

            return projectAdapter.Object;
        }

        internal static Mock<IVsProjectAdapter> CreateProjectAdapter(Mock<IProjectBuildProperties> projectBuildProperties)
        {
            var projectAdapter = new Mock<IVsProjectAdapter>();

            projectAdapter
                .SetupGet(x => x.ProjectName)
                .Returns("TestProject");

            projectAdapter
                .Setup(x => x.Version)
                .Returns("1.0.0");

            projectAdapter
                .Setup(x => x.BuildProperties)
                .Returns(projectBuildProperties.Object);

            return projectAdapter;
        }

        internal static ProjectNames GetTestProjectNames(string projectPath, string projectUniqueName)
        {
            var projectNames = new ProjectNames(
            fullName: projectPath,
            uniqueName: projectUniqueName,
            shortName: projectUniqueName,
            customUniqueName: projectUniqueName,
            projectId: Guid.NewGuid().ToString());
            return projectNames;
        }

        internal static PackageSpec GetPackageSpec(string projectName, string packageSpecFullPath, string version)
        {
            string referenceSpec = $@"
                {{
                    ""frameworks"":
                    {{
                        ""net5.0"":
                        {{
                            ""dependencies"":
                            {{
                                ""packageA"":
                                {{
                                    ""version"": ""{version}"",
                                    ""target"": ""Package""
                                }},
                            }}
                        }}
                    }}
                }}";
            return JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, packageSpecFullPath).WithTestRestoreMetadata();
        }
    }
}
