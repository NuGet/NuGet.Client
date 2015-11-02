using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class RestoreCommandTests : IDisposable
    {
        private ConcurrentBag<string> _testFolders = new ConcurrentBag<string>();

        [Fact]
        public async Task RestoreCommand_FrameworkImportRulesAreApplied()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"
            {
                ""dependencies"": {
                    ""Newtonsoft.Json"": ""7.0.1""
                },
                ""frameworks"": {
                    ""dotnet"": {
                        ""imports"": ""portable-net452+win81"",
                        ""warn"": false
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
            var framework = new FallbackFramework(NuGetFramework.Parse("dotnet"), NuGetFramework.Parse("portable-net452+win81"));

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);
            var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, framework, null);
            var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(1, result.GetAllInstalled().Count);
            Assert.Equal("Newtonsoft.Json", result.GetAllInstalled().Single().Name);
            Assert.Equal("7.0.1", result.GetAllInstalled().Single().Version.ToNormalizedString());
            Assert.Equal(1, runtimeAssemblies.Count);
            Assert.Equal("lib/portable-net45+wp80+win8+wpa81+dnxcore50/Newtonsoft.Json.dll", runtimeAssembly.Path);
        }

        [Fact]
        public async Task RestoreCommand_FrameworkImportRulesAreApplied_Noop()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"
            {
                ""dependencies"": {
                    ""Newtonsoft.Json"": ""7.0.1""
                },
                ""frameworks"": {
                    ""dotnet"": {
                        ""imports"": ""portable-net452+win81"",
                        ""warn"": false
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
            var framework = new FallbackFramework(NuGetFramework.Parse("dotnet"), NuGetFramework.Parse("portable-net452+win81"));
            var result = await command.ExecuteAsync();
            result.Commit(logger);
            logger.Clear();

            // Act
            request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");
            request.ExistingLockFile = result.LockFile;
            command = new RestoreCommand(logger, request);
            result = await command.ExecuteAsync();
            result.Commit(logger);

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(0, result.GetAllInstalled().Count);
        }

        [Fact]
        public async Task RestoreCommand_LeftOverNupkg_Overwritten()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"
            {
                ""dependencies"": {
                    ""Newtonsoft.Json"": ""7.0.1""
                },
                ""frameworks"": {
                    ""dotnet"": {
                        ""imports"": ""portable-net452+win81"",
                        ""warn"": false
                    }
                }
            }");

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);
            var logger = new TestLogger();

            // Create left over nupkg to simulate a corrupted install
            var nupkgFolder = Path.Combine(packagesDir, "NewtonSoft.json", "7.0.1");
            var nupkgPath = Path.Combine(nupkgFolder, "Newtonsoft.Json.7.0.1.nupkg");

            Directory.CreateDirectory(nupkgFolder);

            using (File.Create(nupkgPath))
            {
            }

            Assert.True(File.Exists(nupkgPath));

            var fileSize = new FileInfo(nupkgPath).Length;

            Assert.True(fileSize == 0, "Dummy nupkg file bigger than expected");

            // create the request
            var request = new RestoreRequest(spec, sources, packagesDir);
            
            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();

            // Assert
            var newFileSize = new FileInfo(nupkgPath).Length;

            Assert.True(newFileSize > 0, "Downloaded file not overriding the dummy nupkg");
        }

        [Fact]
        public async Task RestoreCommand_FrameworkImport_WarnOn()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"
            {
                ""dependencies"": {
                    ""Newtonsoft.Json"": ""7.0.1""
                },
                ""frameworks"": {
                    ""dotnet"": {
                        ""imports"": ""portable-net452+win81"",
                        ""warn"": true
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
            var framework = new FallbackFramework(NuGetFramework.Parse("dotnet"), NuGetFramework.Parse("portable-net452+win81"));
            var warning = "Package 'Newtonsoft.Json 7.0.1' was restored using '.NETPortable,Version=v0.0,Profile=net452+win81' instead the project target framework '.NETPlatform,Version=v5.0'. This may cause compatibility problems.";

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);
            var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, framework, null);
            var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

            // Assert
            Assert.Equal(1, result.GetAllInstalled().Count);
            Assert.Equal(0, logger.Errors);
            Assert.Equal(1, logger.Warnings);
            Assert.Equal(1, logger.Messages.Where(message => message.Equals(warning)).Count());
        }

        [Fact]
        public async Task RestoreCommand_FollowFallbackDependencies()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"
            {
                ""dependencies"": {
                    ""WindowsAzure.Storage"": ""4.4.1-preview""
                },
                ""frameworks"": {
                    ""dotnet"": {
                        ""imports"": ""portable-net452+win81"",
                        ""warn"": false
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
            var framework = new FallbackFramework(NuGetFramework.Parse("dotnet"), NuGetFramework.Parse("portable-net452+win81"));

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);
            var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, framework, null);
            var runtimeAssembly = runtimeAssemblies.FirstOrDefault();
            var dependencies = string.Join("|", result.GetAllInstalled().Select(dependency => dependency.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));

            // Assert
            Assert.Equal(4, result.GetAllInstalled().Count);
            Assert.Equal("Microsoft.Data.Edm|Microsoft.Data.OData|System.Spatial|WindowsAzure.Storage", dependencies);
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
        }

        [Fact]
        public async Task RestoreCommand_FrameworkImportValidateLockFile()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var configJson = JObject.Parse(@"
            {
                ""dependencies"": {
                    ""Newtonsoft.Json"": ""7.0.1""
                },
                ""frameworks"": {
                    ""dotnet"": {
                        ""imports"": ""portable-net452+win81"",
                        ""warn"": false
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
            var framework = new FallbackFramework(NuGetFramework.Parse("dotnet"), NuGetFramework.Parse("portable-net452+win81"));
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            // Act
            var valid = result.LockFile.IsValidForPackageSpec(spec);

            // Assert
            Assert.True(valid);
        }

        [Fact]
        public async Task RestoreCommand_DependenciesDifferOnCase()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var json = new JObject();

            var frameworks = new JObject();
            frameworks["net46"] = new JObject();

            json["dependencies"] = new JObject();

            json["frameworks"] = frameworks;

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(json.ToString(), "TestProject", specPath);

            AddDependency(spec, "nEwTonSoft.JSon", "6.0.8");
            AddDependency(spec, "json-ld.net", "1.0.4");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);
            var assemblies = GetRuntimeAssemblies(result.LockFile.Targets, "net46", null);

            // Build again to verify the noop works also
            result = await command.ExecuteAsync();
            result.Commit(logger);
            var assemblies2 = GetRuntimeAssemblies(result.LockFile.Targets, "net46", null);

            // Assert
            Assert.Equal(2, assemblies.Count);
            Assert.Equal("lib/net45/Newtonsoft.Json.dll", assemblies[1].Path);
            Assert.Equal(2, assemblies2.Count);
            Assert.Equal("lib/net45/Newtonsoft.Json.dll", assemblies2[1].Path);
        }

        [Fact]
        public async Task RestoreCommand_DependenciesDifferOnCase_Downgrade()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var json = new JObject();

            var frameworks = new JObject();
            frameworks["net46"] = new JObject();

            json["dependencies"] = new JObject();

            json["frameworks"] = frameworks;

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(json.ToString(), "TestProject", specPath);

            AddDependency(spec, "nEwTonSoft.JSon", "4.0.1");
            AddDependency(spec, "dotNetRDF", "1.0.8.3533");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);
            var assemblies = GetRuntimeAssemblies(result.LockFile.Targets, "net46", null);

            // Assert
            Assert.Equal(4, assemblies.Count);
            Assert.Equal("lib/40/Newtonsoft.Json.dll", assemblies[2].Path);
        }

        [Fact]
        public async Task RestoreCommand_TestLockFileWrittenOnLockFileChange()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

            AddDependency(spec, "NuGet.Versioning", "1.0.7");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var lockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lastDate = File.GetLastWriteTimeUtc(lockFilePath);

            // wait half a second to make sure the time difference can be picked up
            await Task.Delay(500);

            request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var previousLockFile = result.LockFile;

            // Act

            // Modify the previous lock file so that they are not equal
            previousLockFile.Version = 1000;

            request.ExistingLockFile = previousLockFile;

            command = new RestoreCommand(logger, request);
            result = await command.ExecuteAsync();
            result.Commit(logger);

            var currentDate = File.GetLastWriteTimeUtc(lockFilePath);

            // Assert
            // The file should be written out
            Assert.NotEqual(lastDate, currentDate);
        }

        [Fact]
        public async Task RestoreCommand_WriteLockFileOnForce()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

            AddDependency(spec, "NuGet.Versioning", "1.0.7");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var lockFilePath = Path.Combine(projectDir, "project.lock.json");

            // Act
            var lastDate = File.GetLastWriteTime(lockFilePath);

            request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");
            var previousLockFile = result.LockFile;
            request.ExistingLockFile = result.LockFile;

            command = new RestoreCommand(logger, request);
            result = await command.ExecuteAsync();

            // wait half a second to make sure the time difference can be picked up
            await Task.Delay(500);

            result.Commit(logger, true);

            var currentDate = File.GetLastWriteTime(lockFilePath);

            // Assert
            // The file should be written out
            Assert.NotEqual(lastDate, currentDate);
        }

        [Fact]
        public async Task RestoreCommand_NoopOnLockFileWriteIfFilesMatch()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

            AddDependency(spec, "NuGet.Versioning", "1.0.7");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var lockFilePath = Path.Combine(projectDir, "project.lock.json");

            // Act
            var lastDate = File.GetLastWriteTime(lockFilePath);

            request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");
            var previousLockFile = result.LockFile;
            request.ExistingLockFile = result.LockFile;

            // Act 2 
            // Read the file from disk to verify the reader
            var fromDisk = lockFileFormat.Read(lockFilePath);

            // wait half a second to make sure the time difference can be picked up
            await Task.Delay(500);

            command = new RestoreCommand(logger, request);
            result = await command.ExecuteAsync();
            result.Commit(logger);

            var currentDate = File.GetLastWriteTime(lockFilePath);

            // Assert
            // The file should not be written out
            Assert.Equal(lastDate, currentDate);

            // Verify the files are equal
            Assert.True(previousLockFile.Equals(result.LockFile));
            Assert.True(fromDisk.Equals(result.LockFile));

            // Verify the hash codes are the same
            Assert.Equal(previousLockFile.GetHashCode(), result.LockFile.GetHashCode());
            Assert.Equal(fromDisk.GetHashCode(), result.LockFile.GetHashCode());
        }

        [Fact]
        public async Task RestoreCommand_NuGetVersioning107RuntimeAssemblies()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

            AddDependency(spec, "NuGet.Versioning", "1.0.7");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();
            var unresolved = result.GetAllUnresolved();
            var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "netcore50", null);

            var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

            // Assert
            Assert.Equal(0, logger.Errors);
            Assert.Equal(1, installed.Count);
            Assert.Equal(0, unresolved.Count);
            Assert.Equal("NuGet.Versioning", installed.Single().Name);
            Assert.Equal("1.0.7", installed.Single().Version.ToNormalizedString());

            Assert.Equal(1, runtimeAssemblies.Count);
            Assert.Equal("lib/portable-net40+win/NuGet.Versioning.dll", runtimeAssembly.Path);
        }

        [Fact]
        public async Task RestoreCommand_InstallPackageWithDependencies()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

            AddDependency(spec, "WebGrease", "1.6.0");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();
            var unresolved = result.GetAllUnresolved();
            var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "netcore50", null);
            var jsonNetReference = runtimeAssemblies.SingleOrDefault(assembly => assembly.Path == "lib/netcore45/Newtonsoft.Json.dll");
            var jsonNetPackage = installed.SingleOrDefault(package => package.Name == "Newtonsoft.Json");

            // Assert
            // There will be compatibility errors, but we don't care
            Assert.Equal(3, installed.Count);
            Assert.Equal(0, unresolved.Count);
            Assert.Equal("5.0.4", jsonNetPackage.Version.ToNormalizedString());

            Assert.Equal(1, runtimeAssemblies.Count);
            Assert.NotNull(jsonNetReference);
        }

        [Fact]
        public async Task RestoreCommand_InstallPackageWithReferenceDependencies()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfigWithNet46.ToString(), "TestProject", specPath);

            AddDependency(spec, "Moon.Owin.Localization", "1.3.1");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();
            var unresolved = result.GetAllUnresolved();
            var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "net46", null);
            var jsonNetReference = runtimeAssemblies.SingleOrDefault(assembly => assembly.Path == "lib/net45/Newtonsoft.Json.dll");
            var jsonNetPackage = installed.SingleOrDefault(package => package.Name == "Newtonsoft.Json");

            // Assert
            // There will be compatibility errors, but we don't care
            Assert.Equal(23, installed.Count);
            Assert.Equal(0, unresolved.Count);
            Assert.Equal("7.0.1", jsonNetPackage.Version.ToNormalizedString());

            Assert.Equal(22, runtimeAssemblies.Count);
            Assert.NotNull(jsonNetReference);
        }

        [Fact]
        public async Task RestoreCommand_RestoreWithNoChanges()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

            AddDependency(spec, "NuGet.Versioning", "1.0.7");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var firstRun = await command.ExecuteAsync();

            // Act
            command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();
            var unresolved = result.GetAllUnresolved();

            // Assert
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, installed.Count);
            Assert.Equal(0, unresolved.Count);
        }

        [Theory]
        [InlineData("https://www.nuget.org/api/v2/")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task RestoreCommand_PackageIsAddedToPackageCache(string source)
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(source));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

            AddDependency(spec, "NuGet.Versioning", "1.0.7");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var nuspecPath = Path.Combine(packagesDir, "NuGet.Versioning", "1.0.7", "NuGet.Versioning.nuspec");

            // Assert
            Assert.True(File.Exists(nuspecPath));
        }


        [Theory]
        [InlineData("https://www.nuget.org/api/v2/")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task RestoreCommand_PackagesAreExtractedToTheNormalizedPath(string source)
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(source));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

            AddDependency(spec, "owin", "1.0");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var nuspecPath = Path.Combine(packagesDir, "owin", "1.0.0", "owin.nuspec");

            // Assert
            Assert.True(File.Exists(nuspecPath));
        }

        [Fact]
        public async Task RestoreCommand_WarnWhenWeBumpYouUp()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

            AddDependency(spec, "Newtonsoft.Json", "7.0.0"); // 7.0.0 does not exist so we'll bump up to 7.0.1

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();
            var unresolved = result.GetAllUnresolved();
            var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "netcore50", null);

            var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

            // Assert
            Assert.Equal(3, logger.Warnings); // We'll get the warning for each runtime and for the runtime-less restore.
            Assert.Contains("Dependency specified was Newtonsoft.Json (≥ 7.0.0) but ended up with Newtonsoft.Json 7.0.1.", logger.Messages);
        }

        [Fact]
        public async Task RestoreCommand_JsonNet701RuntimeAssemblies()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

            AddDependency(spec, "Newtonsoft.Json", "7.0.1");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();
            var unresolved = result.GetAllUnresolved();
            var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "netcore50", null);

            var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

            // Assert
            Assert.Equal(0, logger.Errors);
            Assert.Equal(1, installed.Count);
            Assert.Equal(0, unresolved.Count);
            Assert.Equal("Newtonsoft.Json", installed.Single().Name);
            Assert.Equal("7.0.1", installed.Single().Version.ToNormalizedString());

            Assert.Equal(1, runtimeAssemblies.Count);
            Assert.Equal("lib/portable-net45+wp80+win8+wpa81+dnxcore50/Newtonsoft.Json.dll", runtimeAssembly.Path);
        }

        [Fact]
        public async Task RestoreCommand_NoCompatibleRuntimeAssembliesForProject()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

            AddDependency(spec, "NuGet.Core", "2.8.3");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();
            var unresolved = result.GetAllUnresolved();
            var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "netcore50", null);

            var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

            // Assert
            var expectedIssue = CompatibilityIssue.Incompatible(
                new PackageIdentity("NuGet.Core", new NuGetVersion(2, 8, 3)), FrameworkConstants.CommonFrameworks.NetCore50, null);
            Assert.Contains(expectedIssue, result.CompatibilityCheckResults.SelectMany(c => c.Issues).ToArray());
            Assert.False(result.CompatibilityCheckResults.Any(c => c.Success));
            Assert.Contains(expectedIssue.Format(), logger.Messages);

            Assert.Equal(9, logger.Errors);
            Assert.Equal(2, installed.Count);
            Assert.Equal(0, unresolved.Count);
            Assert.Equal(0, runtimeAssemblies.Count);
        }

        [Fact]
        public async Task RestoreCommand_CorrectlyIdentifiesUnresolvedPackages()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

            AddDependency(spec, "NotARealPackage.ThisShouldNotExists.DontCreateIt.Seriously.JustDontDoIt.Please", "2.8.3");

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();
            var unresolved = result.GetAllUnresolved();

            // Assert
            Assert.False(result.Success);

            Assert.Equal(1, logger.Errors);
            Assert.Empty(result.CompatibilityCheckResults);
            Assert.DoesNotContain("compatible with", logger.Messages);
            Assert.Equal(1, unresolved.Count);
            Assert.Equal(0, installed.Count);
        }

        [Fact]
        public async Task RestoreCommand_PopulatesProjectFileDependencyGroupsCorrectly()
        {
            const string project = @"{
    ""dependencies"": {
        ""Newtonsoft.Json"": ""6.0.4""
    },
    ""frameworks"": {
        ""net45"": {}
    },
    ""supports"": {
    }
}
";
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));
            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();
            var unresolved = result.GetAllUnresolved();

            // Assert
            Assert.Equal(2, result.LockFile.ProjectFileDependencyGroups.Count);
            Assert.True(string.IsNullOrEmpty(result.LockFile.ProjectFileDependencyGroups[0].FrameworkName));
            Assert.Equal(new[] { "Newtonsoft.Json >= 6.0.4" }, result.LockFile.ProjectFileDependencyGroups[0].Dependencies.ToArray());
            Assert.Equal(".NETFramework,Version=v4.5", result.LockFile.ProjectFileDependencyGroups[1].FrameworkName);
            Assert.Empty(result.LockFile.ProjectFileDependencyGroups[1].Dependencies);
        }

        [Fact]
        public async Task RestoreCommand_CanInstallPackageWithSatelliteAssemblies()
        {
            const string project = @"
{
    ""dependencies"": {
        ""Microsoft.OData.Client"": ""6.12.0"",
    },
    ""frameworks"": {
        ""net40"": {}
    }
}
";

            // Arrange
            var sources = new List<PackageSource>();

            sources.Add(new PackageSource("https://www.nuget.org/api/v2"));

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task RestoreCommand_UnmatchedRefAndLibAssemblies()
        {
            const string project = @"
{
    ""dependencies"": {
        ""System.Runtime.WindowsRuntime"": ""4.0.11-beta-*"",
        ""Microsoft.NETCore.Targets"": ""1.0.0-beta-*""
    },
    ""frameworks"": {
        ""dotnet"": {}
    },
    ""supports"": {
        ""dnxcore50.app"": {}
    }
}
";

            // Arrange
            var sources = new List<PackageSource>();

            sources.Add(new PackageSource("https://nuget.org/api/v2/"));

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var lockFileFormat = new LockFileFormat();

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();
            var unresolved = result.GetAllUnresolved();
            var brokenPackages = result.CompatibilityCheckResults.FirstOrDefault(c =>
                c.Graph.Framework == FrameworkConstants.CommonFrameworks.DnxCore50 &&
                !string.IsNullOrEmpty(c.Graph.RuntimeIdentifier)).Issues.Where(c => c.Type == CompatibilityIssueType.ReferenceAssemblyNotImplemented).ToArray();

            // Assert
            Assert.True(brokenPackages.Length >= 1);
            Assert.True(brokenPackages.Any(c => c.Package.Id.Equals("System.Runtime.WindowsRuntime") && c.AssemblyName.Equals("System.Runtime.WindowsRuntime")));
        }

        [Fact]
        public async Task RestoreCommand_LockedLockFile()
        {
            const string project = @"
{
    ""dependencies"": {
        ""System.Runtime"": ""4.0.10-beta-23019""
    },
    ""frameworks"": {
        ""dotnet"": { }
    }
}";

            const string lockFileContent = @"{
  ""locked"": true,
  ""version"": 1,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.10-beta-23019"": {
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.10-beta-23019"": {
      ""sha512"": ""JkGp8sCzxxRY1GS+p1SEk8WcaT8pu++/5b94ar2i/RaUN/OzkcGP/6OLFUxUf1uar75pUvotpiMawVt1dCEUVA=="",
      ""type"": ""Package"",
      ""files"": [
        ""_rels/.rels"",
        ""System.Runtime.nuspec"",
        ""License.rtf"",
        ""ref/dotnet/System.Runtime.dll"",
        ""ref/net451/_._"",
        ""lib/net451/_._"",
        ""ref/win81/_._"",
        ""lib/win81/_._"",
        ""ref/netcore50/System.Runtime.dll"",
        ""package/services/metadata/core-properties/cdec43993f064447a2d882cbfd022539.psmdcp"",
        ""[Content_Types].xml""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime >= 4.0.10-beta-23019""
    ],
    "".NETPlatform,Version=v5.0"": []
  }
}
";

            // Arrange
            var sources = new List<PackageSource>();

            // TODO(anurse): We should be mocking this out or using a stable source...
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Parse(lockFileContent, "In Memory");
            Assert.True(lockFile.IsLocked); // Just to make sure no-one accidentally unlocks it :)

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.ExistingLockFile = lockFile;

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();

            // Assert
            Assert.Equal(1, installed.Count);
            Assert.Equal("System.Runtime", installed.Single().Name);
            Assert.Equal(NuGetVersion.Parse("4.0.10-beta-23019"), installed.Single().Version);
        }

        [Fact]
        public async Task RestoreCommand_LockedLockFileWithOutOfDateProject()
        {
            const string project = @"
{
    ""dependencies"": {
        ""System.Runtime"": ""4.0.20-beta-*""
    },
    ""frameworks"": {
        ""dotnet"": { }
    }
}";

            const string lockFileContent = @"{
  ""locked"": true,
  ""version"": 1,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.10-beta-23008"": {
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.10-beta-23008"": {
      ""sha512"": ""JkGp8sCzxxRY1GS+p1SEk8WcaT8pu++/5b94ar2i/RaUN/OzkcGP/6OLFUxUf1uar75pUvotpiMawVt1dCEUVA=="",
      ""type"": ""Package"",
      ""files"": [
        ""_rels/.rels"",
        ""System.Runtime.nuspec"",
        ""License.rtf"",
        ""ref/dotnet/System.Runtime.dll"",
        ""ref/net451/_._"",
        ""lib/net451/_._"",
        ""ref/win81/_._"",
        ""lib/win81/_._"",
        ""ref/netcore50/System.Runtime.dll"",
        ""package/services/metadata/core-properties/cdec43993f064447a2d882cbfd022539.psmdcp"",
        ""[Content_Types].xml""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime >= 4.0.10-beta-*""
    ],
    "".NETPlatform,Version=v5.0"": []
  }
}
";

            // Arrange
            var sources = new List<PackageSource>();

            sources.Add(new PackageSource("https://nuget.org/api/v2/"));

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Parse(lockFileContent, "In Memory");
            Assert.True(lockFile.IsLocked); // Just to make sure no-one accidentally unlocks it :)

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.ExistingLockFile = lockFile;

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();

            // Assert
            Assert.Equal(1, installed.Count);
            Assert.Equal("System.Runtime", installed.Single().Name);
            Assert.Equal(4, installed.Single().Version.Major);
            Assert.Equal(0, installed.Single().Version.Minor);
            Assert.Equal(20, installed.Single().Version.Patch);
            // Don't assert the pre-release tag since it may vary
        }

        private static List<LockFileItem> GetRuntimeAssemblies(IList<LockFileTarget> targets, string framework, string runtime)
        {
            return GetRuntimeAssemblies(targets, NuGetFramework.Parse(framework), runtime);
        }

        private static List<LockFileItem> GetRuntimeAssemblies(IList<LockFileTarget> targets, NuGetFramework framework, string runtime)
        {
            return targets.Where(target => target.TargetFramework.Equals(framework))
                .Where(target => target.RuntimeIdentifier == runtime)
                .SelectMany(target => target.Libraries)
                .SelectMany(library => library.RuntimeAssemblies)
                .ToList();
        }

        private static void AddRuntime(PackageSpec spec, string rid)
        {
            spec.RuntimeGraph = RuntimeGraph.Merge(
                spec.RuntimeGraph,
                new RuntimeGraph(new[]
                {
                    new RuntimeDescription(rid)
                }));
        }

        private static void AddDependency(PackageSpec spec, string id, string version)
        {
            var target = new LibraryDependency()
            {
                LibraryRange = new LibraryRange()
                {
                    Name = id,
                    VersionRange = VersionRange.Parse(version)
                }
            };

            if (spec.Dependencies == null)
            {
                spec.Dependencies = new List<LibraryDependency>();
            }

            spec.Dependencies.Add(target);
        }

        private static JObject BasicConfig
        {
            get
            {
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["netcore50"] = new JObject();

                json["dependencies"] = new JObject();

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
        }

        private static JObject BasicConfigWithNet46
        {
            get
            {
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["net46"] = new JObject();

                json["dependencies"] = new JObject();

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
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
