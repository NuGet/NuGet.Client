﻿using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class ImportFrameworkTest
    {
        [Fact]
        public void ImportFramwork_UnknowFramework()
        {
            var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1""
                    },
                    ""frameworks"": {
                        ""netstandard1.2"": {
                            ""imports"": [""dotnet5.3"",""portable-net452+win81"",""furtureFramework""],
                            ""warn"": false
                        }
                    }
                }");

            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", "project.json");
            var importFramework = spec.TargetFrameworks.First().Imports.ToList();

            // Assert
            Assert.Equal(3, importFramework.Count);
            Assert.Equal(NuGetFramework.UnsupportedFramework.DotNetFrameworkName,importFramework[2].DotNetFrameworkName);
        }

        [Fact]
        public void ImportFramwork_EmptyImport()
        {
            var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1""
                    },
                    ""frameworks"": {
                        ""netstandard1.2"": {
                            ""imports"": """",
                            ""warn"": false
                        }
                    }
                }");

            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", "project.json");
            var importFramework = spec.TargetFrameworks.First().Imports.ToList();

            // Assert
            Assert.Equal(0, importFramework.Count);
        }
    }
}
