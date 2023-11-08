// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Test.Utility;
using Xunit;
using System.IO;
using NuGet.Configuration;
using NuGet.CommandLine.XPlat;
using System;
using System.Linq;
using System.Globalization;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class PackageSearchRunnerTests : PackageSearchTestInitializer
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
                        ""summary"": """",
                        ""title"": ""Json.NET"",
                        ""iconUrl"": ""https://api.nuget.org/v3-flatcontainer/newtonsoft.json/12.0.3/icon"",
                        ""licenseUrl"": ""https://www.nuget.org/packages/Newtonsoft.Json/12.0.3/license"",

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

        private string _exactMatchGetMetadataResult = @"{
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

        private MockServer _mockServerWithMultipleEndPoints;

        public PackageSearchRunnerTests()
        {
            _mockServerWithMultipleEndPoints = new MockServer();
            string index = $@"
                {{
                    ""version"": ""3.0.0"",

                    ""resources"": [
                    {{
                        ""@id"": ""{_mockServerWithMultipleEndPoints.Uri + "search/query"}"",
                        ""@type"": ""SearchQueryService/Versioned"",
                        ""comment"": ""Query endpoint of NuGet Search service (primary)""
                    }},
                    {{
                        ""@id"": ""{_mockServerWithMultipleEndPoints.Uri + "v3/registration5-semver1/"}"",
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

            _mockServerWithMultipleEndPoints.Get.Add("/v3/index.json", r => index);
            _mockServerWithMultipleEndPoints.Get.Add($"/search/query?q=json&skip=0&take=10&prerelease=true&semVerLevel=2.0.0", r => _onePackageQueryResult);
            _mockServerWithMultipleEndPoints.Get.Add($"/search/query?q=json&skip=0&take=20&prerelease=false&semVerLevel=2.0.0", r => _onePackageQueryResult);
            _mockServerWithMultipleEndPoints.Get.Add($"/search/query?q=json&skip=5&take=10&prerelease=true&semVerLevel=2.0.0", r => _onePackageQueryResult);
            _mockServerWithMultipleEndPoints.Get.Add($"/search/query?q=json&skip=10&take=20&prerelease=false&semVerLevel=2.0.0", r => _onePackageQueryResult);
            _mockServerWithMultipleEndPoints.Get.Add($"/v3/registration5-semver1/fake.newtonsoft.json/index.json", r => _exactMatchGetMetadataResult);
            _mockServerWithMultipleEndPoints.Start();
        }

        [Theory]
        [InlineData(0, 10, true)]
        [InlineData(0, 20, false)]
        [InlineData(5, 10, true)]
        [InlineData(10, 20, false)]
        public async Task PackageSearchRunner_SearchAPIWithVariousSkipTakePrereleaseOptionsValuesReturnsOnePackage_OnePackageTableOutputted(int skip, int take, bool prerelease)
        {
            // Arrange
            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
            var expectedValues = new List<string>
                {
                    "| Package ID           ",
                    "| Latest Version ",
                    "| Authors           ",
                    "| Downloads   ",
                    "|----------------------",
                    "|----------------",
                    "|-------------------",
                    "|-------------",
                    "| ",
                    "",
                    "Fake.Newtonsoft.",
                    "Json",
                    "",
                    " ",
                    "| 12.0.3         ",
                    "| James Newton-King ",
                    "| 531,607,259 ",
                };

                PackageSearchArgs packageSearchArgs = new()
                {
                    Skip = skip,
                    Take = take,
                    Prerelease = prerelease,
                    ExactMatch = false,
                    Logger = GetLogger(),
                    SearchTerm = "json",
                    Sources = new List<string> { $"{_mockServerWithMultipleEndPoints.Uri}v3/index.json" }
                };

                // Act
                await PackageSearchRunner.RunAsync(
                    sourceProvider: sourceProvider,
                    packageSearchArgs,
                    cancellationToken: System.Threading.CancellationToken.None);

            // Assert
            foreach (var expected in expectedValues)
            {
                Assert.Contains(expected, ColoredMessage.Select(tuple => tuple.Item1));
            }

            Assert.Contains(Tuple.Create("Json", ConsoleColor.Red), ColoredMessage);
        }

        [Fact]
        public async Task PackageSearchRunner_ExactMatchOptionEnabled_OnePackageTableOutputted()
        {
            // Arrange
            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
            var expectedValues = new List<string>
                {
                    "| Package ID           ",
                    "| Latest Version ",
                    "| Authors           ",
                    "| Downloads   ",
                    "|----------------------",
                    "|----------------",
                    "|-------------------",
                    "|-------------",
                    "| ",
                    "",
                    "",
                    " ",
                    "| 12.0.3         ",
                    "| James Newton-King ",
                    "| 531,607,259 ",
                };

                PackageSearchArgs packageSearchArgs = new()
                {
                    Skip = 0,
                    Take = 20,
                    Prerelease = false,
                    ExactMatch = true,
                    Logger = GetLogger(),
                    SearchTerm = "Fake.Newtonsoft.Json",
                    Sources = new List<string> { $"{_mockServerWithMultipleEndPoints.Uri}v3/index.json" }
                };

                // Act
                await PackageSearchRunner.RunAsync(
                    sourceProvider: sourceProvider,
                    packageSearchArgs,
                    cancellationToken: System.Threading.CancellationToken.None);

            // Assert
            foreach (var expected in expectedValues)
            {
                Assert.Contains(expected, ColoredMessage.Select(tuple => tuple.Item1));
            }

            Assert.Contains(Tuple.Create("Fake.Newtonsoft.Json", ConsoleColor.Red), ColoredMessage);
        }

        [Fact]
        public async Task PackageSearchRunner_WhenSourceIsInvalid_ReturnsExitCodeOne()
        {
            // Arrange
            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
            string source = "invalid-source";
            string expectedError = string.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidSource, source);
            PackageSearchArgs packageSearchArgs = new()
            {
                Skip = 0,
                Take = 10,
                Prerelease = true,
                ExactMatch = false,
                Logger = GetLogger(),
                SearchTerm = "json",
                Sources = new List<string> { source }
            };

            // Act
            int exitCode = await PackageSearchRunner.RunAsync(
                sourceProvider: sourceProvider,
                packageSearchArgs,
                cancellationToken: System.Threading.CancellationToken.None);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Contains(expectedError, StoredErrorMessage);
        }
    }
}
