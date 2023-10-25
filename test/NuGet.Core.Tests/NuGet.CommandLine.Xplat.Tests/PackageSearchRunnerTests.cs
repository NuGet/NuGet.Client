// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Test.Utility;
using Xunit;
using System.IO;
using NuGet.Configuration;
using NuGet.CommandLine.XPlat;

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
                "| Package ID           | Latest Version | Authors           | Downloads   |\n"
                + "|----------------------|----------------|-------------------|-------------|\n"
                + "| Fake.Newtonsoft.Json | 12.0.3         | James Newton-King | 531,607,259 |\n";

        private string NormalizeNewlines(string input)
        {
            return input.Replace("\r\n", "\n").Replace("\r", "\n");
        }

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
            await PackageSearchRunner.RunAsync(
                sourceProvider: sourceProvider,
                sources: new List<string> { $"{mockServer.Uri}v3/index.json" },
                searchTerm: "json",
                skip: 0,
                take: 20,
                prerelease: false,
                exactMatch: false,
                logger: Common.NullLogger.Instance,
                cancellationToken: System.Threading.CancellationToken.None);
            string consoleOutputNormalized = NormalizeNewlines(consoleOutput.ToString());

            //stop mock server
            mockServer.Stop();

            // Assert
            Assert.Equal(_onePackageExpectedOutput, consoleOutputNormalized);
        }

        [Theory]
        [InlineData(0, 10, true)]
        [InlineData(0, 20, false)]
        [InlineData(5, 10, true)]
        [InlineData(10, 20, false)]
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
            string prereleaseValue = prerelease ? "true" : "false";
            mockServer.Get.Add($"/search/query?q=json&skip={skip}&take={take}&prerelease={prereleaseValue}&semVerLevel=2.0.0", r => _onePackageQueryResult);
            mockServer.Start();

            // Redirect console output
            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await PackageSearchRunner.RunAsync(
                sourceProvider: sourceProvider,
                sources: new List<string> { $"{mockServer.Uri}v3/index.json" },
                searchTerm: "json",
                skip: skip,
                take: take,
                prerelease: prerelease,
                exactMatch: false,
                logger: Common.NullLogger.Instance,
                cancellationToken: System.Threading.CancellationToken.None);
            string consoleOutputNormalized = NormalizeNewlines(consoleOutput.ToString());

            //stop mock server
            mockServer.Stop();

            // Assert
            Assert.Equal(_onePackageExpectedOutput, consoleOutputNormalized);
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
                        ""@id"": ""{mockServer.Uri + "v3/registration5-semver1/"}"",
                        ""@type"": ""RegistrationsBaseUrl/3.0.0-rc"",
                        ""comment"": ""Base URL of Azure storage where NuGet package registration info is stored used by RC clients. This base URL does not include SemVer 2.0.0 packages.""
                    }}
                    ],

                    ""@context"":
                    {{
                        ""@vocab"": ""http://schema.nuget.org/services#"",
                        ""comment"": ""http://www.w3.org/2000/01/rdf-schema#comment""
                    }}
                }}";

            const string exactMatchGetMetadataResult = @"{
                            ""@type"": ""Package"",
                            ""count"": 1,
                            ""items"": [
                                {
                                    ""@type"": ""catalog:CatalogPage"",
                                    ""count"": 1,
                                    ""items"": [
                                        {
                                            ""@type"": ""Package"",
                                            ""catalogEntry"": {
                                                ""@type"": ""PackageDetails"",
                                                ""authors"": ""James Newton-King"",
                                                ""id"": ""Fake.Newtonsoft.Json"",
                                                ""version"": ""12.0.3"",
                                                ""totalDownloads"": 531607259
                                            },
                                        }
                                    ],
                                    ""lower"": ""1.0.0"",
                                    ""upper"": ""13.0.0""
                                }
                            ],
                            ""@context"": {
                                ""@vocab"": ""http://schema.nuget.org/schema#"",
                                ""catalog"": ""http://schema.nuget.org/catalog#""
                            }
                        }";

            mockServer.Get.Add("/v3/index.json", r => index);
            mockServer.Get.Add($"/v3/registration5-semver1/fake.newtonsoft.json/index.json", r => exactMatchGetMetadataResult);
            mockServer.Start();

            // Redirect console output
            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await PackageSearchRunner.RunAsync(
                sourceProvider: sourceProvider,
                sources: new List<string> { $"{mockServer.Uri}v3/index.json" },
                searchTerm: "Fake.Newtonsoft.Json",
                skip: 0,
                take: 20,
                prerelease: false,
                exactMatch: true,
                logger: Common.NullLogger.Instance,
                cancellationToken: System.Threading.CancellationToken.None);
            string consoleOutputNormalized = NormalizeNewlines(consoleOutput.ToString());

            //stop mock server
            mockServer.Stop();

            // Assert
            Assert.Equal(_onePackageExpectedOutput, consoleOutputNormalized);
        }
    }
}