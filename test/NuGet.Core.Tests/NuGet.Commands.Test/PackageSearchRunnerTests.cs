// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Commands.CommandRunners;
using Test.Utility;
using Xunit;
using System.IO;
using NuGet.Configuration;

namespace NuGet.Commands.Test
{
    public class PackageSearchRunnerTests
    {
        readonly string _onePackageQueryResult = $@"
                {{
                    ""@context"":
                    {{
                        ""@vocab"": ""http://schema.nuget.org/schema#"",
                        ""@base"": ""https://api.nuget.org/v3/registration5-semver1/""
                    }},
                    ""totalHits"": 396,
                    ""data"": [
                    {{
                        ""@id"": ""https://api.nuget.org/v3/registration5-semver1/newtonsoft.json/index.json"",
                        ""@type"": ""Package"",
                        ""registration"": ""https://api.nuget.org/v3/registration5-semver1/newtonsoft.json/index.json"",
                        ""id"": ""Fake.Newtonsoft.Json"",
                        ""version"": ""12.0.3"",
                        ""description"": ""Json.NET is a popular high-performance JSON framework for .NET"",
                        ""summary"": """",
                        ""title"": ""Json.NET"",
                        ""iconUrl"": ""https://api.nuget.org/v3-flatcontainer/newtonsoft.json/12.0.3/icon"",
                        ""licenseUrl"": ""https://www.nuget.org/packages/Newtonsoft.Json/12.0.3/license"",
                        ""projectUrl"": ""https://www.newtonsoft.com/json"",

                        ""tags"": [
                            ""json""
                        ],

                        ""authors"": [
                        ""James Newton-King""
                        ],

                        ""totalDownloads"": 531607259,
                        ""verified"": true,

                        ""packageTypes"": [
                        {{
                            ""name"": ""Dependency""
                        }}
                        ],

                        ""versions"": [
                        {{
                            ""version"": ""3.5.8"",
                            ""downloads"": 461992,
                            ""@id"": ""https://api.nuget.org/v3/registration5-semver1/newtonsoft.json/3.5.8.json""
                        }}
                        ]
                    }}
                    ]
                }}";
        readonly string _onePackageExpectedOutput =
                "| Package ID           | Latest Version | Authors           | Downloads      |\r\n"
                + "|----------------------|----------------|-------------------|----------------|\r\n"
                + "| Fake.Newtonsoft.Json | 12.0.3         | James Newton-King | 531,607,259.00 |\r\n";

        [Fact]
        public async Task PackageSearchRunner_SearchAPIReturnsOnePackage_OnePackageTableOutputted()
        {
            // Arrange
            ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory(),
                    configFileName: null,
                    machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
            var mockServer = new MockServer();

            string index = $@"
                {{
                    ""version"": ""3.0.0"",

                    ""resources"": [
                    {{
                        ""@id"": ""{mockServer.Uri + "search/query"}"",
                        ""@type"": ""SearchQueryService/Versioned"",
                        ""comment"": ""Query endpoint of NuGet Search service (primary)""
                    }}
                    ],

                    ""@context"":
                    {{
                        ""@vocab"": ""http://schema.nuget.org/services#"",
                        ""comment"": ""http://www.w3.org/2000/01/rdf-schema#comment""
                    }}
                }}";

            mockServer.Get.Add("/v3/index.json", r => index);
            mockServer.Get.Add("/search/query?q=json&skip=0&take=20&prerelease=false&semVerLevel=2.0.0", r => _onePackageQueryResult);
            mockServer.Start();


            // Redirect console output
            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await PackageSearchRunner.RunAsync(sourceProvider, new List<string> { $"{mockServer.Uri}v3/index.json" }, "json", 0, 20, false, false, Common.NullLogger.Instance);

            // Assert
            Assert.Equal(_onePackageExpectedOutput, consoleOutput.ToString());

            //stop mock server
            mockServer.Stop();
        }

        [Theory]
        [InlineData(0, 10, true)]
        [InlineData(0, 20, false)]
        [InlineData(5, 10, true)]
        [InlineData(10, 20, false)]
        [InlineData(15, 25, true)]
        [InlineData(20, 30, false)]
        [InlineData(0, 50, true)]
        [InlineData(25, 25, true)]
        [InlineData(50, 10, false)]
        public async Task PackageSearchRunner_SearchAPIWithVariousSkipTakePrereleaseOptionsValuesReturnsOnePackage_OnePackageTableOutputted(int skip, int take, bool prerelease)
        {
            // Arrange
            ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory(),
                    configFileName: null,
                    machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
            var mockServer = new MockServer();

            string index = $@"
                {{
                    ""version"": ""3.0.0"",

                    ""resources"": [
                    {{
                        ""@id"": ""{mockServer.Uri + "search/query"}"",
                        ""@type"": ""SearchQueryService/Versioned"",
                        ""comment"": ""Query endpoint of NuGet Search service (primary)""
                    }}
                    ],

                    ""@context"":
                    {{
                        ""@vocab"": ""http://schema.nuget.org/services#"",
                        ""comment"": ""http://www.w3.org/2000/01/rdf-schema#comment""
                    }}
                }}";

            mockServer.Get.Add("/v3/index.json", r => index);
            string prereleaseValue =  prerelease ? "true" : "false";
            mockServer.Get.Add($"/search/query?q=json&skip={skip}&take={take}&prerelease={prereleaseValue}&semVerLevel=2.0.0", r => _onePackageQueryResult);
            mockServer.Start();

            // Redirect console output
            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await PackageSearchRunner.RunAsync(sourceProvider, new List<string> { $"{mockServer.Uri}v3/index.json" }, "json", skip, take, prerelease, false, Common.NullLogger.Instance);

            // Assert
            Assert.Equal(_onePackageExpectedOutput, consoleOutput.ToString());

            //stop mock server
            mockServer.Stop();
        }

        [Fact]
        public async Task PackageSearchRunner_GetMetadataAPIRequestReturnsOnePackage_OnePackageTableOutputted()
        {
            // Arrange
            ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory(),
                    configFileName: null,
                    machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
            var mockServer = new MockServer();

            string index = $@"
                {{
                    ""version"": ""3.0.0"",

                    ""resources"": [
                    {{
                        ""@id"": ""{mockServer.Uri + "search/query"}"",
                        ""@type"": ""SearchQueryService/Versioned"",
                        ""comment"": ""Query endpoint of NuGet Search service (primary)""
                    }},
                    {{
                        ""@id"": ""{mockServer.Uri + "v3/registration5-semver1/"}"",
                        ""@type"": ""RegistrationsBaseUrl/3.0.0-rc"",
                        ""comment"": ""Base URL of Azure storage where NuGet package registration info is stored used by RC clients. This base URL does not include SemVer 2.0.0 packages.""
                    }},
                    {{
                        ""@id"": ""{mockServer.Uri + "v3/registration5-semver1/{id-lower}/index.json"}"",
                        ""@type"": ""PackageDisplayMetadataUriTemplate/3.0.0-rc"",
                        ""comment"": ""URI template used by NuGet Client to construct display metadata for Packages using ID""
                    }}
                    ],

                    ""@context"":
                    {{
                        ""@vocab"": ""http://schema.nuget.org/services#"",
                        ""comment"": ""http://www.w3.org/2000/01/rdf-schema#comment""
                    }}
                }}";

            string json = @"{
                            ""@id"": ""https://api.nuget.org/v3/registration5-semver1/newtonsoft.json/index.json"",
                            ""@type"": ""Package"",
                            ""commitId"": ""632756fb-f739-4424-952e-ad8acf8b784d"",
                            ""commitTimeStamp"": ""2023-08-24T17:47:35.2174425+00:00"",
                            ""count"": 2,
                            ""items"": [
                                {
                                    ""@id"": ""https://api.nuget.org/v3/registration5-semver1/nuget.commandline/index.json#page/1.0.11220.26/4.5.0"",
                                    ""@type"": ""catalog:CatalogPage"",
                                    ""count"": 1,
                                    ""items"": [
                                        {
                                            ""@id"": ""https://api.nuget.org/v3/registration5-semver1/nuget.commandline/1.0.11220.26.json"",
                                            ""@type"": ""Package"",
                                            ""commitId"": ""d96df90a-009c-4e5b-84ed-5a3feb38c0c6"",
                                            ""commitTimeStamp"": ""2020-02-07T22:41:07.0034916+00:00"",
                                            ""catalogEntry"": {
                                                ""@id"": ""https://api.nuget.org/v3/catalog0/data/2018.10.05.02.27.33/nuget.commandline.1.0.11220.26.json"",
                                                ""@type"": ""PackageDetails"",
                                                ""authors"": ""James Newton-King"",
                                                ""description"": ""NuGet command line tool used to create and push packages."",
                                                ""iconUrl"": """",
                                                ""id"": ""Fake.Newtonsoft.Json"",
                                                ""language"": ""en-US"",
                                                ""licenseExpression"": """",
                                                ""licenseUrl"": """",
                                                ""listed"": true,
                                                ""minClientVersion"": """",
                                                ""packageContent"": ""https://api.nuget.org/v3-flatcontainer/nuget.commandline/1.0.11220.26/nuget.commandline.1.0.11220.26.nupkg"",
                                                ""projectUrl"": ""http://nuget.codeplex.com"",
                                                ""published"": ""2011-01-16T02:09:54.94+00:00"",
                                                ""requireLicenseAcceptance"": false,
                                                ""summary"": ""NuGet command line tool used to create and push packages."",
                                                ""tags"": [
                                                    ""nuget""
                                                ],
                                                ""title"": """",
                                                ""version"": ""12.0.3"",
                                                ""totalDownloads"": 531607259
                                            },
                                            ""packageContent"": ""https://api.nuget.org/v3-flatcontainer/nuget.commandline/1.0.11220.26/nuget.commandline.1.0.11220.26.nupkg"",
                                            ""registration"": ""https://api.nuget.org/v3/registration5-semver1/nuget.commandline/index.json""
                                        }
                                    ],
                                    ""parent"": ""https://api.nuget.org/v3/registration5-semver1/nuget.commandline/index.json"",
                                    ""lower"": ""4.5.1"",
                                    ""upper"": ""6.7.0""
                                }
                            ],
                            ""@context"": {
                                ""@vocab"": ""http://schema.nuget.org/schema#"",
                                ""catalog"": ""http://schema.nuget.org/catalog#""
                            }
                        }";


            mockServer.Get.Add("/v3/index.json", r => index);
            mockServer.Get.Add($"/v3/registration5-semver1/fake.newtonsoft.json/index.json", r => json);
            mockServer.Start();

            // Redirect console output
            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await PackageSearchRunner.RunAsync(
                sourceProvider,
                new List<string> { $"{mockServer.Uri}v3/index.json" },
                "Fake.Newtonsoft.Json",
                0,
                20,
                false,
                true,
                Common.NullLogger.Instance);

            // Assert
            Assert.Equal(_onePackageExpectedOutput, consoleOutput.ToString());

            //stop mock server
            mockServer.Stop();
        }


    }
}
