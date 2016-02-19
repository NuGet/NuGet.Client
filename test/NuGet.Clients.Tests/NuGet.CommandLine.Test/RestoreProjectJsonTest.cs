using System;
using System.IO;
using System.Linq;
using System.Text;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreProjectJsonTest
    {
        [Fact]
        public void RestoreProjectJson_RestoreFromSlnWithCsproj()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var projectDir1 = Path.Combine(workingPath, "test1");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(projectDir1);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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

                var slnPath = Path.Combine(workingPath, "xyz.sln");

                Util.CreateFile(workingPath, "xyz.sln",
                           @"
                        Microsoft Visual Studio Solution File, Format Version 12.00
                        # Visual Studio 14
                        VisualStudioVersion = 14.0.23107.0
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project(""{AAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""test1"", ""test1\test1.csproj"", ""{AA6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Global
                            GlobalSection(SolutionConfigurationPlatforms) = preSolution
                                Debug|Any CPU = Debug|Any CPU
                                Release|Any CPU = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(ProjectConfigurationPlatforms) = postSolution
                                {AA6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {AA6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
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

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                Assert.True(test1Lock.Exists);
            }
        }

        [Theory]
        [InlineData(null, 1, 2 * 60 * 1000)]
        [InlineData(null, 2, 2 * 60 * 1000)]
        [InlineData(null, 40, 4 * 60 * 1000)]
        [InlineData(null, 30, 3 * 60 * 1000)]
        [InlineData("0", 1, 2 * 60 * 1000)]
        [InlineData("-1", 1, 2 * 60 * 1000)]
        [InlineData("1", 1, 1000)]
        [InlineData("1", 2, 1000)]
        public void RestoreProjectJson_P2PTimeouts(string timeout, int projectCount, int expectedTimeOut)
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                Func<int, string> getProjectDir = (int i) => Path.Combine(workingPath, "test" + i);

                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                for (int i = 1; i <= projectCount; i++)
                {
                    string projectDir = getProjectDir(i);

                    Directory.CreateDirectory(projectDir);

                    Util.CreateFile(projectDir, "project.json",
                                                    @"{
                                                    'dependencies': {
                                                    },
                                                    'frameworks': {
                                                                'uap10.0': { }
                                                            }
                                                    }");

                    Util.CreateFile(projectDir, $"test{i}.csproj",
                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                            <Target Name=""_NuGet_GetProjectsReferencingProjectJsonInternal""></Target>
                        </Project>");
                }

                var slnPath = Path.Combine(workingPath, "xyz.sln");

                var sln = new StringBuilder();

                sln.AppendLine(@"
                        Microsoft Visual Studio Solution File, Format Version 12.00
                        # Visual Studio 14
                        VisualStudioVersion = 14.0.23107.0
                        MinimumVisualStudioVersion = 10.0.40219.1");

                var guids = new string[projectCount + 1];

                for (int i = 1; i <= projectCount; i++)
                {
                    guids[i] = Guid.NewGuid().ToString().ToUpper();
                    var projGuid = guids[i];

                    sln.AppendLine(
@"                        Project(""{" + Guid.NewGuid().ToString().ToUpper() + @"}"") = ""test" + i +
                            @""", ""test" + i + @"\test" + i + @".csproj"", ""{" + projGuid + @"}""
                        EndProject");
                }

                sln.AppendLine(
@"                        Global
                            GlobalSection(SolutionConfigurationPlatforms) = preSolution
                                Debug|Any CPU = Debug|Any CPU
                                Release|Any CPU = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(ProjectConfigurationPlatforms) = postSolution");

                for (int i = 0; i < projectCount; i++)
                {
                    sln.AppendLine($"                                {guids[i]}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
                    sln.AppendLine($"                                {guids[i]}.Debug|Any CPU.Build.0 = Debug|Any CPU");
                }

                sln.AppendLine(@"                            EndGlobalSection
                            GlobalSection(SolutionProperties) = preSolution
                                HideSolutionNode = FALSE
                            EndGlobalSection
                        EndGlobal");

                var solution = sln.ToString();

                Util.CreateFile(workingPath, "xyz.sln", solution);

                string args;

                if (timeout == null)
                {
                    args = $"restore -verbosity detailed -Source {repositoryPath} -solutionDir {workingPath} {slnPath}";
                }
                else
                {
                    args = $"restore -verbosity detailed -Source {repositoryPath} -solutionDir {workingPath} -Project2ProjectTimeOut {timeout} {slnPath}";
                }
                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    args,
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + Environment.NewLine + r.Item3);

                var lines = r.Item2.Split(
                                new[] { Environment.NewLine },
                                StringSplitOptions.RemoveEmptyEntries);

                var prefix = "MSBuild P2P timeout [ms]: ";

                var timeoutLineResult = lines.Where(line => line.Contains(prefix)).SingleOrDefault();

                Assert.NotNull(timeoutLineResult);

                var timeoutResult = timeoutLineResult.Substring(timeoutLineResult.IndexOf(prefix) + prefix.Length);
                Assert.Equal(expectedTimeOut, int.Parse(timeoutResult));

                for (int i = 1; i < projectCount + 1; i++)
                {
                    var test1Lock = new FileInfo(Path.Combine(getProjectDir(i), "project.lock.json"));

                    Assert.True(test1Lock.Exists);
                }
            }
        }

        [Fact]
        public void RestoreProjectJson_RestoreFromSlnWithReferenceOutputAssemblyFalse()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var projectDir1 = Path.Combine(workingPath, "test1");
                var projectDir2 = Path.Combine(workingPath, "test2");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(projectDir1);
                Directory.CreateDirectory(projectDir2);
                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
                            <ItemGroup Label=""Project References"">
                            <ProjectReference Include=""" + projectDir2 + @"\\test2.csproj"">
                              <Project>{BB6279C1-B5EE-4C6B-9FA3-A794CE195136}</Project>
                              <Name>Test2</Name>
                              <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
                            </ProjectReference>
                            </ItemGroup>
                        </Project>");

                Util.CreateFile(projectDir2, "project.json",
                                    @"{
                                        'version': '1.0.0-*',
                                        'dependencies': {
                                            ""packageA"": ""1.0.0""
                                        },
                                        'frameworks': {
                                                    'uap10.0': { }
                                                }
                                        }");

                Util.CreateFile(projectDir2, "test2.csproj",
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
                        Project(""{BBE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""test2"", ""test2\test2.csproj"", ""{BB6279C1-B5EE-4C6B-9FA3-A794CE195136}""
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

                var packageA = Util.CreateTestPackageBuilder("packageA", "1.0.0");
                var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");
                packageA.Files.Add(libA);
                Util.CreateTestPackage(packageA, repositoryPath);

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

                var format = new LockFileFormat();
                var lockFile1 = format.Read(test1Lock.FullName);
                var lockFile2 = format.Read(test2Lock.FullName);

                var a1 = lockFile1.Libraries
                    .Where(lib => lib.Name.Equals("packageA", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                var a2 = lockFile2.Libraries
                    .Where(lib => lib.Name.Equals("packageA", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                Assert.True(test1Lock.Exists);
                Assert.True(test2Lock.Exists);

                // Verify the package does exist in 2
                Assert.NotNull(a2);

                // Verify the package does not flow to 1
                Assert.Null(a1);
            }
        }

        [Fact]
        public void RestoreProjectJson_RestoreProjectFileNotFound()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
        }

        [Fact]
        public void RestoreProjectJson_RestoreProjectJsonFileNotFound()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
        }

        [Fact]
        public void RestoreProjectJson_RestoreXProj()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var projectDir1 = Path.Combine(workingPath, "test1");
                var project1Path = Path.Combine(projectDir1, "test1.xproj");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(projectDir1);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                Assert.True(test1Lock.Exists);
            }
        }

        [Fact]
        public void RestoreProjectJson_RestoreUnknownProj()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var projectDir1 = Path.Combine(workingPath, "test1");
                var project1Path = Path.Combine(projectDir1, "test1.abcproj");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(projectDir1);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
        }

        [Fact]
        public void RestoreProjectJson_RestoreFromSlnWithUnknownProjAndCsproj()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var projectDir1 = Path.Combine(workingPath, "test1");
                var projectDir2 = Path.Combine(workingPath, "test2");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(projectDir1);
                Directory.CreateDirectory(projectDir2);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));

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
        }

        [Fact]
        public void RestoreProjectJson_RestoreFromSlnWithXprojAndCsproj()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var projectDir1 = Path.Combine(workingPath, "test1");
                var projectDir2 = Path.Combine(workingPath, "test2");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(projectDir1);
                Directory.CreateDirectory(projectDir2);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
                Assert.True(test2Lock.Exists);
            }
        }

        [Fact]
        public void RestoreProjectJson_FloatReleaseLabelHighestPrelease()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
                    "project.json",
                    "-nocache"
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
        }

        [Fact]
        public void RestoreProjectJson_FloatReleaseLabelTakesStable()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
        }

        [Fact]
        public void RestoreProjectJson_FloatIncludesStableOnly()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
        }

        [Fact]
        public void RestoreProjectJson_RestoreFiltersToStablePackages()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
        }

        [Fact]
        public void RestoreProjectJson_RestoreBumpsFromStableToPrereleaseWhenNeeded()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
        }

        [Fact]
        public void RestoreProjectJson_RestoreDowngradesStableDependency()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
        }

        [Fact(Skip = "https://github.com/NuGet/Home/issues/1330")]
        public void RestoreProjectJson_RestoreDowngradesFromStableToPrereleaseWhenNeeded()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
        }

        [Fact]
        public void RestoreProjectJson_SolutionFileWithAllProjectsInOneFolder()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var projectDir = Path.Combine(workingPath, "abc");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(projectDir);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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

                Util.CreateFile(projectDir, "testA.csproj", Util.GetCSProjXML("testA")); // testA.project.json
                Util.CreateFile(projectDir, "testB.csproj", Util.GetCSProjXML("testB")); // testB.project.json
                Util.CreateFile(projectDir, "testC.csproj", Util.GetCSProjXML("testC")); // packages.testC.config
                Util.CreateFile(projectDir, "testD.csproj", Util.GetCSProjXML("testD")); // Non-nuget

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
        }

        [Fact]
        public void RestoreProjectJson_GenerateFilesWithProjectNameFromCSProj()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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

                Util.CreateFile(workingPath, "test.csproj", Util.GetCSProjXML("test"));

                var csprojPath = Path.Combine(workingPath, "test.csproj");

                string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                csprojPath
            };

                var targetFilePath = Path.Combine(workingPath, "test.nuget.targets");
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
        }

        [Fact]
        public void RestoreProjectJson_GenerateTargetsFileFromSln()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var nugetexe = Util.GetNuGetExePath();

                var repositoryPath = Path.Combine(workingPath, "Repository");
                var projectDir = Path.Combine(workingPath, "abc");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(projectDir);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));

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

                Util.CreateFile(projectDir, "test.csproj", Util.GetCSProjXML("test"));

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

                var targetFilePath = Path.Combine(projectDir, "test.nuget.targets");

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
        }

        [Fact]
        public void RestoreProjectJson_GenerateTargetsFileFromCSProj()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Util.CreateConfigForGlobalPackagesFolder(workingPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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

                Util.CreateFile(workingPath, "test.csproj", Util.GetCSProjXML("test"));

                var csprojPath = Path.Combine(workingPath, "test.csproj");

                string[] args = new string[] {
                    "restore",
                    "-Source",
                    repositoryPath,
                    "-solutionDir",
                    workingPath,
                    csprojPath
                };

                var targetFilePath = Path.Combine(workingPath, "test.nuget.targets");

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
        }

        [Fact]
        public void RestoreProjectJson_GenerateTargetsFileWithFolder()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                string folderName = Path.GetFileName(workingPath);

                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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

                Util.CreateFile(workingPath, "test.csproj", Util.GetCSProjXML("test"));

                string[] args = new string[] {
                    "restore",
                    "-Source",
                    repositoryPath,
                    "-solutionDir",
                    workingPath,
                    "test.csproj"
                };

                var targetFilePath = Path.Combine(workingPath, "test.nuget.targets");

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
        }

        [Fact]
        public void RestoreProjectJson_SkipTargetsForProjectJsonOnly()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                string folderName = Path.GetFileName(workingPath);

                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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

                var targetFilePath = Path.Combine(workingPath, $"{folderName}.nuget.targets");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                Assert.False(File.Exists(targetFilePath));
            }
        }

        [Fact]
        public void RestoreProjectJson_SkipTargetsForXProjProjects()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                string folderName = Path.GetFileName(workingPath);

                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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

                Util.CreateFile(workingPath, "test.xproj", Util.GetCSProjXML("test"));

                string[] args = new string[] {
                    "restore",
                    "-Source",
                    repositoryPath,
                    "-solutionDir",
                    workingPath,
                    "test.xproj"
                };

                var targetFilePath = Path.Combine(workingPath, "test.nuget.targets");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                Assert.False(File.Exists(targetFilePath));
            }
        }

        [Fact]
        public void RestoreProjectJson_GenerateTargetsForRootBuildFolderIgnoreSubFolders()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                string folderName = Path.GetFileName(workingPath);

                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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

                Util.CreateFile(workingPath, "test.csproj", Util.GetCSProjXML("test"));


                string[] args = new string[] {
                    "restore",
                    "-Source",
                    repositoryPath,
                    "-solutionDir",
                    workingPath,
                    "test.csproj"
                };

                var targetFilePath = Path.Combine(workingPath, "test.nuget.targets");

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
        }

        [Fact]
        public void RestoreProjectJson_GenerateTargetsPersistsWithMultipleRestores()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                string folderName = Path.GetFileName(workingPath);

                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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

                Util.CreateFile(workingPath, "test.csproj", Util.GetCSProjXML("test"));

                string[] args = new string[] {
                    "restore",
                    "-Source",
                    repositoryPath,
                    "-solutionDir",
                    workingPath,
                    "test.csproj"
                };

                var targetFilePath = Path.Combine(workingPath, "test.nuget.targets");

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
        }

        [Fact]
        public void RestoreProjectJson_IsLockedTrueAfterRestore()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
        }

        [Fact]
        public void RestoreProjectJson_CorruptedLockFile()
        {
            // Arrange
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));
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
        }
    }
}
