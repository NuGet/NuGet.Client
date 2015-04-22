using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using Xunit;

namespace ProjectManagement.Test
{
    public class JsonConfigUtilityTests
    {

        [Fact]
        public void JsonConfigUtility_GetTargetFramework()
        {
            // Arrange
            var json = BasicConfig;

            // Act
            var frameworks = JsonConfigUtility.GetFrameworks(json);

            // Assert
            Assert.Equal("netcore5", frameworks.Single().GetShortFolderName());
        }

        [Fact]
        public void JsonConfigUtility_AddDependencyToNewFile()
        {
            // Arrange
            var json = BasicConfig;

            // Act
            JsonConfigUtility.AddDependency(json, new PackageDependency("testpackage", VersionRange.Parse("1.0.0")));

            // Assert
            Assert.Equal("1.0.0", json["dependencies"]["testpackage"].ToString());
        }

        [Fact]
        public void JsonConfigUtility_RemoveDependencyFromNewFile()
        {
            // Arrange
            var json = BasicConfig;

            // Act
            JsonConfigUtility.RemoveDependency(json, "testpackage");

            JToken val = null;
            json.TryGetValue("dependencies", out val);

            // Assert
            Assert.Null(val);
        }

        [Fact]
        public void JsonConfigUtility_AddAndRemovePackage()
        {
            // Arrange
            var json = BasicConfig;

            // Act
            JsonConfigUtility.AddDependency(json, new PackageDependency("testpackage", VersionRange.Parse("1.0.0")));
            JsonConfigUtility.RemoveDependency(json, "testpackage");

            JToken val = null;
            json.TryGetValue("dependencies", out val);

            // Assert
            Assert.Equal(0, val.Count());
        }

        private static JObject BasicConfig
        {
            get
            {
                JObject json = new JObject();

                JObject frameworks = new JObject();
                frameworks["netcore50"] = new JObject();

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
        }

    }
}
