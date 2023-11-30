// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using System.IO;
using NuGet.Configuration;
using NuGet.CommandLine.XPlat;
using System;
using System.Linq;
using System.Globalization;
using System.Threading;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class PackageSearchRunnerTests : PackageSearchTestInitializer, IClassFixture<PackageSearchRunnerFixture>
    {
        PackageSearchRunnerFixture _fixture;

        public PackageSearchRunnerTests(PackageSearchRunnerFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData(0, 10, true)]
        [InlineData(0, 20, false)]
        [InlineData(5, 10, true)]
        [InlineData(10, 20, false)]
        public async Task PackageSearchRunner_TableFormatNormalVerbosity_OnePackageTableOutputted(int skip, int take, bool prerelease)
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
                "| Owners            ",
                "| Total Downloads ",
                "|----------------------",
                "|----------------",
                "|-------------------",
                "|-----------------",
                "| ",
                "",
                "Fake.Newtonsoft.",
                "Json",
                "",
                " ",
                "| 12.0.3         ",
                "| James Newton-King ",
                "| 531,607,259     ",
            };

            var notExpectedValues = new List<string>
            {
                "| Vulnerable ",
                "| Deprecation                      ",
                "| Project URL   ",
                "| Description     ",
            };

            PackageSearchArgs packageSearchArgs = new()
            {
                Skip = skip,
                Take = take,
                Prerelease = prerelease,
                ExactMatch = false,
                Logger = GetLogger(),
                SearchTerm = "json",
                Sources = new List<string> { $"{_fixture.ServerWithMultipleEndpoints.Uri}v3/index.json" },
                Verbosity = PackageSearchVerbosity.Normal,
                JsonFormat = false
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

            foreach (var notExpected in notExpectedValues)
            {
                Assert.DoesNotContain(notExpected, ColoredMessage.Select(tuple => tuple.Item1));
            }

            Assert.Contains(Tuple.Create("Json", ConsoleColor.Red), ColoredMessage);
        }

        [Theory]
        [InlineData(0, 10, true)]
        [InlineData(0, 20, false)]
        [InlineData(5, 10, true)]
        [InlineData(10, 20, false)]
        public async Task PackageSearchRunner_TableFormatMinimalVerbosity_OnePackageTableOutputted(int skip, int take, bool prerelease)
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
                "|----------------------",
                "|----------------",
                "| ",
                "",
                "Fake.Newtonsoft.",
                "Json",
                "",
                " ",
                "| 12.0.3         ",
            };

            var notExpectedValues = new List<string>
            {
                "| Owners            ",
                "| Total Downloads ",
                "| Vulnerable ",
                "| Deprecation                      ",
                "| Project URL   ",
                "| Description     ",
            };

            PackageSearchArgs packageSearchArgs = new()
            {
                Skip = skip,
                Take = take,
                Prerelease = prerelease,
                ExactMatch = false,
                Logger = GetLogger(),
                SearchTerm = "json",
                Sources = new List<string> { $"{_fixture.ServerWithMultipleEndpoints.Uri}v3/index.json" },
                Verbosity = PackageSearchVerbosity.Minimal,
                JsonFormat = false
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

            foreach (var notExpected in notExpectedValues)
            {
                Assert.DoesNotContain(notExpected, ColoredMessage.Select(tuple => tuple.Item1));
            }

            Assert.Contains(Tuple.Create("Json", ConsoleColor.Red), ColoredMessage);
        }

        [Theory]
        [InlineData(0, 10, true)]
        [InlineData(0, 20, false)]
        [InlineData(5, 10, true)]
        [InlineData(10, 20, false)]
        public async Task PackageSearchRunner_TableFormatDetailedVerbosity_OnePackageTableOutputted(int skip, int take, bool prerelease)
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
                    "| Owners            ",
                    "| Total Downloads ",
                    "| Vulnerable ",
                    "| Deprecation                      ",
                    "| Project URL   ",
                    "| Description     ",
                    "|----------------------",
                    "|----------------",
                    "|-------------------",
                    "|-----------------",
                    "|------------",
                    "|----------------------------------",
                    "|---------------",
                    "|-----------------",
                    " ",
                    "Fake.Newtonsoft.",
                    " ",
                    " ",
                    "| 12.0.3         ",
                    "| James Newton-King ",
                    "| 531,607,259     ",
                    "| N/A        ",
                    "| This package has been deprecated ",
                    "| http://myuri/ ",
                    "| My description. "
                };

            PackageSearchArgs packageSearchArgs = new()
            {
                Skip = skip,
                Take = take,
                Prerelease = prerelease,
                ExactMatch = false,
                Logger = GetLogger(),
                SearchTerm = "json",
                Sources = new List<string> { $"{_fixture.ServerWithMultipleEndpoints.Uri}v3/index.json" },
                Verbosity = PackageSearchVerbosity.Detailed,
                JsonFormat = false
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

        [Theory]
        [InlineData(0, 10, true)]
        [InlineData(0, 20, false)]
        [InlineData(5, 10, true)]
        [InlineData(10, 20, false)]
        public async Task PackageSearchRunner_JsonFormatNormalVerbosity_OnePackageJsonOutputted(int skip, int take, bool prerelease)
        {
            // Arrange
            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);

            PackageSearchArgs packageSearchArgs = new()
            {
                Skip = skip,
                Take = take,
                Prerelease = prerelease,
                ExactMatch = false,
                Logger = GetLogger(),
                SearchTerm = "json",
                Sources = new List<string> { $"{_fixture.ServerWithMultipleEndpoints.Uri}v3/index.json" },
                Verbosity = PackageSearchVerbosity.Normal,
                JsonFormat = true
            };

            // Act
            await PackageSearchRunner.RunAsync(
                sourceProvider: sourceProvider,
                packageSearchArgs,
                cancellationToken: System.Threading.CancellationToken.None);

            // Assert
            string message = _fixture.NormalizeNewlines(Message);
            Assert.Contains(_fixture.ExpectedSearchResultNormal, message);
        }

        [Theory]
        [InlineData(0, 10, true)]
        [InlineData(0, 20, false)]
        [InlineData(5, 10, true)]
        [InlineData(10, 20, false)]
        public async Task PackageSearchRunner_JsonFormatMinimalVerbosity_OnePackageJsonOutputted(int skip, int take, bool prerelease)
        {
            // Arrange
            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);

            PackageSearchArgs packageSearchArgs = new()
            {
                Skip = skip,
                Take = take,
                Prerelease = prerelease,
                ExactMatch = false,
                Logger = GetLogger(),
                SearchTerm = "json",
                Sources = new List<string> { $"{_fixture.ServerWithMultipleEndpoints.Uri}v3/index.json" },
                Verbosity = PackageSearchVerbosity.Minimal,
                JsonFormat = true
            };

            // Act
            await PackageSearchRunner.RunAsync(
                sourceProvider: sourceProvider,
                packageSearchArgs,
                cancellationToken: System.Threading.CancellationToken.None);

            // Assert
            string message = _fixture.NormalizeNewlines(Message);
            Assert.Contains(_fixture.ExpectedSearchResultMinimal, message);
        }

        [Theory]
        [InlineData(0, 10, true)]
        [InlineData(0, 20, false)]
        [InlineData(5, 10, true)]
        [InlineData(10, 20, false)]
        public async Task PackageSearchRunner_JsonFormatDetailedVerbosity_OnePackageJsonOutputted(int skip, int take, bool prerelease)
        {
            // Arrange
            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);

            PackageSearchArgs packageSearchArgs = new()
            {
                Skip = skip,
                Take = take,
                Prerelease = prerelease,
                ExactMatch = false,
                Logger = GetLogger(),
                SearchTerm = "json",
                Sources = new List<string> { $"{_fixture.ServerWithMultipleEndpoints.Uri}v3/index.json" },
                Verbosity = PackageSearchVerbosity.Detailed,
                JsonFormat = true
            };

            // Act
            await PackageSearchRunner.RunAsync(
                sourceProvider: sourceProvider,
                packageSearchArgs,
                cancellationToken: System.Threading.CancellationToken.None);

            // Assert
            string message = _fixture.NormalizeNewlines(Message);
            Assert.Contains(_fixture.ExpectedSearchResultDetailed, message);
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
                "| Version ",
                "| Owners            ",
                "| Total Downloads ",
                "|----------------------",
                "|---------",
                "|-------------------",
                "|-----------------",
                "| ",
                "",
                "",
                " ",
                "| 12.0.3  ",
                "| James Newton-King ",
                "| 531,607,259     ",
            };

            PackageSearchArgs packageSearchArgs = new()
            {
                Skip = 0,
                Take = 20,
                Prerelease = false,
                ExactMatch = true,
                Logger = GetLogger(),
                SearchTerm = "Fake.Newtonsoft.Json",
                Sources = new List<string> { $"{_fixture.ServerWithMultipleEndpoints.Uri}v3/index.json" }
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

        [Fact]
        public async Task PackageSearchRunner_WhenSourceHasNoSearchResource_LogsSearchServiceMissingError()
        {
            // Arrange
            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
            string source = $"{_fixture.ServerWithMultipleEndpoints.Uri}v3/indexWithNoSearchResource.json";
            string expectedError = Protocol.Strings.Protocol_MissingSearchService;
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
            Assert.Equal(0, exitCode);
            Assert.Contains(expectedError, StoredErrorMessage);
        }

        [Fact]
        public async Task PackageSearchRunner_HandlesOperationCanceledException_WhenCancellationIsRequested()
        {
            // Arrange
            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
            var expectedError = "A task was canceled.";
            var cts = new CancellationTokenSource();
            string source = $"{_fixture.ServerWithMultipleEndpoints.Uri}v3/indexWithNoSearchResource.json";
            PackageSearchArgs packageSearchArgs = new PackageSearchArgs
            {
                Skip = 0,
                Take = 10,
                Prerelease = true,
                ExactMatch = false,
                Logger = GetLogger(),
                SearchTerm = "json",
                Sources = new List<string> { source }
            };

            // Immediately request cancellation
            cts.Cancel();

            // Act
            var exitCode = await PackageSearchRunner.RunAsync(
                    sourceProvider: sourceProvider,
                    packageSearchArgs,
                    cancellationToken: cts.Token);

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Contains(expectedError, StoredErrorMessage);
        }
    }
}
