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
            request.MaxDegreeOfConcurrency = 1;
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
            request.MaxDegreeOfConcurrency = 1;
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
            request.MaxDegreeOfConcurrency = 1;
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
            request.MaxDegreeOfConcurrency = 1;
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
            request.MaxDegreeOfConcurrency = 1;
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
            request.MaxDegreeOfConcurrency = 1;
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
            request.MaxDegreeOfConcurrency = 1;
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
            request.MaxDegreeOfConcurrency = 1;
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
        public async Task RestoreCommand_UnmatchedRefAndLibAssemblies()
        {
            const string project = @"
{
    ""dependencies"": {
        ""System.Linq"": ""4.0.0-beta-*"",
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

            // TODO(anurse): We should be mocking this out or using a stable source...
            sources.Add(new PackageSource("https://www.myget.org/F/dotnet-core/api/v2/"));

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.MaxDegreeOfConcurrency = 1;
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
            Assert.True(brokenPackages.Length >= 3);
            Assert.True(brokenPackages.Any(c => c.Package.Id.Equals("System.Globalization") && c.AssemblyName.Equals("System.Globalization")));
            Assert.True(brokenPackages.Any(c => c.Package.Id.Equals("System.IO") && c.AssemblyName.Equals("System.IO")));
            Assert.True(brokenPackages.Any(c => c.Package.Id.Equals("System.Reflection") && c.AssemblyName.Equals("System.Reflection")));
        }

        [Fact]
        public async Task RestoreCommand_LockedLockFile()
        {
            const string project = @"
{
    ""dependencies"": {
        ""System.Runtime"": ""4.0.10-beta-*""
    },
    ""frameworks"": {
        ""dotnet"": { }
    }
}";

            const string lockFileContent = @"{
  ""locked"": true,
  ""version"": -9996,
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

            // TODO(anurse): We should be mocking this out or using a stable source...
            sources.Add(new PackageSource("https://www.myget.org/F/dotnet-core/api/v2/"));

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Parse(lockFileContent);
            Assert.True(lockFile.IsLocked); // Just to make sure no-one accidentally unlocks it :)

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.MaxDegreeOfConcurrency = 1;
            request.ExistingLockFile = lockFile;

            // Act
            var logger = new TestLogger();
            var command = new RestoreCommand(logger, request);
            var result = await command.ExecuteAsync();
            var installed = result.GetAllInstalled();

            // Assert
            Assert.Equal(1, installed.Count);
            Assert.Equal("System.Runtime", installed.Single().Name);
            Assert.Equal(NuGetVersion.Parse("4.0.10-beta-23008"), installed.Single().Version);
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
  ""version"": -9996,
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

            // TODO(anurse): We should be mocking this out or using a stable source...
            sources.Add(new PackageSource("https://www.myget.org/F/dotnet-core/api/v2/"));

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
            var projectDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(packagesDir);
            _testFolders.Add(projectDir);

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Parse(lockFileContent);
            Assert.True(lockFile.IsLocked); // Just to make sure no-one accidentally unlocks it :)

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.MaxDegreeOfConcurrency = 1;
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
            return targets.Where(target => target.TargetFramework.Equals(NuGetFramework.Parse(framework)))
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
