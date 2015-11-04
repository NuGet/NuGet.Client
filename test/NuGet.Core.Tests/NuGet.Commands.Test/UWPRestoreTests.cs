using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class UWPRestoreTests : IDisposable
    {
        private ConcurrentBag<string> _testFolders = new ConcurrentBag<string>();

        [Fact]
        public async Task UWPRestore_VerifySatellitePackagesAreCompatibleInPCL()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://api.nuget.org/v3/index.json"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"{
                  ""dependencies"": {
                    ""Microsoft.AspNet.Mvc.de"": ""5.2.3""
                  },
                  ""frameworks"": {
                    ""net46"": {
                    }
                  }
                }");

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(5, result.GetAllInstalled().Count);
        }

        // Verify that UWP packages are still compatible after excluding their contents.
        [Fact]
        public async Task UWPRestore_BlankUWPAppWithExcludes()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://api.nuget.org/v3/index.json"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"{
                  ""dependencies"": {
                    ""Microsoft.NETCore.UniversalWindowsPlatform"": {
                        ""version"": ""5.0.0"",
                        ""exclude"": ""build,runtime,compile,native""
                     }
                  },
                  ""frameworks"": {
                    ""uap10.0"": {}
                  },
                  ""runtimes"": {
                    ""win10-arm"": {},
                    ""win10-arm-aot"": {},
                    ""win10-x86"": {},
                    ""win10-x86-aot"": {},
                    ""win10-x64"": {},
                    ""win10-x64-aot"": {}
                  }
                }");

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var lockFileJson = JObject.Parse(File.OpenText(request.LockFilePath).ReadToEnd());

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(118, result.GetAllInstalled().Count);
        }

        [Fact]
        public async Task UWPRestore_VerifySameResultWhenRestoringWithLocalPackages()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://api.nuget.org/v3/index.json"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"{
                ""runtimes"": {
                    ""win7-x86"": { }
                    },
                ""frameworks"": {
                ""dnxcore50"": {
                    ""dependencies"": {
                    ""Microsoft.NETCore"": ""5.0.1-beta-23225""
                    },
                    ""imports"": ""portable-net451+win81""
                }
                }
            }");

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            var result2 = await command.ExecuteAsync();

            // Assert
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(result.LockFile, result2.LockFile);
        }

        [Fact]
        public async Task UWPRestore_SystemDependencyVersionConflict()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://api.nuget.org/v3/index.json"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"{
                  ""dependencies"": {
                    ""System.Text.Encoding"": ""4.0.10"",
                    ""System.Collections"": ""4.0.11-beta-23225""
                  },
                  ""frameworks"": {
                    ""uap10.0"": {}
                  }
                }");

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var lockFileJson = JObject.Parse(File.OpenText(request.LockFilePath).ReadToEnd());

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
        }

        // Verify that File > New Project > Blank UWP App can restore without errors or warnings.
        [Fact]
        public async Task UWPRestore_BlankUWPApp()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://api.nuget.org/v3/index.json"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"{
                  ""dependencies"": {
                    ""Microsoft.NETCore.UniversalWindowsPlatform"": ""5.0.0""
                  },
                  ""frameworks"": {
                    ""uap10.0"": {}
                  },
                  ""runtimes"": {
                    ""win10-arm"": {},
                    ""win10-arm-aot"": {},
                    ""win10-x86"": {},
                    ""win10-x86-aot"": {},
                    ""win10-x64"": {},
                    ""win10-x64-aot"": {}
                  }
                }");

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);

#if !DNXCORE50
            var expectedStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("NuGet.Commands.Test.compiler.resources.uwpBlankApp.json");

            JObject expectedJson = null;

            using (var reader = new StreamReader(expectedStream))
            {
                expectedJson = JObject.Parse(reader.ReadToEnd());
            }
#endif

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var lockFileJson = JObject.Parse(File.OpenText(request.LockFilePath).ReadToEnd());

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(118, result.GetAllInstalled().Count);

#if !DNXCORE50
            Assert.Equal(expectedJson.ToString(), lockFileJson.ToString());
#endif
        }

        // Verify that File > New Project > Class Library (Portable) can restore without errors or warnings.
        [Fact]
        public async Task UWPRestore_ModernPCL()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://api.nuget.org/v3/index.json"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"{
                  ""supports"": {
                    ""net46.app"": { },
                    ""uwp.10.0.app"": { },
                    ""dnxcore50.app"": { }
                        },
                  ""dependencies"": {
                    ""Microsoft.NETCore"": ""5.0.0"",
                    ""Microsoft.NETCore.Portable.Compatibility"": ""1.0.0""
                  },
                  ""frameworks"": {
                    ""dotnet"": {
                      ""imports"": ""portable-net452+win81""
                    }
                  }
                }");

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(86, result.GetAllInstalled().Count);
        }

        // Verify that installing all Office 365 services into a UWP app restores without errors.
        [Fact]
        public async Task UWPRestore_UWPAppWithOffice365Packages()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://api.nuget.org/v3/index.json"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"{
                  ""dependencies"": {
                    ""Microsoft.ApplicationInsights"": ""1.0.0"",
                    ""Microsoft.ApplicationInsights.PersistenceChannel"": ""1.0.0"",
                    ""Microsoft.ApplicationInsights.WindowsApps"": ""1.0.0"",
                    ""Microsoft.Azure.ActiveDirectory.GraphClient"": ""2.0.6"",
                    ""Microsoft.IdentityModel.Clients.ActiveDirectory"": ""2.14.201151115"",
                    ""Microsoft.NETCore.UniversalWindowsPlatform"": ""5.0.0"",
                    ""Microsoft.Office365.Discovery"": ""1.0.22"",
                    ""Microsoft.Office365.OutlookServices"": ""1.0.35"",
                    ""Microsoft.Office365.SharePoint"": ""1.0.22""
                  },
                  ""frameworks"": {
                    ""uap10.0"": {}
                  },
                  ""runtimes"": {
                    ""win10-arm"": {},
                    ""win10-arm-aot"": {},
                    ""win10-x86"": {},
                    ""win10-x86-aot"": {},
                    ""win10-x64"": {},
                    ""win10-x64-aot"": {}
                  }
                }");

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(140, result.GetAllInstalled().Count);
        }

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
