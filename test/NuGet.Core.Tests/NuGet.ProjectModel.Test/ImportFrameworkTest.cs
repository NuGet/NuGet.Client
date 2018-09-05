// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
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

            // Act & Assert
            var ex = Assert.Throws<FileFormatException>(() => JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", "project.json"));
            Assert.Equal("Imports contains an invalid framework: '[  \"dotnet5.3\",  \"portable-net452+win81\",  \"furtureFramework\"]' in 'project.json'.", ex.InnerException.Message);

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

        [Fact]
        public void ImportFramwork_SingleImport()
        {
            var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1""
                    },
                    ""frameworks"": {
                        ""netstandard1.2"": {
                            ""imports"": [""dotnet5.3""],
                            ""warn"": false
                        }
                    }
                }");

            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", "project.json");
            var importFramework = spec.TargetFrameworks.First().Imports.ToList();
            var expectedFramework = NuGetFramework.Parse("dotnet5.3");

            // Assert
            Assert.Equal(1, importFramework.Count);
            Assert.True(importFramework[0].Equals(expectedFramework));
           
        }
    }
}
