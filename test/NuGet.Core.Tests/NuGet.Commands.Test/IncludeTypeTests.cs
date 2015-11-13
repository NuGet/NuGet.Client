using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class IncludeTypeTests : IDisposable
    {
        [Fact]
        public async Task IncludeType_ProjectToProjectDeeperDependencyExcluded()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0"",
                        ""suppressParent"": ""runtime,compile""
                    },
                    ""packageY"": {
                        ""version"": ""1.0.0"",
                        ""exclude"": ""runtime,compile"",
                        ""suppressParent"": ""runtime,compile""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            CreateXYZ(Path.Combine(workingDir, "repository"), "all", string.Empty);

            // Act
            var result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);
            result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(0, GetNonEmptyCount(targets["packageY"].CompileTimeAssemblies));
            Assert.Equal(0, GetNonEmptyCount(targets["packageY"].RuntimeAssemblies));
        }

        [Fact]
        public async Task IncludeType_ProjectToProjectDeeperDependencyOverrides()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0""
                    },
                    ""packageY"": {
                        ""version"": ""1.0.0"",
                        ""exclude"": ""runtime,compile""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            CreateXYZ(Path.Combine(workingDir, "repository"), "all", string.Empty);

            // Act
            var result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);
            result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].CompileTimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].RuntimeAssemblies));
        }

        [Fact]
        public async Task IncludeType_ProjectToProjectDefaultFlowDoubleRestore()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            CreateXYZ(Path.Combine(workingDir, "repository"), "all", string.Empty);

            // Act
            var result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);
            result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(0, GetNonEmptyCount(targets["packageX"].ContentFiles));
            Assert.Equal(1, GetNonEmptyCount(targets["packageX"].NativeLibraries));
            Assert.Equal(1, GetNonEmptyCount(targets["packageX"].RuntimeAssemblies));
            Assert.Equal(1, targets["packageX"].FrameworkAssemblies.Count);
            Assert.Equal(1, targets["packageX"].Dependencies.Count);

            Assert.Equal(0, GetNonEmptyCount(targets["packageY"].ContentFiles));
            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].NativeLibraries));
            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].RuntimeAssemblies));
            Assert.Equal(1, targets["packageY"].FrameworkAssemblies.Count);
            Assert.Equal(1, targets["packageY"].Dependencies.Count);

            Assert.Equal(0, GetNonEmptyCount(targets["packageZ"].ContentFiles));
            Assert.Equal(1, GetNonEmptyCount(targets["packageZ"].NativeLibraries));
            Assert.Equal(1, GetNonEmptyCount(targets["packageZ"].RuntimeAssemblies));
            Assert.Equal(1, targets["packageZ"].FrameworkAssemblies.Count);
            Assert.Equal(0, targets["packageZ"].Dependencies.Count);

            Assert.Equal(0, msbuildTargets["TestProject1"].Count);
        }

        [Fact]
        public async Task IncludeType_ProjectToProjectFlowAllDoubleRestore()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0"",
                        ""suppressParent"": ""none""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            CreateXYZ(Path.Combine(workingDir, "repository"), "all", string.Empty);

            // Act
            var result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);
            result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(2, GetNonEmptyCount(targets["packageX"].ContentFiles));
            Assert.Equal(1, GetNonEmptyCount(targets["packageX"].NativeLibraries));
            Assert.Equal(1, GetNonEmptyCount(targets["packageX"].RuntimeAssemblies));
            Assert.Equal(1, targets["packageX"].FrameworkAssemblies.Count);
            Assert.Equal(1, targets["packageX"].Dependencies.Count);

            Assert.Equal(2, GetNonEmptyCount(targets["packageY"].ContentFiles));
            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].NativeLibraries));
            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].RuntimeAssemblies));
            Assert.Equal(1, targets["packageY"].FrameworkAssemblies.Count);
            Assert.Equal(1, targets["packageY"].Dependencies.Count);

            Assert.Equal(2, GetNonEmptyCount(targets["packageZ"].ContentFiles));
            Assert.Equal(1, GetNonEmptyCount(targets["packageZ"].NativeLibraries));
            Assert.Equal(1, GetNonEmptyCount(targets["packageZ"].RuntimeAssemblies));
            Assert.Equal(1, targets["packageZ"].FrameworkAssemblies.Count);
            Assert.Equal(0, targets["packageZ"].Dependencies.Count);

            Assert.Equal(3, msbuildTargets["TestProject1"].Count);
        }

        [Fact]
        public async Task IncludeType_ProjectToProjectFlowAll()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0"",
                        ""suppressParent"": ""none""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            CreateXYZ(Path.Combine(workingDir, "repository"), "all", string.Empty);

            // Act
            var result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(2, GetNonEmptyCount(targets["packageX"].ContentFiles));
            Assert.Equal(1, GetNonEmptyCount(targets["packageX"].NativeLibraries));
            Assert.Equal(1, GetNonEmptyCount(targets["packageX"].RuntimeAssemblies));
            Assert.Equal(1, targets["packageX"].FrameworkAssemblies.Count);
            Assert.Equal(1, targets["packageX"].Dependencies.Count);

            Assert.Equal(2, GetNonEmptyCount(targets["packageY"].ContentFiles));
            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].NativeLibraries));
            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].RuntimeAssemblies));
            Assert.Equal(1, targets["packageY"].FrameworkAssemblies.Count);
            Assert.Equal(1, targets["packageY"].Dependencies.Count);

            Assert.Equal(2, GetNonEmptyCount(targets["packageZ"].ContentFiles));
            Assert.Equal(1, GetNonEmptyCount(targets["packageZ"].NativeLibraries));
            Assert.Equal(1, GetNonEmptyCount(targets["packageZ"].RuntimeAssemblies));
            Assert.Equal(1, targets["packageZ"].FrameworkAssemblies.Count);
            Assert.Equal(0, targets["packageZ"].Dependencies.Count);

            Assert.Equal(3, msbuildTargets["TestProject1"].Count);
        }

        [Fact]
        public async Task IncludeType_ProjectToProjectsIntersectExcludes()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson3 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0"",
                        ""suppressParent"": ""compile,runtime""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0"",
                        ""suppressParent"": ""native,runtime""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            CreateXYZ(Path.Combine(workingDir, "repository"));

            // Act
            var result = await TriangleProjectSetup(workingDir, logger, configJson1, configJson2, configJson3);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(3, target.Libraries.Count);


            Assert.Equal(0, GetNonEmptyCount(targets["packageY"].RuntimeAssemblies));
            Assert.Equal(0, GetNonEmptyCount(targets["packageZ"].RuntimeAssemblies));
            Assert.Equal(0, GetNonEmptyCount(targets["packageX"].RuntimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].CompileTimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageZ"].CompileTimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageX"].CompileTimeAssemblies));
        }

        [Fact]
        public async Task IncludeType_ExludeDependency()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var projectJson = @"{
                        ""dependencies"": {
                            ""packageA"": {
                                ""version"": ""1.0.0""
                            },
                            ""packageB"": {
                                ""version"": ""1.0.0"",
                                ""exclude"": ""all""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                    }";

            CreateAToB(Path.Combine(workingDir, "repository"));

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var a = targets["packageA"];
            var b = targets["packageB"];

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(1, GetNonEmptyCount(a.RuntimeAssemblies));
            Assert.Equal(0, GetNonEmptyCount(b.RuntimeAssemblies));
        }

        [Fact]
        public async Task IncludeType_CircularDependencyIsHandledInFlatten()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var projectJson = @"{
                        ""dependencies"": {
                            ""packageX"": ""1.0.0""
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                 }";

            var x = new TestPackage()
            {
                Id = "packageX",
                Version = "1.0.0",
                Include = "runtime"
            };

            var z = new TestPackage()
            {
                Id = "packageZ",
                Version = "1.0.0",
                Include = "runtime"
            };
            z.Dependencies.Add(x);

            var y = new TestPackage()
            {
                Id = "packageY",
                Version = "1.0.0",
                Include = "runtime"
            };
            y.Dependencies.Add(z);

            x.Dependencies.Add(y);

            var packages = new List<TestPackage>()
            {
                x
            };

            CreatePackages(packages, Path.Combine(workingDir, "repository"));

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(1, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].RuntimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageZ"].RuntimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageX"].RuntimeAssemblies));
            Assert.Equal(0, GetNonEmptyCount(targets["packageY"].CompileTimeAssemblies));
            Assert.Equal(0, GetNonEmptyCount(targets["packageZ"].CompileTimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageX"].CompileTimeAssemblies));
        }

        [Fact]
        public async Task IncludeType_UnifyDependencyEdges()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var projectJson = @"{
                        ""dependencies"": {
                            ""packageA"": ""1.0.0"",
                            ""packageB"": ""2.0.0""
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                 }";

            var z1 = new TestPackage()
            {
                Id = "packageZ",
                Version = "1.0.0",
                Include = "runtime"
            };

            var y1 = new TestPackage()
            {
                Id = "packageY",
                Version = "1.0.0",
                Include = "runtime"
            };
            y1.Dependencies.Add(z1);

            var z2 = new TestPackage()
            {
                Id = "packageZ",
                Version = "2.0.0",
                Include = "compile"
            };

            var y2 = new TestPackage()
            {
                Id = "packageY",
                Version = "2.0.0",
                Include = "compile"
            };
            y2.Dependencies.Add(z2);

            var a = new TestPackage()
            {
                Id = "packageA",
                Version = "1.0.0"
            };
            a.Dependencies.Add(y1);

            var b = new TestPackage()
            {
                Id = "packageB",
                Version = "2.0.0"
            };
            b.Dependencies.Add(y2);

            var packages = new List<TestPackage>()
            {
                a,
                b
            };

            CreatePackages(packages, Path.Combine(workingDir, "repository"));

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].RuntimeAssemblies));
            Assert.Equal(0, GetNonEmptyCount(targets["packageZ"].RuntimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].CompileTimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageZ"].CompileTimeAssemblies));
        }

        [Fact]
        public async Task IncludeType_ProjectToProjectWithBuildOverrideToExclude()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": ""1.0.0""
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0"",
                        ""exclude"": ""build""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            CreateXYZ(Path.Combine(workingDir, "repository"));

            // Act
            var result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(3, target.Libraries.Count);
            Assert.Equal(3, result.LockFile.Libraries.Count);
            Assert.Equal(0, msbuildTargets["TestProject1"].Count);
        }

        [Fact]
        public async Task IncludeType_ProjectToProjectWithBuildOverride()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                    ""packageX"": ""1.0.0""
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            CreateXYZ(Path.Combine(workingDir, "repository"));

            // Act
            var result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(3, target.Libraries.Count);
            Assert.Equal(3, result.LockFile.Libraries.Count);
            Assert.Equal(3, msbuildTargets["TestProject1"].Count);
        }

        [Fact]
        public async Task IncludeType_ProjectToProjectNoTransitiveBuild()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            CreateXYZ(Path.Combine(workingDir, "repository"));

            // Act
            var result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(3, target.Libraries.Count);
            Assert.Equal(3, result.LockFile.Libraries.Count);
            Assert.Equal(0, msbuildTargets["TestProject1"].Count);
        }

        [Fact]
        public async Task IncludeType_ProjectToProjectNoTransitiveContent()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            CreateXYZ(Path.Combine(workingDir, "repository"));

            // Act
            var result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(3, target.Libraries.Count);
            Assert.Equal(3, result.LockFile.Libraries.Count);
            Assert.True(target.Libraries.All(lib => IsEmptyFolder(lib.ContentFiles)));
        }

        [Fact]
        public async Task IncludeType_ExcludedAndTransitivePackage()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0"",
                        ""suppressParent"": ""all""
                    },
                    ""packageZ"": ""1.0.0""
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            CreateXYZ(Path.Combine(workingDir, "repository"));

            // Act
            var result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(1, target.Libraries.Count);
            Assert.Equal(1, result.LockFile.Libraries.Count);
            Assert.Equal("packageZ", target.Libraries.Single().Name);
            Assert.Equal(1, target.Libraries.Single().CompileTimeAssemblies.Count);
        }

        [Fact]
        public async Task IncludeType_ProjectToProjectReferenceWithBuildReferenceAndTopLevel()
        {
            // Restore Project1
            // Project2 has only build dependencies
            // Project1 -> Project2 -(suppress: all)-> packageX -> packageY -> packageB

            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0"",
                        ""suppressParent"": ""all""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                    ""packageZ"": ""1.0.0""
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            CreateXYZ(Path.Combine(workingDir, "repository"));

            // Act
            var result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(1, target.Libraries.Count);
            Assert.Equal(1, result.LockFile.Libraries.Count);
            Assert.Equal("packageZ", target.Libraries.Single().Name);
            Assert.Equal(1, target.Libraries.Single().CompileTimeAssemblies.Count);
        }

        [Fact]
        public async Task IncludeType_ProjectToProjectReferenceWithBuildDependency()
        {
            // Restore Project1
            // Project2 has only build dependencies
            // Project1 -> Project2 -(suppress: all)-> packageX -> packageY -> packageB

            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var configJson2 = @"{
                ""dependencies"": {
                    ""packageX"": {
                        ""version"": ""1.0.0"",
                        ""suppressParent"": ""all""
                    }
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var configJson1 = @"{
                ""dependencies"": {
                },
                ""frameworks"": {
                ""net46"": {}
                }
            }";

            var result = await ProjectToProjectSetup(workingDir, logger, configJson1, configJson2);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(0, target.Libraries.Count);
            Assert.Equal(0, result.LockFile.Libraries.Count);
        }

        [Fact]
        public async Task IncludeType_ProjectIncludesOnlyCompile()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var projectJson = @"{
                        ""dependencies"": {
                            ""packageX"": {
                                ""version"": ""1.0.0"",
                                ""include"": ""compile""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                 }";

            CreateXYZ(Path.Combine(workingDir, "repository"));

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(0, GetNonEmptyCount(targets["packageX"].RuntimeAssemblies));
            Assert.Equal(0, GetNonEmptyCount(targets["packageY"].RuntimeAssemblies));
            Assert.Equal(0, GetNonEmptyCount(targets["packageZ"].RuntimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageX"].CompileTimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].CompileTimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageZ"].CompileTimeAssemblies));
        }

        [Fact]
        public async Task IncludeType_ProjectOverrideNuspecExclude()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var projectJson = @"{
                        ""dependencies"": {
                            ""packageX"": {
                                ""version"": ""1.0.0""
                            },
                            ""packageY"": ""1.0.0"",
                            ""packageZ"": ""1.0.0""
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                 }";

            CreateXYZ(Path.Combine(workingDir, "repository"), string.Empty, "build");

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(3, msbuildTargets["TestProject"].Count);
        }

        [Fact]
        public async Task IncludeType_ProjectOverrideNuspecExclude_UnderFramework()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var projectJson = @"{
                        ""frameworks"": {
                            ""net46"": {
                                ""dependencies"": {
                                    ""packageX"": ""1.0.0"",
                                    ""packageY"": ""1.0.0"",
                                    ""packageZ"": ""1.0.0""
                                }
                            }
                        }
                 }";

            CreateXYZ(Path.Combine(workingDir, "repository"), string.Empty, "build");

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(3, msbuildTargets["TestProject"].Count);
        }

        [Fact]
        public async Task IncludeType_ProjectExcludesBuildFromAll()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var projectJson = @"{
                        ""dependencies"": {
                            ""packageX"": {
                                ""version"": ""1.0.0"",
                                ""exclude"": ""build""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                 }";

            CreateXYZ(Path.Combine(workingDir, "repository"));

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(0, msbuildTargets["TestProject"].Count);
        }

        [Fact]
        public async Task IncludeType_NuspecExcludesBuildFromTransitiveDependencies()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var projectJson = @"{
                        ""dependencies"": {
                            ""packageX"": {
                                ""version"": ""1.0.0""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                 }";

            CreateXYZ(Path.Combine(workingDir, "repository"), string.Empty, "build");

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(1, msbuildTargets["TestProject"].Count);
            Assert.Equal("packageX", msbuildTargets["TestProject"].Single());
        }

        [Fact]
        public async Task IncludeType_NuspecFlowsOnlyRuntimeTransitiveDependencies()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var projectJson = @"{
                        ""dependencies"": {
                            ""packageX"": {
                                ""version"": ""1.0.0""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                 }";

            CreateXYZ(Path.Combine(workingDir, "repository"), "runtime", string.Empty);

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(1, GetNonEmptyCount(targets["packageX"].RuntimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageY"].RuntimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageZ"].RuntimeAssemblies));
            Assert.Equal(1, GetNonEmptyCount(targets["packageX"].CompileTimeAssemblies));
            Assert.Equal(0, GetNonEmptyCount(targets["packageY"].CompileTimeAssemblies));
            Assert.Equal(0, GetNonEmptyCount(targets["packageZ"].CompileTimeAssemblies));
        }

        [Fact]
        public async Task IncludeType_NuspecFlowsContentFromTransitiveDependencies()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var projectJson = @"{
                        ""dependencies"": {
                            ""packageX"": {
                                ""version"": ""1.0.0""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                 }";

            CreateXYZ(Path.Combine(workingDir, "repository"), "contentFiles", string.Empty);

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(2, GetNonEmptyCount(targets["packageX"].ContentFiles));
            Assert.Equal(2, GetNonEmptyCount(targets["packageY"].ContentFiles));
            Assert.Equal(2, GetNonEmptyCount(targets["packageZ"].ContentFiles));
        }

        [Fact]
        public async Task IncludeType_ProjectOverridesNoContentFromTransitiveDependencies()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var projectJson = @"{
                        ""dependencies"": {
                            ""packageA"": {
                                ""version"": ""1.0.0""
                            },
                            ""packageB"": {
                                ""version"": ""1.0.0""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                    }";

            CreateAToB(Path.Combine(workingDir, "repository"));

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var a = targets["packageA"];
            var b = targets["packageB"];

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(2, GetNonEmptyCount(a.ContentFiles));
            Assert.Equal(2, GetNonEmptyCount(b.ContentFiles));
        }

        [Fact]
        public async Task IncludeType_NoContentFromTransitiveDependencies()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            var projectJson = @"{
                        ""dependencies"": {
                            ""packageA"": {
                                ""version"": ""1.0.0""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                    }";

            CreateAToB(Path.Combine(workingDir, "repository"));

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var a = targets["packageA"];
            var b = targets["packageB"];

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(2, GetNonEmptyCount(a.ContentFiles));
            Assert.Equal(0, GetNonEmptyCount(b.ContentFiles));

            Assert.Equal(3, b.ContentFiles.Single().Properties.Count);
            Assert.Equal("None", b.ContentFiles.Single().Properties["buildAction"]);
            Assert.Equal("False", b.ContentFiles.Single().Properties["copyToOutput"]);
            Assert.Equal("any", b.ContentFiles.Single().Properties["codeLanguage"]);
        }

        [Theory]
        [InlineData(@"{
                        ""dependencies"": {
                            ""packageA"": {
                                ""version"": ""1.0.0"",
                                ""suppressParent"": ""all""
                            },
                            ""packageB"": {
                                ""version"": ""1.0.0"",
                                ""suppressParent"": ""all"",
                                ""exclude"": ""contentFiles""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                    }")]
        [InlineData(@"{
                        ""dependencies"": {
                            ""packageA"": {
                                ""version"": ""1.0.0""
                            },
                            ""packageB"": {
                                ""version"": ""1.0.0"",
                                ""exclude"": ""contentFiles""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                    }")]
        [InlineData(@"{
                        ""dependencies"": {
                            ""packageA"": {
                                ""version"": ""1.0.0""
                            },
                            ""packageB"": {
                                ""version"": ""1.0.0"",
                                ""include"": ""build,compile,runtime,native""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                    }")]
        [InlineData(@"{
                        ""dependencies"": {
                            ""packageA"": {
                                ""version"": ""1.0.0"",
                                ""include"": ""build,compile,runtime,native,contentfiles""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                    }")]
        [InlineData(@"{
                        ""dependencies"": {
                            ""packageA"": {
                                ""version"": ""1.0.0"",
                                ""include"": ""all"",
                                ""exclude"": ""none""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                    }")]
        [InlineData(@"{
                        ""dependencies"": {
                            ""packageA"": {
                                ""version"": ""1.0.0"",
                                ""exclude"": ""none""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                    }")]
        [InlineData(@"{
                        ""dependencies"": {
                            ""packageA"": {
                                ""version"": ""1.0.0"",
                                ""include"": ""all""
                            }
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                    }")]
        [InlineData(@"{
                        ""dependencies"": {
                            ""packageA"": ""1.0.0""
                        },
                        ""frameworks"": {
                            ""net46"": {}
                        }
                    }")]
        public async Task IncludeType_SingleProjectEquivalentToTheDefault(string projectJson)
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();

            CreateAToB(Path.Combine(workingDir, "repository"));

            // Act
            var result = await StandardSetup(workingDir, logger, projectJson);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var targets = target.Libraries.ToDictionary(lib => lib.Name);

            var a = targets["packageA"];
            var b = targets["packageB"];

            var msbuildTargets = GetInstalledTargets(workingDir);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);

            Assert.Equal(2, GetNonEmptyCount(a.ContentFiles));
            Assert.Equal(1, GetNonEmptyCount(a.NativeLibraries));
            Assert.Equal(1, GetNonEmptyCount(a.RuntimeAssemblies));
            Assert.Equal(1, a.FrameworkAssemblies.Count);
            Assert.Equal(1, a.Dependencies.Count);

            Assert.Equal(0, GetNonEmptyCount(b.ContentFiles));
            Assert.Equal(1, GetNonEmptyCount(b.NativeLibraries));
            Assert.Equal(1, GetNonEmptyCount(b.RuntimeAssemblies));
            Assert.Equal(1, b.FrameworkAssemblies.Count);
            Assert.Equal(0, b.Dependencies.Count);

            Assert.Equal(2, msbuildTargets["TestProject"].Count);
        }

        private async Task<RestoreResult> ProjectToProjectSetup(
            string workingDir,
            NuGet.Logging.ILogger logger,
            string configJson1,
            string configJson2)
        {
            // Arrange
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);
            var testProject1Dir = Path.Combine(projectDir, "TestProject1");
            Directory.CreateDirectory(testProject1Dir);
            var testProject2Dir = Path.Combine(projectDir, "TestProject2");
            Directory.CreateDirectory(testProject2Dir);

            var specPath1 = Path.Combine(testProject1Dir, "project.json");
            var spec1 = JsonPackageSpecReader.GetPackageSpec(configJson1, "TestProject1", specPath1);
            using (var writer = new StreamWriter(File.OpenWrite(specPath1)))
            {
                writer.WriteLine(configJson1.ToString());
            }

            var specPath2 = Path.Combine(testProject2Dir, "project.json");
            var spec2 = JsonPackageSpecReader.GetPackageSpec(configJson2, "TestProject2", specPath2);
            using (var writer = new StreamWriter(File.OpenWrite(specPath2)))
            {
                writer.WriteLine(configJson2.ToString());
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var request = new RestoreRequest(spec1, sources, packagesDir);
            request.LockFilePath = Path.Combine(testProject1Dir, "project.lock.json");
            request.ExternalProjects = new List<ExternalProjectReference>()
            {
                new ExternalProjectReference("TestProject2", specPath2, Enumerable.Empty<string>())
            };

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            return result;
        }

        private async Task<RestoreResult> TriangleProjectSetup(
            string workingDir,
            NuGet.Logging.ILogger logger,
            string configJson1,
            string configJson2,
            string configJson3)
        {
            // Arrange
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);
            var testProject1Dir = Path.Combine(projectDir, "TestProject1");
            Directory.CreateDirectory(testProject1Dir);
            var testProject2Dir = Path.Combine(projectDir, "TestProject2");
            Directory.CreateDirectory(testProject2Dir);
            var testProject3Dir = Path.Combine(projectDir, "TestProject3");
            Directory.CreateDirectory(testProject3Dir);

            var specPath1 = Path.Combine(testProject1Dir, "project.json");
            var spec1 = JsonPackageSpecReader.GetPackageSpec(configJson1, "TestProject1", specPath1);
            using (var writer = new StreamWriter(File.OpenWrite(specPath1)))
            {
                writer.WriteLine(configJson1);
            }

            var specPath2 = Path.Combine(testProject2Dir, "project.json");
            var spec2 = JsonPackageSpecReader.GetPackageSpec(configJson2, "TestProject2", specPath2);
            using (var writer = new StreamWriter(File.OpenWrite(specPath2)))
            {
                writer.WriteLine(configJson2);
            }

            var specPath3 = Path.Combine(testProject3Dir, "project.json");
            var spec3 = JsonPackageSpecReader.GetPackageSpec(configJson3, "TestProject3", specPath3);
            using (var writer = new StreamWriter(File.OpenWrite(specPath3)))
            {
                writer.WriteLine(configJson3);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var request = new RestoreRequest(spec1, sources, packagesDir);
            request.LockFilePath = Path.Combine(testProject1Dir, "project.lock.json");
            request.ExternalProjects = new List<ExternalProjectReference>()
            {
                new ExternalProjectReference("TestProject2", specPath2, Enumerable.Empty<string>()),
                new ExternalProjectReference("TestProject3", specPath3, Enumerable.Empty<string>())
            };

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            return result;
        }

        private async Task<RestoreResult> StandardSetup(
            string workingDir,
            NuGet.Logging.ILogger logger,
            string configJson)
        {
            // Arrange
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);
            var testProjectDir = Path.Combine(projectDir, "TestProject");
            Directory.CreateDirectory(testProjectDir);

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var specPath = Path.Combine(testProjectDir, "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson, "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);

            request.LockFilePath = Path.Combine(testProjectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            return result;
        }

        private static void CreateAToB(string repositoryDir)
        {
            var b = new TestPackage()
            {
                Id = "packageB",
                Version = "1.0.0"
            };

            var a = new TestPackage()
            {
                Id = "packageA",
                Version = "1.0.0"
            };
            a.Dependencies.Add(b);

            var packages = new List<TestPackage>()
            {
                a
            };

            CreatePackages(packages, repositoryDir);
        }

        private static void CreateXYZ(string repositoryDir)
        {
            CreateXYZ(repositoryDir, string.Empty, string.Empty);
        }

        private static void CreateXYZ(string repositoryDir, string include, string exclude)
        {
            var z = new TestPackage()
            {
                Id = "packageZ",
                Version = "1.0.0",
                Include = include,
                Exclude = exclude
            };

            var y = new TestPackage()
            {
                Id = "packageY",
                Version = "1.0.0",
                Include = include,
                Exclude = exclude
            };
            y.Dependencies.Add(z);

            var x = new TestPackage()
            {
                Id = "packageX",
                Version = "1.0.0",
                Include = include,
                Exclude = exclude
            };
            x.Dependencies.Add(y);

            var packages = new List<TestPackage>()
            {
                x
            };

            CreatePackages(packages, repositoryDir);
        }

        private static FileInfo CreateFullPackage(
            string repositoryDir,
            string id,
            string version)
        {
            return CreateFullPackage(repositoryDir, id, version, new List<Packaging.Core.PackageDependency>());
        }

        private static FileInfo CreateFullPackage(
            string repositoryDir,
            string id,
            string version,
            Packaging.Core.PackageDependency dependency)
        {
            return CreateFullPackage(repositoryDir, id, version,
                new List<Packaging.Core.PackageDependency>() { dependency });
        }

        private static FileInfo CreateFullPackage(
            string repositoryDir,
            string id,
            string version,
            IEnumerable<Packaging.Core.PackageDependency> dependencies)
        {
            var file = new FileInfo(Path.Combine(repositoryDir, $"{id}.{version}.nupkg"));

            file.Directory.Create();

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/config.xml", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/code.cs", new byte[] { 0 });
                zip.AddEntry("lib/net45/a.dll", new byte[] { 0 });
                zip.AddEntry($"build/net45/{id}.targets", @"<targets />", Encoding.UTF8);
                zip.AddEntry("native/net45/a.dll", new byte[] { 0 });
                zip.AddEntry("tools/a.exe", new byte[] { 0 });

                var nuspecXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>{id}</id>
                            <version>{version}</version>
                            <title />
                            <frameworkAssemblies>
                                <frameworkAssembly assemblyName=""System.Runtime"" />
                            </frameworkAssemblies>
                            <contentFiles>
                                <files include=""cs/net45/config/config.xml"" buildAction=""none"" />
                                <files include=""cs/net45/config/config.xml"" copyToOutput=""true"" flatten=""false"" />
                                <files include=""cs/net45/images/image.jpg"" buildAction=""embeddedresource"" />
                            </contentFiles>
                        </metadata>
                        </package>";

                var xml = XDocument.Parse(nuspecXml);

                if (dependencies.Any())
                {
                    var metadata = xml.Element(XName.Get("package")).Element(XName.Get("metadata"));

                    var dependenciesNode = new XElement(XName.Get("dependencies"));
                    var groupNode = new XElement(XName.Get("group"));
                    dependenciesNode.Add(groupNode);
                    metadata.Add(dependenciesNode);

                    foreach (var dependency in dependencies)
                    {
                        var node = new XElement(XName.Get("dependency"));
                        groupNode.Add(node);
                        node.Add(new XAttribute(XName.Get("id"), dependency.Id));
                        node.Add(new XAttribute(XName.Get("version"), dependency.VersionRange.ToNormalizedString()));

                        if (dependency.Include.Count > 0)
                        {
                            node.Add(new XAttribute(XName.Get("include"), string.Join(",", dependency.Include)));
                        }

                        if (dependency.Exclude.Count > 0)
                        {
                            node.Add(new XAttribute(XName.Get("exclude"), string.Join(",", dependency.Exclude)));
                        }
                    }
                }

                zip.AddEntry($"{id}.nuspec", xml.ToString(), Encoding.UTF8);
            }

            return file;
        }

        private class TestPackage
        {
            public string Id { get; set; } = "packageA";
            public string Version { get; set; } = "1.0.0";
            public List<TestPackage> Dependencies { get; set; } = new List<TestPackage>();
            public string Include { get; set; } = string.Empty;
            public string Exclude { get; set; } = string.Empty;

            public PackageIdentity Identity
            {
                get
                {
                    return new PackageIdentity(Id, NuGetVersion.Parse(Version));
                }
            }
        }

        private static void CreatePackages(List<TestPackage> packages, string repositoryPath)
        {
            var done = new HashSet<PackageIdentity>();
            var toCreate = new Stack<TestPackage>(packages);

            while (toCreate.Count > 0)
            {
                var package = toCreate.Pop();

                if (done.Add(package.Identity))
                {
                    var dependencies = package.Dependencies.Select(e =>
                        new Packaging.Core.PackageDependency(
                            e.Id,
                            VersionRange.Parse(e.Version),
                            string.IsNullOrEmpty(e.Include)
                                ? new List<string>()
                                : e.Include.Split(',').ToList(),
                            string.IsNullOrEmpty(e.Exclude)
                                ? new List<string>()
                                : e.Exclude.Split(',').ToList()));

                    CreateFullPackage(
                        repositoryPath,
                        package.Id,
                        package.Version,
                        dependencies);

                    foreach (var dep in package.Dependencies)
                    {
                        toCreate.Push(dep);
                    }
                }
            }
        }

        private bool IsEmptyFolder(IList<LockFileItem> group)
        {
            return group.SingleOrDefault()?.Path.EndsWith("/_._") == true;
        }

        private int GetNonEmptyCount(IList<LockFileItem> group)
        {
            return group.Where(e => !e.Path.EndsWith("/_._")).Count();
        }

        private static Dictionary<string, HashSet<string>> GetInstalledTargets(string workingDir)
        {
            var result = new Dictionary<string, HashSet<string>>();
            var projectDir = new DirectoryInfo(Path.Combine(workingDir, "project"));

            foreach (var dir in projectDir.GetDirectories())
            {
                result.Add(dir.Name, new HashSet<string>());

                var targets = dir.GetFiles("*.nuget.targets").SingleOrDefault();

                if (targets != null)
                {
                    var xml = XDocument.Load(targets.OpenRead());

                    foreach (var package in xml.Descendants()
                        .Where(node => node.Name.LocalName == "Import")
                        .Select(node => node.Attribute(XName.Get("Project")))
                        .Select(file => Path.GetFileNameWithoutExtension(file.Value)))
                    {
                        result[dir.Name].Add(package);
                    }
                }
            }

            return result;
        }

        public void Dispose()
        {
            // Clean up
            foreach (var folder in _testFolders)
            {
                TestFileSystemUtility.DeleteRandomTestFolders(folder);
            }
        }

        private ConcurrentBag<string> _testFolders = new ConcurrentBag<string>();
    }
}
