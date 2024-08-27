// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Commands.Test;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.FuncTest
{
    // Tests that validate the equivalency of the old/new algorithm.
    // The rule of thumb is everything that's a theory (ie providing new/old algo switch) is a difference, but everything that's a fact, instead calling ValidateRestoreAlgorithmEquivalency is a equivalent
    public partial class RestoreCommandTests
    {
        [Fact]
        // Project 1 -> a 1.0.0 -> b 1.0.0
        //                      -> c 1.0.0 -> b 2.0.0
        public async Task RestoreCommand_WithPackageDrivenDowngrade_RespectsDowngrade_AndRaisesWarning()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();
            var packageA = new SimpleTestPackageContext("a", "1.0.0");
            packageA.Dependencies.Add(new SimpleTestPackageContext("b", "1.0.0"));
            packageA.Dependencies.Add(new SimpleTestPackageContext("c", "1.0.0")
            {
                Dependencies = new() { new SimpleTestPackageContext("b", "2.0.0") }
            });
            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA);

            var project1spec = ProjectTestHelpers.GetPackageSpec("Project1",
                pathContext.SolutionRoot,
                framework: "net472",
                dependencyName: "a");

            // Act & Assert
            (var result, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, project1spec);

            // Additional assert
            result.Success.Should().BeTrue();
            result.LogMessages.Select(e => e.Code).Should().BeEquivalentTo([NuGetLogCode.NU1605]);
            result.LockFile.Targets.Should().HaveCount(1);
            result.LockFile.Targets[0].Libraries.Should().HaveCount(3);
            result.LockFile.Targets[0].Libraries[0].Name.Should().Be("a");
            result.LockFile.Targets[0].Libraries[0].Version.Should().Be(new NuGetVersion("1.0.0"));

            result.LockFile.Targets[0].Libraries[1].Name.Should().Be("b");
            result.LockFile.Targets[0].Libraries[1].Version.Should().Be(new NuGetVersion("1.0.0"));

            result.LockFile.Targets[0].Libraries[2].Name.Should().Be("c");
            result.LockFile.Targets[0].Libraries[2].Version.Should().Be(new NuGetVersion("1.0.0"));
        }

        [Fact]
        // Project 1 -> d 1.0.0 -> b 1.0.0
        // Project 1 -> a 1.0.0 -> b 1.0.0
        //                      -> c 1.0.0 -> b 2.0.0
        public async Task RestoreCommand_WithPackageDrivenDowngradeWithMultipleAncestors_RespectsDowngrade_AndRaisesWarning()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();
            var packageA = new SimpleTestPackageContext("a", "1.0.0");
            packageA.Dependencies.Add(new SimpleTestPackageContext("b", "1.0.0"));
            packageA.Dependencies.Add(new SimpleTestPackageContext("c", "1.0.0")
            {
                Dependencies = new() { new SimpleTestPackageContext("b", "2.0.0") }
            });
            var packageD = new SimpleTestPackageContext("d", "1.0.0")
            {
                Dependencies = [new SimpleTestPackageContext("b", "1.0.0")]
            };

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA,
                packageD);

            var projectSpec = @"
                {
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                                ""d"": ""1.0.0"",
                                ""a"":  ""1.0.0""
                        }
                    }
                  }
                }";

            (var result, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, ProjectTestHelpers.GetPackageSpecWithProjectNameAndSpec("Project1", pathContext.SolutionRoot, projectSpec));

            // Assert
            result.Success.Should().BeTrue();
            result.LogMessages.Select(e => e.Code).Should().BeEquivalentTo([NuGetLogCode.NU1605]);
            result.LockFile.Targets.Should().HaveCount(1);
            result.LockFile.Targets[0].Libraries.Should().HaveCount(4);
            result.LockFile.Targets[0].Libraries[0].Name.Should().Be("a");
            result.LockFile.Targets[0].Libraries[0].Version.Should().Be(new NuGetVersion("1.0.0"));

            result.LockFile.Targets[0].Libraries[1].Name.Should().Be("b");
            result.LockFile.Targets[0].Libraries[1].Version.Should().Be(new NuGetVersion("1.0.0"));

            result.LockFile.Targets[0].Libraries[2].Name.Should().Be("c");
            result.LockFile.Targets[0].Libraries[2].Version.Should().Be(new NuGetVersion("1.0.0"));

            result.LockFile.Targets[0].Libraries[3].Name.Should().Be("d");
            result.LockFile.Targets[0].Libraries[3].Version.Should().Be(new NuGetVersion("1.0.0"));
        }

        [Fact]
        // Project 1 -> A 1.0 -> B 2.0 -> E 1.0
        //           -> C 2.0 -> D 1.0 -> B 3.0
        // Expected: A 1.0, B 3.0, C 2.0, D 1.0
        public async Task RestoreCommand_WithNewPackageEvictedByVersionBump()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();
            var packageA = new SimpleTestPackageContext("a", "1.0.0")
            {
                Dependencies = [new SimpleTestPackageContext("b", "2.0.0") {
                    Dependencies = [new SimpleTestPackageContext("e", "1.0")]
                }]
            };
            var packageC = new SimpleTestPackageContext("c", "2.0.0")
            {
                Dependencies = [new SimpleTestPackageContext("d", "1.0.0")
                {
                    Dependencies = [new SimpleTestPackageContext("b", "3.0.0")
                    {
                        Dependencies = [/*new SimpleTestPackageContext("e", "1.0.0")*/]
                    }]
                }]
            };

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA,
                packageC);

            var projectSpec = @"
                {
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                                ""a"":  ""1.0.0"",
                                ""c"": ""2.0.0""
                        }
                    }
                  }
                }";
            (var result, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, ProjectTestHelpers.GetPackageSpecWithProjectNameAndSpec("Project1", pathContext.SolutionRoot, projectSpec));

            // Assert
            result.Success.Should().BeTrue();
            result.LogMessages.Should().HaveCount(0);

            result.LockFile.Targets.Should().HaveCount(1);
            result.LockFile.Targets[0].Libraries.Should().HaveCount(4);
            result.LockFile.Targets[0].Libraries[0].Name.Should().Be("a");
            result.LockFile.Targets[0].Libraries[0].Version.Should().Be(new NuGetVersion("1.0.0"));

            result.LockFile.Targets[0].Libraries[1].Name.Should().Be("b");
            result.LockFile.Targets[0].Libraries[1].Version.Should().Be(new NuGetVersion("3.0.0"));

            result.LockFile.Targets[0].Libraries[2].Name.Should().Be("c");
            result.LockFile.Targets[0].Libraries[2].Version.Should().Be(new NuGetVersion("2.0.0"));

            result.LockFile.Targets[0].Libraries[3].Name.Should().Be("d");
            result.LockFile.Targets[0].Libraries[3].Version.Should().Be(new NuGetVersion("1.0.0"));
        }

        [Fact]
        public async Task RestoreCommand_WithVersionOverrideAndTransitivePinning_VerifiesEquivalency()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            // Setup packages
            var packageA = new SimpleTestPackageContext("a", "1.0.0");
            packageA.Dependencies.Add(new SimpleTestPackageContext("b", "1.0.0"));
            var packageB150 = new SimpleTestPackageContext("b", "1.5.0");
            var packageB200 = new SimpleTestPackageContext("b", "2.0.0");


            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA,
                packageB150,
                packageB200);

            var project1 = @"
                {
                    ""restore"": {
                                    ""centralPackageVersionsManagementEnabled"": true,
                                    ""CentralPackageTransitivePinningEnabled"": true,
                    },
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                                ""a"": {
                                    ""version"": ""[1.0.0,)"",
                                    ""target"": ""Package"",
                                    ""versionCentrallyManaged"": true
                                }
                        },
                        ""centralPackageVersions"": {
                            ""a"": ""[1.0.0,)"",
                            ""b"": ""[2.0.0,)""
                        }
                    }
                  }
                }";

            var project2 = @"
                {
                    ""restore"": {
                                    ""centralPackageVersionsManagementEnabled"": true,
                                    ""CentralPackageTransitivePinningEnabled"": true,
                    },
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                                ""b"": {
                                    ""version"": ""[1.5.0,)"",
                                    ""target"": ""Package"",
                                    ""versionOverride"": ""[1.5.0, )"",
                                    ""versionCentrallyManaged"": true
                                }
                        },
                        ""centralPackageVersions"": {
                            ""a"": ""[1.0.0,)"",
                            ""b"": ""[2.0.0,)""
                        }
                    }
                  }
                }";

            // Setup project
            var projectSpec = ProjectTestHelpers.GetPackageSpecWithProjectNameAndSpec("Project1", pathContext.SolutionRoot, project1);
            var projectSpec2 = ProjectTestHelpers.GetPackageSpecWithProjectNameAndSpec("Project2", pathContext.SolutionRoot, project2);
            projectSpec = projectSpec.WithTestProjectReference(projectSpec2);

            // Act & Assert
            (var result, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, projectSpec, projectSpec2);
            result.LockFile.Targets.Should().HaveCount(1);
            result.LockFile.Targets[0].Libraries.Should().HaveCount(3);
            result.LockFile.Targets[0].Libraries[0].Name.Should().Be("a");
            result.LockFile.Targets[0].Libraries[0].Version.Should().Be(new NuGetVersion("1.0.0"));
            result.LockFile.Targets[0].Libraries[1].Name.Should().Be("b");
            result.LockFile.Targets[0].Libraries[1].Version.Should().Be(new NuGetVersion("2.0.0"));
            result.LockFile.Targets[0].Libraries[2].Name.Should().Be("Project2");
            result.LockFile.Targets[0].Libraries[2].Version.Should().Be(new NuGetVersion("1.0.0"));

            (var result2, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, projectSpec2);
            result2.LockFile.Targets.Should().HaveCount(1);
            result2.LockFile.Targets[0].Libraries.Should().HaveCount(1);
            result2.LockFile.Targets[0].Libraries[0].Name.Should().Be("b");
            result2.LockFile.Targets[0].Libraries[0].Version.Should().Be(new NuGetVersion("1.5.0"));
        }

        [Fact]
        public async Task RestoreCommand_WithSuppressedProjectReferences_VerifiesEquivalency()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            // Setup packages
            var packageA = new SimpleTestPackageContext("a", "1.0.0");

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageA);

            // Setup project
            var projectSpec = ProjectTestHelpers.GetPackageSpec("Project1", pathContext.SolutionRoot);
            var projectSpec2 = ProjectTestHelpers.GetPackageSpec("Project2", pathContext.SolutionRoot);
            var projectSpec3 = ProjectTestHelpers.GetPackageSpec("Project3", pathContext.SolutionRoot, "net5.0", dependencyName: "a");
            projectSpec = projectSpec.WithTestProjectReference(projectSpec2);
            projectSpec2 = projectSpec2.WithTestProjectReference(projectSpec3, LibraryIncludeFlags.All); // With PrivateAssetsAll

            // Act & Assert
            (var result, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, projectSpec, projectSpec2, projectSpec3);
            result.LockFile.Targets.Should().HaveCount(1);
            result.LockFile.Targets[0].Libraries.Should().HaveCount(1);
            result.LockFile.Targets[0].Libraries[0].Name.Should().Be("Project2");
            result.LockFile.Targets[0].Libraries[0].Version.Should().Be(new NuGetVersion("1.0.0"));

            (var result2, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, projectSpec2, projectSpec3);
            result2.LockFile.Targets.Should().HaveCount(1);
            result2.LockFile.Targets[0].Libraries.Should().HaveCount(2);
            result2.LockFile.Targets[0].Libraries[0].Name.Should().Be("a");
            result2.LockFile.Targets[0].Libraries[0].Version.Should().Be(new NuGetVersion("1.0.0"));
            result2.LockFile.Targets[0].Libraries[1].Name.Should().Be("Project3");
            result2.LockFile.Targets[0].Libraries[1].Version.Should().Be(new NuGetVersion("1.0.0"));

            (var result3, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, projectSpec3);
            result3.LockFile.Targets.Should().HaveCount(1);
            result3.LockFile.Targets[0].Libraries.Should().HaveCount(1);
            result3.LockFile.Targets[0].Libraries[0].Name.Should().Be("a");
            result3.LockFile.Targets[0].Libraries[0].Version.Should().Be(new NuGetVersion("1.0.0"));
        }

        // VersionOverride vs transitive pinnning with project reference
        // PrivateAssets

        internal static async Task<(RestoreResult, RestoreResult)> ValidateRestoreAlgorithmEquivalency(SimpleTestPathContext pathContext, params PackageSpec[] projects)
        {
            var legacyResolverProjects = DuplicateAndEnableLegacyAlgorithm(projects);

            RestoreResult result = await RunRestoreAsync(pathContext, projects);
            RestoreResult legacyResult = await RunRestoreAsync(pathContext, legacyResolverProjects);

            // Assert
            ValidateRestoreResults(result, legacyResult);
            return (result, legacyResult);
        }

        internal static Task<RestoreResult> RunRestoreAsync(SimpleTestPathContext pathContext, params PackageSpec[] projects)
        {
            return new RestoreCommand(ProjectTestHelpers.CreateRestoreRequest(pathContext, new TestLogger(), projects)).ExecuteAsync();
        }

        internal static PackageSpec[] DuplicateAndEnableLegacyAlgorithm(PackageSpec[] projects)
        {
            var result = new PackageSpec[projects.Length];
            for (int i = 0; i < projects.Length; i++)
            {
                var legacyResolverProject = projects[i].Clone();
                legacyResolverProject.RestoreMetadata.UseLegacyDependencyResolver = true;
                result[i] = legacyResolverProject;
            }

            return result;
        }

        internal static void ValidateRestoreResults(RestoreResult newAlgorithmResult, RestoreResult legacyResult)
        {
            var leftPackageSpec = newAlgorithmResult.LockFile.PackageSpec;
            var rightPackageSpec = legacyResult.LockFile.PackageSpec;

            newAlgorithmResult.Success.Should().Be(legacyResult.Success);
            newAlgorithmResult.LockFile.PackageSpec = null;
            legacyResult.LockFile.PackageSpec = null;
            newAlgorithmResult.LockFile.Should().Be(legacyResult.LockFile);

            //Reset package specs
            newAlgorithmResult.LockFile.PackageSpec = leftPackageSpec;
            legacyResult.LockFile.PackageSpec = rightPackageSpec;
        }
    }
}
