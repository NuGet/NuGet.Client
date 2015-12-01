using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class RuntimePackageTests : IDisposable
    {
        [Fact]
        public async Task RuntimePackage_BasicRuntimePackageRestore()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = CreateTestFolders();
            var repository = Path.Combine(workingDir, "repository");
            var projectDir = Path.Combine(workingDir, "project");
            var packagesDir = Path.Combine(workingDir, "packages");

            CreateBasicLibPackage(repository, "packageA");
            CreateRuntimesPackage(repository);

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""supports"": {
                    ""net46.app"": {},
                    ""uwp.10.0.app"": {},
                    ""dnxcore50.app"": {}
                },
                ""dependencies"": {
                    ""packageA"": ""1.0.0"",
                    ""runtimes"": ""1.0.0""
                },
                ""frameworks"": {
                    ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            // Assert
            Assert.True(result.Success);
        }

        private string CreateTestFolders()
        {
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);
            var globalDir = Path.Combine(workingDir, "globalPackages");
            Directory.CreateDirectory(globalDir);

            return workingDir;
        }

        private static FileInfo CreateRuntimesPackage(string repositoryDir)
        {
            return CreateRuntimesPackage(repositoryDir, "runtimes", GetRuntimeJson());
        }

        private static FileInfo CreateRuntimesPackage(string repositoryDir, string packageId, string runtimeJson)
        {
            var file = new FileInfo(Path.Combine(repositoryDir, packageId + ".1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("runtime.json", runtimeJson, Encoding.UTF8);

                zip.AddEntry(packageId + ".nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                        <id>" + packageId + @"</id>
                        <version>1.0.0</version>
                        <title />
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            return file;
        }

        private static FileInfo CreateBasicLibPackage(string repositoryDir, string packageId)
        {
            var file = new FileInfo(Path.Combine(repositoryDir, packageId + ".1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[] { 0 });
                zip.AddEntry("lib/uap10.0/a.dll", new byte[] { 0 });
                zip.AddEntry("lib/win8/a.dll", new byte[] { 0 });
                zip.AddEntry("lib/dotnet/a.dll", new byte[] { 0 });
                zip.AddEntry("lib/native/a.dll", new byte[] { 0 });

                zip.AddEntry(packageId + ".nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                        <id>" + packageId + @"</id>
                        <version>1.0.0</version>
                        <title />
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            return file;
        }

        private static string GetRuntimeJson()
        {
            return @"{
                ""supports"": {
                    ""uwp.10.0.app"": {
                            ""uap10.0"": [
                                ""win10-x86"",
                                ""win10-x86-aot"",
                                ""win10-x64"",
                                ""win10-x64-aot"",
                                ""win10-arm"",
                                ""win10-arm-aot""
                        ]
                    },
                    ""net46.app"": {
                        ""net46"": [
                            ""win-x86"",
                            ""win-x64""
                        ]
                    },
                    ""dnxcore50.app"": {
                        ""dnxcore50"": [
                            ""win7-x86"",
                            ""win7-x64""
                        ]
                    }
                }
            }";
        }

        private ConcurrentBag<string> _testFolders = new ConcurrentBag<string>();

        public void Dispose()
        {
            // Clean up
            foreach (var folder in _testFolders)
            {
                TestFileSystemUtility.DeleteRandomTestFolders(folder);
            }
        }
    }
}
