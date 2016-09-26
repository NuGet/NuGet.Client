using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class DependencyGraphSpecTests
    {
        [Fact]
        public void DependencyGraphSpec_ReadFileWithProjects_GetClosures()
        {
            // Arrange
            var json = JObject.Parse(ResourceTestUtility.GetResource("NuGet.ProjectModel.Test.compiler.resources.test1.dg", typeof(DependencyGraphSpecTests)));

            // Act
            var dg = DependencyGraphSpec.Load(json);

            var xClosure = dg.GetClosure("A55205E7-4D08-4672-8011-0925467CC45F").ToList();
            var yClosure = dg.GetClosure("78A6AD3F-9FA5-47F6-A54E-84B46A48CB2F").ToList();
            var zClosure = dg.GetClosure("44B29B8D-8413-42D2-8DF4-72225659619B").ToList();

            // Assert
            Assert.Equal(3, xClosure.Count);
            Assert.Equal("A55205E7-4D08-4672-8011-0925467CC45F", xClosure[0].RestoreMetadata.ProjectUniqueName);
            Assert.Equal("78A6AD3F-9FA5-47F6-A54E-84B46A48CB2F", xClosure[1].RestoreMetadata.ProjectUniqueName);
            Assert.Equal("44B29B8D-8413-42D2-8DF4-72225659619B", xClosure[2].RestoreMetadata.ProjectUniqueName);

            Assert.Equal(1, yClosure.Count);
            Assert.Equal("78A6AD3F-9FA5-47F6-A54E-84B46A48CB2F", yClosure.Single().RestoreMetadata.ProjectUniqueName);

            Assert.Equal(1, zClosure.Count);
            Assert.Equal("44B29B8D-8413-42D2-8DF4-72225659619B", zClosure.Single().RestoreMetadata.ProjectUniqueName);
        }

        [Fact]
        public void DependencyGraphSpec_ReadEmptyJObject()
        {
            // Arrange
            var json = new JObject();

            // Act
            var dg = new DependencyGraphSpec(json);

            // Assert
            Assert.Equal(json, dg.Json);
            Assert.Equal(0, dg.Restore.Count);
            Assert.Equal(0, dg.Projects.Count);
        }

        [Fact]
        public void DependencyGraphSpec_ReadEmpty()
        {
            // Arrange && Act
            var dg = new DependencyGraphSpec();

            // Assert
            Assert.Equal(0, dg.Json.Properties().Count());
            Assert.Equal(0, dg.Restore.Count);
            Assert.Equal(0, dg.Projects.Count);
        }

        [Fact]
        public void DependencyGraphSpec_ReadMSBuildMetadata()
        {
            // Arrange
            var json = ResourceTestUtility.GetResource("NuGet.ProjectModel.Test.compiler.resources.project1.json", typeof(DependencyGraphSpecTests));

            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "x", "c:\\fake\\project.json");
            var msbuildMetadata = spec.RestoreMetadata;

            // Assert
            Assert.NotNull(msbuildMetadata);
            Assert.Equal("A55205E7-4D08-4672-8011-0925467CC45F", msbuildMetadata.ProjectUniqueName);
            Assert.Equal("c:\\x\\x.csproj", msbuildMetadata.ProjectPath);
            Assert.Equal("x", msbuildMetadata.ProjectName);
            Assert.Equal("c:\\x\\project.json", msbuildMetadata.ProjectJsonPath);
            Assert.Equal(RestoreOutputType.NETCore, msbuildMetadata.OutputType);
            Assert.Equal("c:\\packages", msbuildMetadata.PackagesPath);
            Assert.Equal("https://api.nuget.org/v3/index.json", string.Join("|", msbuildMetadata.Sources.Select(s => s.Source)));
            Assert.Equal("c:\\fallback1|c:\\fallback2", string.Join("|", msbuildMetadata.FallbackFolders));
            Assert.Equal("44B29B8D-8413-42D2-8DF4-72225659619B|c:\\a\\a.csproj|78A6AD3F-9FA5-47F6-A54E-84B46A48CB2F|c:\\b\\b.csproj", string.Join("|", msbuildMetadata.ProjectReferences.Select(e => $"{e.ProjectUniqueName}|{e.ProjectPath}")));
        }

        [Fact]
        public void DependencyGraphSpec_VerifyMSBuildMetadataObject()
        {
            // Arrange && Act
            var msbuildMetadata = new ProjectRestoreMetadata();

            msbuildMetadata.ProjectUniqueName = "A55205E7-4D08-4672-8011-0925467CC45F";
            msbuildMetadata.ProjectPath = "c:\\x\\x.csproj";
            msbuildMetadata.ProjectName = "x";
            msbuildMetadata.ProjectJsonPath = "c:\\x\\project.json";
            msbuildMetadata.OutputType = RestoreOutputType.NETCore;
            msbuildMetadata.PackagesPath = "c:\\packages";
            msbuildMetadata.Sources = new[] { new PackageSource("https://api.nuget.org/v3/index.json") };
            msbuildMetadata.ProjectReferences.Add(new ProjectRestoreReference()
            {
                ProjectUniqueName = "44B29B8D-8413-42D2-8DF4-72225659619B",
                ProjectPath = "c:\\a\\a.csproj"
            });

            msbuildMetadata.ProjectReferences.Add(new ProjectRestoreReference()
            {
                ProjectUniqueName = "78A6AD3F-9FA5-47F6-A54E-84B46A48CB2F",
                ProjectPath = "c:\\b\\b.csproj"
            });

            msbuildMetadata.FallbackFolders.Add("c:\\fallback1");
            msbuildMetadata.FallbackFolders.Add("c:\\fallback2");

            // Assert
            Assert.NotNull(msbuildMetadata);
            Assert.Equal("A55205E7-4D08-4672-8011-0925467CC45F", msbuildMetadata.ProjectUniqueName);
            Assert.Equal("c:\\x\\x.csproj", msbuildMetadata.ProjectPath);
            Assert.Equal("x", msbuildMetadata.ProjectName);
            Assert.Equal("c:\\x\\project.json", msbuildMetadata.ProjectJsonPath);
            Assert.Equal(RestoreOutputType.NETCore, msbuildMetadata.OutputType);
            Assert.Equal("c:\\packages", msbuildMetadata.PackagesPath);
            Assert.Equal("https://api.nuget.org/v3/index.json", string.Join("|", msbuildMetadata.Sources.Select(s => s.Source)));
            Assert.Equal("c:\\fallback1|c:\\fallback2", string.Join("|", msbuildMetadata.FallbackFolders));
            Assert.Equal("44B29B8D-8413-42D2-8DF4-72225659619B|c:\\a\\a.csproj|78A6AD3F-9FA5-47F6-A54E-84B46A48CB2F|c:\\b\\b.csproj", string.Join("|", msbuildMetadata.ProjectReferences.Select(e => $"{e.ProjectUniqueName}|{e.ProjectPath}")));
        }

        [Fact]
        public void DependencyGraphSpec_RoundTripMSBuildMetadata()
        {
            // Arrange
            var frameworks = new List<TargetFrameworkInformation>();
            frameworks.Add(new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.Parse("net45")
            });

            var spec = new PackageSpec(frameworks);
            var msbuildMetadata = new ProjectRestoreMetadata();
            spec.RestoreMetadata = msbuildMetadata;

            msbuildMetadata.ProjectUniqueName = "A55205E7-4D08-4672-8011-0925467CC45F";
            msbuildMetadata.ProjectPath = "c:\\x\\x.csproj";
            msbuildMetadata.ProjectName = "x";
            msbuildMetadata.ProjectJsonPath = "c:\\x\\project.json";
            msbuildMetadata.OutputType = RestoreOutputType.NETCore;
            msbuildMetadata.PackagesPath = "c:\\packages";
            msbuildMetadata.Sources = new[] { new PackageSource("https://api.nuget.org/v3/index.json") };
            msbuildMetadata.ProjectReferences.Add(new ProjectRestoreReference()
            {
                ProjectUniqueName = "44B29B8D-8413-42D2-8DF4-72225659619B",
                ProjectPath = "c:\\a\\a.csproj"
            });

            msbuildMetadata.ProjectReferences.Add(new ProjectRestoreReference()
            {
                ProjectUniqueName = "78A6AD3F-9FA5-47F6-A54E-84B46A48CB2F",
                ProjectPath = "c:\\b\\b.csproj"
            });

            msbuildMetadata.FallbackFolders.Add("c:\\fallback1");
            msbuildMetadata.FallbackFolders.Add("c:\\fallback2");

            JObject json = new JObject();

            // Act
            JsonPackageSpecWriter.WritePackageSpec(spec, json);
            var readSpec = JsonPackageSpecReader.GetPackageSpec(json.ToString(), "x", "c:\\fake\\project.json");
            var msbuildMetadata2 = readSpec.RestoreMetadata;

            // Assert
            Assert.NotNull(msbuildMetadata2);
            Assert.Equal("A55205E7-4D08-4672-8011-0925467CC45F", msbuildMetadata2.ProjectUniqueName);
            Assert.Equal("c:\\x\\x.csproj", msbuildMetadata2.ProjectPath);
            Assert.Equal("x", msbuildMetadata2.ProjectName);
            Assert.Equal("c:\\x\\project.json", msbuildMetadata2.ProjectJsonPath);
            Assert.Equal(RestoreOutputType.NETCore, msbuildMetadata2.OutputType);
            Assert.Equal("c:\\packages", msbuildMetadata2.PackagesPath);
            Assert.Equal("https://api.nuget.org/v3/index.json", string.Join("|", msbuildMetadata.Sources.Select(s => s.Source)));
            Assert.Equal("c:\\fallback1|c:\\fallback2", string.Join("|", msbuildMetadata2.FallbackFolders));
            Assert.Equal("44B29B8D-8413-42D2-8DF4-72225659619B|c:\\a\\a.csproj|78A6AD3F-9FA5-47F6-A54E-84B46A48CB2F|c:\\b\\b.csproj", string.Join("|", msbuildMetadata2.ProjectReferences.Select(e => $"{e.ProjectUniqueName}|{e.ProjectPath}")));
        }
    }
}
