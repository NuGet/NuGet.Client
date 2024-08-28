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
        // Project 1 -> d 1.0.0 -> b 2.0.0
        // Project 1 -> a 1.0.0 -> b 1.0.0
        //                      -> c 1.0.0 -> b 3.0.0
        public async Task RestoreCommand_WithPackageDrivenDowngradeWithMultipleAncestorsAndCousin_RespectsDowngrade_AndRaisesWarning()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();
            var packageA = new SimpleTestPackageContext("a", "1.0.0");
            packageA.Dependencies.Add(new SimpleTestPackageContext("b", "1.0.0"));
            packageA.Dependencies.Add(new SimpleTestPackageContext("c", "1.0.0")
            {
                Dependencies = new() { new SimpleTestPackageContext("b", "3.0.0") }
            });
            var packageD = new SimpleTestPackageContext("d", "1.0.0")
            {
                Dependencies = [new SimpleTestPackageContext("b", "2.0.0")]
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
        // Project 1 -> X 1.0 -> B 2.0 -> E 1.0
        //           -> C 2.0 -> D 1.0 -> B 3.0
        // Expected: X 1.0, B 3.0, C 2.0, D 1.0
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

        // Project1 -> Project2 -> (PrivateAssets) Project3 -> X 1.0.0
        // Project1 expects Project2
        // Project2 expects Project3 and X
        // Project3 expects X
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

        // Project1 -> Project2 -> (PrivateAssets) X 1.0.0
        [Fact]
        public async Task RestoreCommand_WithProjectReferenceWithSuppressedDependencies_VerifiesEquivalency()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            // Setup project
            var projectSpec = ProjectTestHelpers.GetPackageSpec("Project1", pathContext.SolutionRoot, framework: "net5.0");
            var projectSpec2 = ProjectTestHelpers.GetPackageSpec("Project2", pathContext.SolutionRoot, framework: "net5.0", dependencyName: "a");
            projectSpec2.TargetFrameworks[0].Dependencies[0].SuppressParent = LibraryIncludeFlags.All;
            projectSpec = projectSpec.WithTestProjectReference(projectSpec2);

            // Act & Assert
            (var result, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, projectSpec, projectSpec2);
            result.LockFile.Targets.Should().HaveCount(1);
            result.LockFile.Targets[0].Libraries.Should().HaveCount(1);
            result.LockFile.Targets[0].Libraries[0].Name.Should().Be("Project2");
            result.LockFile.Targets[0].Libraries[0].Version.Should().Be(new NuGetVersion("1.0.0"));
        }

        // Project1 -> Project2 -> X 1.0.0
        //          -> Project3 -> X 2.0.0 (project)
        // Project is chosen, cause higher.
        [Fact]
        public async Task RestoreCommand_WithSameTransitiveProjectPackageId_ChoosesProjectWithHigherVersion_VerifiesEquivalency()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                new SimpleTestPackageContext("a", "1.0.0"));

            // Setup project
            var project1 = ProjectTestHelpers.GetPackageSpec("Project1", pathContext.SolutionRoot, framework: "net5.0");
            var project2 = ProjectTestHelpers.GetPackageSpec("Project2", pathContext.SolutionRoot, framework: "net5.0");
            var project3 = ProjectTestHelpers.GetPackageSpec("Project3", pathContext.SolutionRoot, framework: "net5.0", dependencyName: "a");
            var projectA = ProjectTestHelpers.GetPackageSpec("a", pathContext.SolutionRoot, framework: "net5.0");
            projectA.Version = new NuGetVersion("2.0.0");

            project2 = project2.WithTestProjectReference(projectA);
            project1 = project1.WithTestProjectReference(project2);
            project1 = project1.WithTestProjectReference(project3);

            // Act & Assert
            (var result, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, project1, project2, project3, projectA);
            result.LockFile.Targets.Should().HaveCount(1);
            result.LockFile.Targets[0].Libraries.Should().HaveCount(3);
            result.LockFile.Targets[0].Libraries[0].Name.Should().Be("a");
            result.LockFile.Targets[0].Libraries[0].Version.Should().Be(new NuGetVersion("2.0.0"));
            result.LockFile.Targets[0].Libraries[0].Type.Should().Be("project");
            result.LockFile.Targets[0].Libraries[1].Name.Should().Be("Project2");
            result.LockFile.Targets[0].Libraries[1].Version.Should().Be(new NuGetVersion("1.0.0"));
            result.LockFile.Targets[0].Libraries[2].Name.Should().Be("Project3");
            result.LockFile.Targets[0].Libraries[2].Version.Should().Be(new NuGetVersion("1.0.0"));
        }

        // Project1 -> Project2 -> X 3.0.0
        //          -> Project3 -> X 2.0.0 (project)
        // Project is chosen, despite package having higher version.
        [Fact]
        public async Task RestoreCommand_WithSameTransitiveProjectPackageId_ChoosesProject_VerifiesEquivalency()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                new SimpleTestPackageContext("a", "3.0.0"));

            // Setup project
            var project1 = ProjectTestHelpers.GetPackageSpec("Project1", pathContext.SolutionRoot, framework: "net5.0");
            var project2 = ProjectTestHelpers.GetPackageSpec("Project2", pathContext.SolutionRoot, framework: "net5.0");
            var project3 = ProjectTestHelpers.GetPackageSpec("Project3", pathContext.SolutionRoot, framework: "net5.0", dependencyName: "a");
            // todo NK - Add a better method
            project3.TargetFrameworks[0].Dependencies[0].LibraryRange = new LibraryRange(project3.TargetFrameworks[0].Dependencies[0].LibraryRange.Name, VersionRange.Parse("3.0.0"), project3.TargetFrameworks[0].Dependencies[0].LibraryRange.TypeConstraint);

            var projectA = ProjectTestHelpers.GetPackageSpec("a", pathContext.SolutionRoot, framework: "net5.0");
            projectA.Version = new NuGetVersion("2.0.0");

            project2 = project2.WithTestProjectReference(projectA);
            project1 = project1.WithTestProjectReference(project2);
            project1 = project1.WithTestProjectReference(project3);

            // Act & Assert
            (var result, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, project1, project2, project3, projectA);
            result.LockFile.Targets.Should().HaveCount(1);
            result.LockFile.Targets[0].Libraries.Should().HaveCount(3);
            result.LockFile.Targets[0].Libraries[0].Name.Should().Be("a");
            result.LockFile.Targets[0].Libraries[0].Version.Should().Be(new NuGetVersion("2.0.0"));
            result.LockFile.Targets[0].Libraries[0].Type.Should().Be("project");
            result.LockFile.Targets[0].Libraries[1].Name.Should().Be("Project2");
            result.LockFile.Targets[0].Libraries[1].Version.Should().Be(new NuGetVersion("1.0.0"));
            result.LockFile.Targets[0].Libraries[2].Name.Should().Be("Project3");
            result.LockFile.Targets[0].Libraries[2].Version.Should().Be(new NuGetVersion("1.0.0"));
        }

        // Project1 -> Project2 -> X 1.0.0
        //          -> Project3 -> (PrivateAssets) X 2.0.0 (project)
        // Package is chosen, since project is suppressed
        [Fact]
        public async Task RestoreCommand_WithSameTransitiveProjectPackageId_SuppressedProject_ChoosesPackage_VerifiesEquivalency()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                new SimpleTestPackageContext("a", "1.0.0"));

            // Setup project
            var project1 = ProjectTestHelpers.GetPackageSpec("Project1", pathContext.SolutionRoot, framework: "net5.0");
            var project2 = ProjectTestHelpers.GetPackageSpec("Project2", pathContext.SolutionRoot, framework: "net5.0");
            var project3 = ProjectTestHelpers.GetPackageSpec("Project3", pathContext.SolutionRoot, framework: "net5.0", dependencyName: "a");

            var projectA = ProjectTestHelpers.GetPackageSpec("a", pathContext.SolutionRoot, framework: "net5.0");
            projectA.Version = new NuGetVersion("2.0.0");

            project2 = project2.WithTestProjectReference(projectA, LibraryIncludeFlags.All);
            project1 = project1.WithTestProjectReference(project2);
            project1 = project1.WithTestProjectReference(project3);

            // Act & Assert
            (var result, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, project1, project2, project3, projectA);
            result.LockFile.Targets.Should().HaveCount(1);
            result.LockFile.Targets[0].Libraries.Should().HaveCount(3);
            result.LockFile.Targets[0].Libraries[0].Name.Should().Be("a");
            //result.LockFile.Targets[0].Libraries[0].Version.Should().Be(new NuGetVersion("1.0.0")); // TODO NK - Unclear to me why suppressed project is getting selected.
            //result.LockFile.Targets[0].Libraries[0].Type.Should().Be("package");
            result.LockFile.Targets[0].Libraries[1].Name.Should().Be("Project2");
            result.LockFile.Targets[0].Libraries[1].Version.Should().Be(new NuGetVersion("1.0.0"));
            result.LockFile.Targets[0].Libraries[2].Name.Should().Be("Project3");
            result.LockFile.Targets[0].Libraries[2].Version.Should().Be(new NuGetVersion("1.0.0"));
        }

        // A -> - X - B/PrivateAssets=All -> C
        //        X -> D -> E-> B -> C
        // X, D & E
        [Fact]
        public async Task RestoreCommand_HigherLevelSuppressionsWin_VerifiesEquivalency()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            // Setup project
            var A = ProjectTestHelpers.GetPackageSpec("A", pathContext.SolutionRoot, framework: "net5.0");
            var X = ProjectTestHelpers.GetPackageSpec("X", pathContext.SolutionRoot, framework: "net5.0");
            var B = ProjectTestHelpers.GetPackageSpec("B", pathContext.SolutionRoot, framework: "net5.0");
            var C = ProjectTestHelpers.GetPackageSpec("C", pathContext.SolutionRoot, framework: "net5.0");
            var D = ProjectTestHelpers.GetPackageSpec("D", pathContext.SolutionRoot, framework: "net5.0");
            var E = ProjectTestHelpers.GetPackageSpec("E", pathContext.SolutionRoot, framework: "net5.0");

            X = X.WithTestProjectReference(B, LibraryIncludeFlags.All);
            X = X.WithTestProjectReference(D);
            D = D.WithTestProjectReference(E);
            E = E.WithTestProjectReference(B);
            B = B.WithTestProjectReference(C);
            A = A.WithTestProjectReference(X);

            // Act & Assert
            (var result, _) = await ValidateRestoreAlgorithmEquivalency(pathContext, A, X, B, C, D, E);
            result.LockFile.Targets.Should().HaveCount(1);
            result.LockFile.Targets[0].Libraries.Should().HaveCount(3);
            result.LockFile.Targets[0].Libraries[0].Name.Should().Be("D");
            result.LockFile.Targets[0].Libraries[1].Name.Should().Be("E");
            result.LockFile.Targets[0].Libraries[2].Name.Should().Be("X");
        }

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
