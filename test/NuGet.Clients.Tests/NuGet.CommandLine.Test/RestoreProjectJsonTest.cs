using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using NuGet.ProjectModel;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreProjectJsonTest : IDisposable
    {
        [Fact]
        public void RestoreProjectJson_RestoreProjectFileNotFound()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);

            var projectFilePath = Path.Combine(workingPath, "test.fsproj");

            string[] args = new string[] {
                "restore",
                projectFilePath,
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var test1Lock = new FileInfo(Path.Combine(workingPath, "project.lock.json"));

            // Assert
            Assert.True(1 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.False(test1Lock.Exists);
            Assert.Contains("input file does not exist", r.Item3, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RestoreProjectJson_RestoreProjectJsonFileNotFound()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);

            var projectJsonPath = Path.Combine(workingPath, "project.json");

            string[] args = new string[] {
                "restore",
                projectJsonPath,
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var test1Lock = new FileInfo(Path.Combine(workingPath, "project.lock.json"));

            // Assert
            Assert.True(1 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.False(test1Lock.Exists);
            Assert.Contains("input file does not exist", r.Item3, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RestoreProjectJson_RestoreXProj()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var projectDir1 = Path.Combine(workingPath, "test1");
            var project1Path = Path.Combine(projectDir1, "test1.xproj");
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(projectDir1);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);

            Util.CreateFile(projectDir1, "project.json",
                                            @"{
                                            'dependencies': {
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(projectDir1, "test1.xproj",
                                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""_NuGet_GetProjectsReferencingProjectJsonInternal""></Target>
        </Project>");

            string[] args = new string[] {
                "restore",
                project1Path,
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var test1Lock = new FileInfo(Path.Combine(projectDir1, "project.lock.json"));

            // Assert
            Assert.True(1 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.False(test1Lock.Exists);
            Assert.Contains("error parsing solution file", r.Item3, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RestoreProjectJson_RestoreUnknownProj()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var projectDir1 = Path.Combine(workingPath, "test1");
            var project1Path = Path.Combine(projectDir1, "test1.abcproj");
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(projectDir1);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);

            Util.CreateFile(projectDir1, "project.json",
                                            @"{
                                            'dependencies': {
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(projectDir1, "test1.abcproj",
                                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""_NuGet_GetProjectsReferencingProjectJsonInternal""></Target>
        </Project>");

            string[] args = new string[] {
                "restore",
                project1Path,
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var test1Lock = new FileInfo(Path.Combine(projectDir1, "project.lock.json"));

            // Assert
            Assert.True(1 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.False(test1Lock.Exists);
            Assert.Contains("error parsing solution file", r.Item3, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RestoreProjectJson_RestoreFromSlnWithUnknownProjAndCsproj()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var projectDir1 = Path.Combine(workingPath, "test1");
            var projectDir2 = Path.Combine(workingPath, "test2");
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(projectDir1);
            Util.CreateDirectory(projectDir2);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);

            var packageA = Util.CreateTestPackageBuilder("packageA", "1.1.0-beta-01");
            var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");
            packageA.Files.Add(libA);

            Util.CreateTestPackage(packageA, repositoryPath);

            Util.CreateFile(projectDir1, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(projectDir1, "test1.csproj",
                                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""_NuGet_GetProjectsReferencingProjectJsonInternal""></Target>
        </Project>");

            Util.CreateFile(projectDir2, "project.json",
                                @"{
                                            'version': '1.0.0-*',
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(projectDir2, "test2.abcproj",
                                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""_NuGet_GetProjectsReferencingProjectJsonInternal""></Target>
        </Project>");

            var slnPath = Path.Combine(workingPath, "xyz.sln");

            Util.CreateFile(workingPath, "xyz.sln",
                       @"
                        Microsoft Visual Studio Solution File, Format Version 12.00
                        # Visual Studio 14
                        VisualStudioVersion = 14.0.23107.0
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project(""{AAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""test1"", ""test1\test1.csproj"", ""{AA6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Project(""{BBE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""test2"", ""test2\test2.abcproj"", ""{BB6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Global
                            GlobalSection(SolutionConfigurationPlatforms) = preSolution
                                Debug|Any CPU = Debug|Any CPU
                                Release|Any CPU = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(ProjectConfigurationPlatforms) = postSolution
                                {AA6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {AA6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
                                {BB6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {BB6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
                            EndGlobalSection
                            GlobalSection(SolutionProperties) = preSolution
                                HideSolutionNode = FALSE
                            EndGlobalSection
                        EndGlobal
                        ");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                slnPath
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var test1Lock = new FileInfo(Path.Combine(projectDir1, "project.lock.json"));
            var test2Lock = new FileInfo(Path.Combine(projectDir2, "project.lock.json"));

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

            Assert.True(test1Lock.Exists);
            Assert.True(test2Lock.Exists);
        }

        [Fact]
        public void RestoreProjectJson_RestoreFromSlnWithXprojAndCsproj()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var projectDir1 = Path.Combine(workingPath, "test1");
            var projectDir2 = Path.Combine(workingPath, "test2");
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(projectDir1);
            Util.CreateDirectory(projectDir2);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);

            Util.CreateFile(projectDir1, "project.json",
                                            @"{
                                            'dependencies': {
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(projectDir1, "test1.csproj",
                                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""_NuGet_GetProjectsReferencingProjectJsonInternal""></Target>
        </Project>");

            Util.CreateFile(projectDir2, "project.json",
                                @"{
                                            'version': '1.0.0-*',
                                            'dependencies': {
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(projectDir2, "test2.xproj",
                                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""_NuGet_GetProjectsReferencingProjectJsonInternal""></Target>
        </Project>");

            var slnPath = Path.Combine(workingPath, "xyz.sln");

            Util.CreateFile(workingPath, "xyz.sln",
                       @"
                        Microsoft Visual Studio Solution File, Format Version 12.00
                        # Visual Studio 14
                        VisualStudioVersion = 14.0.23107.0
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project(""{AAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""test1"", ""test1\test1.csproj"", ""{AA6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Project(""{BBE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""test2"", ""test2\test2.xproj"", ""{BB6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Global
                            GlobalSection(SolutionConfigurationPlatforms) = preSolution
                                Debug|Any CPU = Debug|Any CPU
                                Release|Any CPU = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(ProjectConfigurationPlatforms) = postSolution
                                {AA6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {AA6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
                                {BB6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {BB6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
                            EndGlobalSection
                            GlobalSection(SolutionProperties) = preSolution
                                HideSolutionNode = FALSE
                            EndGlobalSection
                        EndGlobal
                        ");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                slnPath
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var test1Lock = new FileInfo(Path.Combine(projectDir1, "project.lock.json"));
            var test2Lock = new FileInfo(Path.Combine(projectDir2, "project.lock.json"));

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

            Assert.True(test1Lock.Exists);
            Assert.False(test2Lock.Exists);
        }

        [Fact]
        public void RestoreProjectJson_FloatReleaseLabelHighestPrelease()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateTestPackage("packageA", "1.0.0-alpha", repositoryPath);
            Util.CreateTestPackage("packageA", "1.0.0-beta-01", repositoryPath);
            Util.CreateTestPackage("packageA", "1.0.0-beta-02", repositoryPath);
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.0.0-*'
                                            },
                                            'frameworks': {
                                                    'uap10.0': { }
                                                }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();

            var lockFile = lockFileFormat.Read(lockFilePath);

            var installedA = lockFile.Targets.First().Libraries.Single(package => package.Name == "packageA");

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.Equal("1.0.0-beta-02", installedA.Version.ToNormalizedString());
        }

        [Fact]
        public void RestoreProjectJson_FloatReleaseLabelTakesStable()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateTestPackage("packageA", "1.0.0", repositoryPath);
            Util.CreateTestPackage("packageA", "2.0.0", repositoryPath);
            Util.CreateTestPackage("packageA", "1.0.0-alpha", repositoryPath);
            Util.CreateTestPackage("packageA", "1.0.0-beta-01", repositoryPath);
            Util.CreateTestPackage("packageA", "1.0.0-beta-02", repositoryPath);
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.0.0-*'
                                            },
                                            'frameworks': {
                                                    'uap10.0': { }
                                                }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();

            var lockFile = lockFileFormat.Read(lockFilePath);

            var installedA = lockFile.Targets.First().Libraries.Single(package => package.Name == "packageA");

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.Equal("1.0.0", installedA.Version.ToNormalizedString());
        }

        [Fact]
        public void RestoreProjectJson_FloatIncludesStableOnly()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateTestPackage("packageA", "1.0.0", repositoryPath);
            Util.CreateTestPackage("packageA", "1.0.9", repositoryPath);
            Util.CreateTestPackage("packageA", "1.0.10", repositoryPath);
            Util.CreateTestPackage("packageA", "1.1.15", repositoryPath);
            Util.CreateTestPackage("packageA", "1.0.15-beta", repositoryPath);
            Util.CreateTestPackage("packageA", "1.0.9-beta", repositoryPath);
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.0.*'
                                            },
                                            'frameworks': {
                                                    'uap10.0': { }
                                                }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();

            var lockFile = lockFileFormat.Read(lockFilePath);

            var installedA = lockFile.Targets.First().Libraries.Single(package => package.Name == "packageA");

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.Equal("1.0.10", installedA.Version.ToNormalizedString());
        }

        [Fact]
        public void RestoreProjectJson_RestoreFiltersToStablePackages()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateTestPackage("packageA", "1.0.0", repositoryPath, "win8", "packageB", "1.0.0");
            Util.CreateTestPackage("packageB", "1.0.0-beta", repositoryPath);
            Util.CreateTestPackage("packageB", "2.0.0-beta", repositoryPath);
            Util.CreateTestPackage("packageB", "3.0.0", repositoryPath);
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.0.0'
                                            },
                                            'frameworks': {
                                                    'uap10.0': { }
                                                }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();

            var lockFile = lockFileFormat.Read(lockFilePath);

            var installedB = lockFile.Targets.First().Libraries.Where(package => package.Name == "packageB").ToList();

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.Equal(1, installedB.Count);
            Assert.Equal("3.0.0", installedB.Single().Version.ToNormalizedString());
        }

        [Fact]
        public void RestoreProjectJson_RestoreBumpsFromStableToPrereleaseWhenNeeded()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateTestPackage("packageA", "1.0.0", repositoryPath, "win8", "packageC", "1.0.0");
            Util.CreateTestPackage("packageB", "1.0.0-beta", repositoryPath, "win8", "packageC", "2.0.0-beta");
            Util.CreateTestPackage("packageC", "1.0.0", repositoryPath);
            Util.CreateTestPackage("packageC", "2.0.0-beta", repositoryPath);
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.0.0',
                                            'packageB': '1.0.0-*'
                                            },
                                            'frameworks': {
                                                    'uap10.0': { }
                                                }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();

            var lockFile = lockFileFormat.Read(lockFilePath);

            var installedC = lockFile.Targets.First().Libraries.Single(package => package.Name == "packageC");

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.Equal("2.0.0-beta", installedC.Version.ToNormalizedString());
        }

        [Fact]
        public void RestoreProjectJson_RestoreDowngradesStableDependency()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateTestPackage("packageA", "1.0.0", repositoryPath, "win8", "packageC", "1.0.0");
            Util.CreateTestPackage("packageB", "1.0.0", repositoryPath, "win8", "packageC", "[2.1.0]");
            Util.CreateTestPackage("packageC", "3.0.0", repositoryPath);
            Util.CreateTestPackage("packageC", "2.1.0", repositoryPath);
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.0.0',
                                            'packageB': '1.0.0'
                                            },
                                            'frameworks': {
                                                    'uap10.0': { }
                                                }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();

            var lockFile = lockFileFormat.Read(lockFilePath);

            var installedC = lockFile.Targets.First().Libraries.Single(package => package.Name == "packageC");

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.Equal("2.1.0", installedC.Version.ToNormalizedString());
        }

        [Fact(Skip = "https://github.com/NuGet/Home/issues/1330")]
        public void RestoreProjectJson_RestoreDowngradesFromStableToPrereleaseWhenNeeded()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateTestPackage("packageA", "1.0.0", repositoryPath, "win8", "packageC", "1.0.0");
            Util.CreateTestPackage("packageB", "1.0.0-beta", repositoryPath, "win8", "packageC", "[2.0.0-beta]");
            Util.CreateTestPackage("packageC", "3.0.0", repositoryPath);
            Util.CreateTestPackage("packageC", "2.0.0-beta", repositoryPath);
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.0.0',
                                            'packageB': '1.0.0-*'
                                            },
                                            'frameworks': {
                                                    'uap10.0': { }
                                                }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();

            var lockFile = lockFileFormat.Read(lockFilePath);

            var installedC = lockFile.Targets.First().Libraries.Single(package => package.Name == "packageC");

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.Equal("2.0.0-beta", installedC.Version.ToNormalizedString());
        }

        [Fact]
        public void RestoreProjectJson_SolutionFileWithAllProjectsInOneFolder()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var projectDir = Path.Combine(workingPath, "abc");
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(projectDir);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            var packageA = Util.CreateTestPackageBuilder("packageA", "1.1.0-beta-01");
            var targetContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Project ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>";

            var targetA = Util.CreatePackageFile("build/uap/packageA.targets", targetContent);
            var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");

            packageA.Files.Add(targetA);
            packageA.Files.Add(libA);

            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateTestPackage(packageA, repositoryPath);

            Util.CreateFile(projectDir, "testA.project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(projectDir, "testB.project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(projectDir, "packages.testC.config",
                                            @"<packages>
                                <package id=""packageA"" version=""1.1.0-beta-01"" targetFramework=""net45"" />
                            </packages>");

            Util.CreateFile(projectDir, "testA.csproj", CSProjXML); // testA.project.json
            Util.CreateFile(projectDir, "testB.csproj", CSProjXML); // testB.project.json
            Util.CreateFile(projectDir, "testC.csproj", CSProjXML); // packages.testC.config
            Util.CreateFile(projectDir, "testD.csproj", CSProjXML); // Non-nuget

            var slnPath = Path.Combine(workingPath, "xyz.sln");

            Util.CreateFile(workingPath, "xyz.sln",
                       @"
                        Microsoft Visual Studio Solution File, Format Version 12.00
                        # Visual Studio 14
                        VisualStudioVersion = 14.0.23107.0
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project(""{AAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""testA"", ""abc\testA.csproj"", ""{6A6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Project(""{ABE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""testB"", ""abc\testB.csproj"", ""{6A6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Project(""{ACE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""testC"", ""abc\testC.csproj"", ""{6A6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Project(""{ADE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""testD"", ""abc\testD.csproj"", ""{6A6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Global
                            GlobalSection(SolutionConfigurationPlatforms) = preSolution
                                Debug|Any CPU = Debug|Any CPU
                                Release|Any CPU = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(ProjectConfigurationPlatforms) = postSolution
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Release|Any CPU.ActiveCfg = Release|Any CPU
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Release|Any CPU.Build.0 = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(SolutionProperties) = preSolution
                                HideSolutionNode = FALSE
                            EndGlobalSection
                        EndGlobal
                        ");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                slnPath
            };

            var targetFileA = Path.Combine(projectDir, "testA.nuget.targets");
            var targetFileB = Path.Combine(projectDir, "testB.nuget.targets");
            var lockFileA = Path.Combine(projectDir, "testA.project.lock.json");
            var lockFileB = Path.Combine(projectDir, "testB.project.lock.json");

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(targetFileA));
            Assert.True(File.Exists(targetFileB));
            Assert.True(File.Exists(lockFileA));
            Assert.True(File.Exists(lockFileB));
            Assert.True(File.Exists(Path.Combine(workingPath, "packages/packageA.1.1.0-beta-01/packageA.1.1.0-beta-01.nupkg")));
        }

        [Fact]
        public void RestoreProjectJson_GenerateFilesWithProjectNameFromCSProj()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            var packageA = Util.CreateTestPackageBuilder("packageA", "1.1.0-beta-01");
            var targetContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Project ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>";
            var targetA = Util.CreatePackageFile("build/uap/packageA.targets", targetContent);
            var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");
            packageA.Files.Add(targetA);
            packageA.Files.Add(libA);

            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateTestPackage(packageA, repositoryPath);

            Util.CreateFile(workingPath, "test.project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(workingPath, "test.csproj",
                                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""_NuGet_GetProjectsReferencingProjectJsonInternal""></Target>
        </Project>");

            var csprojPath = Path.Combine(workingPath, "test.csproj");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                csprojPath
            };

            var targetFilePath = Path.Combine(workingPath, $"test.nuget.targets");
            var lockFilePath = Path.Combine(workingPath, $"test.project.lock.json");

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(lockFilePath));
            Assert.True(File.Exists(targetFilePath));
        }

        [Fact]
        public void RestoreProjectJson_GenerateTargetsFileFromSln()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var projectDir = Path.Combine(workingPath, "abc");
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(projectDir);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            var packageA = Util.CreateTestPackageBuilder("packageA", "1.1.0-beta-01");
            var targetContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Project ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>";

            var targetA = Util.CreatePackageFile("build/uap/packageA.targets", targetContent);
            var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");

            packageA.Files.Add(targetA);
            packageA.Files.Add(libA);

            Util.CreateTestPackage(packageA, repositoryPath);

            Util.CreateFile(projectDir, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(projectDir, "test.csproj",
                                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""_NuGet_GetProjectsReferencingProjectJsonInternal""></Target>
        </Project>");

            var slnPath = Path.Combine(workingPath, "xyz.sln");

            Util.CreateFile(workingPath, "xyz.sln",
                       @"
                        Microsoft Visual Studio Solution File, Format Version 12.00
                        # Visual Studio 14
                        VisualStudioVersion = 14.0.23107.0
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""test"", ""abc\test.csproj"", ""{6A6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Global
                            GlobalSection(SolutionConfigurationPlatforms) = preSolution
                                Debug|Any CPU = Debug|Any CPU
                                Release|Any CPU = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(ProjectConfigurationPlatforms) = postSolution
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Release|Any CPU.ActiveCfg = Release|Any CPU
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Release|Any CPU.Build.0 = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(SolutionProperties) = preSolution
                                HideSolutionNode = FALSE
                            EndGlobalSection
                        EndGlobal
                        ");

            var csprojPath = Path.Combine(projectDir, "test.csproj");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                slnPath
            };

            var targetFilePath = Path.Combine(projectDir, $"test.nuget.targets");

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(targetFilePath));

            var targetsFile = File.OpenText(targetFilePath).ReadToEnd();
            Assert.True(targetsFile.IndexOf(@"build\uap\packageA.targets") > -1);
        }


        [Fact]
        public void RestoreProjectJson_GenerateTargetsFileFromCSProj()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            var packageA = Util.CreateTestPackageBuilder("packageA", "1.1.0-beta-01");
            var packageB = Util.CreateTestPackageBuilder("packageB", "2.2.0-beta-02");

            var targetContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Project ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>";

            var targetA = Util.CreatePackageFile("build/uap/packageA.targets", targetContent);
            var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");

            packageA.Files.Add(targetA);
            packageA.Files.Add(libA);

            var targetB = Util.CreatePackageFile("build/uap/packageB.targets", targetContent);
            var libB = Util.CreatePackageFile("lib/uap/b.dll", "b");

            packageB.Files.Add(targetB);
            packageB.Files.Add(libB);

            Util.CreateTestPackage(packageA, repositoryPath);
            Util.CreateTestPackage(packageB, repositoryPath);

            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*',
                                            'packageB': '2.2.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(workingPath, "test.csproj",
                                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""_NuGet_GetProjectsReferencingProjectJsonInternal""></Target>
        </Project>");

            var csprojPath = Path.Combine(workingPath, "test.csproj");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                csprojPath
            };

            var targetFilePath = Path.Combine(workingPath, $"test.nuget.targets");

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(targetFilePath));

            var targetsFile = File.OpenText(targetFilePath).ReadToEnd();
            Assert.True(targetsFile.IndexOf(@"build\uap\packageA.targets") > -1);
            Assert.True(targetsFile.IndexOf(@"build\uap\packageB.targets") > -1);
        }

        [Fact]
        public void RestoreProjectJson_GenerateTargetsFileWithFolder()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            var packageA = Util.CreateTestPackageBuilder("packageA", "1.1.0-beta-01");
            var packageB = Util.CreateTestPackageBuilder("packageB", "2.2.0-beta-02");

            var targetContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Project ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>";

            var targetA = Util.CreatePackageFile("build/uap/packageA.targets", targetContent);
            var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");

            packageA.Files.Add(targetA);
            packageA.Files.Add(libA);

            var targetB = Util.CreatePackageFile("build/uap/packageB.targets", targetContent);
            var libB = Util.CreatePackageFile("lib/uap/b.dll", "b");

            packageB.Files.Add(targetB);
            packageB.Files.Add(libB);

            Util.CreateTestPackage(packageA, repositoryPath);
            Util.CreateTestPackage(packageB, repositoryPath);

            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*',
                                            'packageB': '2.2.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            var targetFilePath = Path.Combine(workingPath, $"{guid}.nuget.targets");

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(targetFilePath));

            var targetsFile = File.OpenText(targetFilePath).ReadToEnd();
            Assert.True(targetsFile.IndexOf(@"build\uap\packageA.targets") > -1);
            Assert.True(targetsFile.IndexOf(@"build\uap\packageB.targets") > -1);
        }

        [Fact]
        public void RestoreProjectJson_GenerateTargetsForRootBuildFolderIgnoreSubFolders()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            var packageA = Util.CreateTestPackageBuilder("packageA", "3.1.0");

            var targetContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Project ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>";

            var targetA = Util.CreatePackageFile("build/net45/packageA.targets", targetContent);
            var targetARoot = Util.CreatePackageFile("build/packageA.targets", targetContent);

            packageA.Files.Add(targetA);
            packageA.Files.Add(targetARoot);

            Util.CreateTestPackage(packageA, repositoryPath);

            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '3.1.0',
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            var targetFilePath = Path.Combine(workingPath, $"{guid}.nuget.targets");

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(targetFilePath));

            var targetsFile = File.OpenText(targetFilePath).ReadToEnd();
            // Verify the target was added
            Assert.True(targetsFile.IndexOf(@"build\packageA.targets") > -1);

            // Verify sub directories were not used
            Assert.True(targetsFile.IndexOf(@"build\net45\packageA.targets") < 0);
        }

        [Fact]
        public void RestoreProjectJson_GenerateTargetsPersistsWithMultipleRestores()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            var packageA = Util.CreateTestPackageBuilder("packageA", "1.1.0-beta-01");
            var packageB = Util.CreateTestPackageBuilder("packageB", "2.2.0-beta-02");

            var targetContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Project ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>";

            var targetA = Util.CreatePackageFile("build/uap/packageA.targets", targetContent);
            var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");

            packageA.Files.Add(targetA);
            packageA.Files.Add(libA);

            var targetB = Util.CreatePackageFile("build/uap/packageB.targets", targetContent);
            var libB = Util.CreatePackageFile("lib/uap/b.dll", "b");

            packageB.Files.Add(targetB);
            packageB.Files.Add(libB);

            Util.CreateTestPackage(packageA, repositoryPath);
            Util.CreateTestPackage(packageB, repositoryPath);

            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*',
                                            'packageB': '2.2.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            var targetFilePath = Path.Combine(workingPath, $"{guid}.nuget.targets");

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(targetFilePath));

            using (var stream = File.OpenText(targetFilePath))
            {
                var targetsFile = stream.ReadToEnd();
                Assert.True(targetsFile.IndexOf(@"build\uap\packageA.targets") > -1);
                Assert.True(targetsFile.IndexOf(@"build\uap\packageB.targets") > -1);
            }

            // Act 2
            r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert 2
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(targetFilePath));

            using (var stream = File.OpenText(targetFilePath))
            {
                var targetsFile = stream.ReadToEnd();
                Assert.True(targetsFile.IndexOf(@"build\uap\packageA.targets") > -1);
                Assert.True(targetsFile.IndexOf(@"build\uap\packageB.targets") > -1);
            }

            // Act 3
            r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert 3
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(targetFilePath));

            using (var stream = File.OpenText(targetFilePath))
            {
                var targetsFile = stream.ReadToEnd();
                Assert.True(targetsFile.IndexOf(@"build\uap\packageA.targets") > -1);
                Assert.True(targetsFile.IndexOf(@"build\uap\packageB.targets") > -1);
            }
        }

        [Fact]
        public void RestoreProjectJson_IsLockedTrueAfterRestore()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
            Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0',
                                            'packageB': '2.2.0'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            // Restore once to get a lock file
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Set IsLocked=true
            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Read(lockFilePath);
            lockFile.IsLocked = true;
            lockFileFormat.Write(lockFilePath, lockFile);

            // Act
            // Restore using the locked lock file
            r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFileAfter = lockFileFormat.Read(lockFilePath);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(lockFileAfter.IsLocked);
            Assert.True(lockFile.Equals(lockFileAfter));
        }

        [Fact]
        public void RestoreProjectJson_CorruptedLockFile()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
            Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0',
                                            'packageB': '2.2.0'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();
            using (var writer = new StreamWriter(lockFilePath))
            {
                writer.WriteLine("{ \"CORRUPTED!\": \"yep\"");
            }

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFile = lockFileFormat.Read(lockFilePath);

            // Assert
            // If the library count can be obtained then a new lock file was created
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.Equal(2, lockFile.Libraries.Count);
        }

        private const string CSProjXML = @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""_NuGet_GetProjectsReferencingProjectJsonInternal""></Target>
        </Project>";

        /// <summary>
        /// Store all directories used by the unit tests and clean them up at the end during Dispose()
        /// </summary>
        private ConcurrentDictionary<string, bool> _dirs = new ConcurrentDictionary<string, bool>();

        public void Dispose()
        {
            foreach (var dir in _dirs.Keys)
            {
                try
                {
                    Util.DeleteDirectory(dir);
                }
                catch
                {

                }
            }
        }
    }
}
