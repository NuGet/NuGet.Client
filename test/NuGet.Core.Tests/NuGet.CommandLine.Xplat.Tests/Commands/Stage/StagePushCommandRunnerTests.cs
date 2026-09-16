// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.Commands.Stage;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests.Commands.Stage
{
    public class StagePushCommandRunnerTests
    {
        [Fact]
        public async Task ExecuteCommandAsync_PackageWithSnupkg_StagesPackageThenSymbols()
        {
            using var directory = TestDirectory.Create();
            string packagePath = Path.Combine(directory.Path, "Example.1.0.0.nupkg");
            string symbolsPath = Path.Combine(directory.Path, "Example.1.0.0.snupkg");
            File.WriteAllText(packagePath, "package");
            File.WriteAllText(symbolsPath, "symbols");

            var requests = new List<(Uri Uri, string? ApiKey, string? GroupId)>();
            PackageStagingResourceV3 resource = CreateResource(requests);
            var source = new PackageSource("https://unit.test/v3/index.json", "source");
            var sourceProvider = CreateSourceProvider(source);
            var settings = new Mock<ISettings>();
            var logger = new Mock<ILoggerWithColor>();
            var environment = new TestEnvironmentVariableReader(
                new Dictionary<string, string> { ["NUGET_API_KEY"] = "environment-key" });
            var runner = new StagePushCommandRunner(
                environment,
                (_, _, _) => Task.FromResult<PackageStagingResourceV3?>(resource));

            int exitCode = await runner.ExecuteCommandAsync(
                CreateArgs(packagePath, logger.Object, groupId: "release"),
                settings.Object,
                sourceProvider.Object);

            exitCode.Should().Be(ExitCodes.Success);
            requests.Should().HaveCount(2);
            requests[0].Uri.AbsolutePath.Should().Be("/staging/package");
            requests[1].Uri.AbsolutePath.Should().Be("/staging/symbols");
            requests.Should().OnlyContain(request => request.ApiKey == "environment-key");
            requests.Should().OnlyContain(request => request.GroupId == "release");
            logger.Verify(
                value => value.LogMinimal(It.Is<string>(message => message.Contains(packagePath, StringComparison.Ordinal))),
                Times.Once);
            logger.Verify(
                value => value.LogMinimal(It.Is<string>(message => message.Contains(symbolsPath, StringComparison.Ordinal))),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteCommandAsync_NoSymbols_StagesOnlyPackage()
        {
            using var directory = TestDirectory.Create();
            string packagePath = Path.Combine(directory.Path, "Example.1.0.0.nupkg");
            File.WriteAllText(packagePath, "package");
            File.WriteAllText(Path.Combine(directory.Path, "Example.1.0.0.snupkg"), "symbols");

            var requests = new List<(Uri Uri, string? ApiKey, string? GroupId)>();
            PackageStagingResourceV3 resource = CreateResource(requests);
            var source = new PackageSource("https://unit.test/v3/index.json", "source");
            var sourceProvider = CreateSourceProvider(source);
            var runner = new StagePushCommandRunner(
                TestEnvironmentVariableReader.EmptyInstance,
                (_, _, _) => Task.FromResult<PackageStagingResourceV3?>(resource));

            int exitCode = await runner.ExecuteCommandAsync(
                CreateArgs(packagePath, new Mock<ILoggerWithColor>().Object, noSymbols: true),
                new Mock<ISettings>().Object,
                sourceProvider.Object);

            exitCode.Should().Be(ExitCodes.Success);
            requests.Should().ContainSingle();
            requests[0].Uri.AbsolutePath.Should().Be("/staging/package");
        }

        [Theory]
        [InlineData("Example.1.0.0.snupkg")]
        [InlineData("Example.1.0.0.symbols.nupkg")]
        public async Task ExecuteCommandAsync_DirectSymbolsPackage_StagesSymbols(string fileName)
        {
            using var directory = TestDirectory.Create();
            string packagePath = Path.Combine(directory.Path, fileName);
            File.WriteAllText(packagePath, "symbols");

            var requests = new List<(Uri Uri, string? ApiKey, string? GroupId)>();
            PackageStagingResourceV3 resource = CreateResource(requests);
            var source = new PackageSource("https://unit.test/v3/index.json", "source");
            var sourceProvider = CreateSourceProvider(source);
            var runner = new StagePushCommandRunner(
                TestEnvironmentVariableReader.EmptyInstance,
                (_, _, _) => Task.FromResult<PackageStagingResourceV3?>(resource));

            int exitCode = await runner.ExecuteCommandAsync(
                CreateArgs(packagePath, new Mock<ILoggerWithColor>().Object, noSymbols: true),
                new Mock<ISettings>().Object,
                sourceProvider.Object);

            exitCode.Should().Be(ExitCodes.Success);
            requests.Should().ContainSingle();
            requests[0].Uri.AbsolutePath.Should().Be("/staging/symbols");
        }

        [Fact]
        public async Task ExecuteCommandAsync_UnsupportedFile_FailsBeforeResourceDiscovery()
        {
            using var directory = TestDirectory.Create();
            string packagePath = Path.Combine(directory.Path, "Example.1.0.0.zip");
            File.WriteAllText(packagePath, "package");
            bool resourceRequested = false;
            var source = new PackageSource("https://unit.test/v3/index.json", "source");
            var sourceProvider = CreateSourceProvider(source);
            var runner = new StagePushCommandRunner(
                TestEnvironmentVariableReader.EmptyInstance,
                (_, _, _) =>
                {
                    resourceRequested = true;
                    return Task.FromResult<PackageStagingResourceV3?>(null);
                });

            Func<Task> action = () => runner.ExecuteCommandAsync(
                CreateArgs(packagePath, new Mock<ILoggerWithColor>().Object),
                new Mock<ISettings>().Object,
                sourceProvider.Object);

            await action.Should().ThrowAsync<ArgumentException>();
            resourceRequested.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteCommandAsync_SourceWithoutStagingResource_Throws()
        {
            using var directory = TestDirectory.Create();
            string packagePath = Path.Combine(directory.Path, "Example.1.0.0.nupkg");
            File.WriteAllText(packagePath, "package");
            var source = new PackageSource("https://unit.test/v3/index.json", "source");
            var sourceProvider = CreateSourceProvider(source);
            var runner = new StagePushCommandRunner(
                TestEnvironmentVariableReader.EmptyInstance,
                (_, _, _) => Task.FromResult<PackageStagingResourceV3?>(null));

            Func<Task> action = () => runner.ExecuteCommandAsync(
                CreateArgs(packagePath, new Mock<ILoggerWithColor>().Object),
                new Mock<ISettings>().Object,
                sourceProvider.Object);

            await action.Should().ThrowAsync<FatalProtocolException>()
                .WithMessage("*does not advertise*");
        }

        [Fact]
        public async Task ExecuteCommandAsync_UsesDiscoveredStagingResource()
        {
            using var directory = TestDirectory.Create();
            string packagePath = Path.Combine(directory.Path, "Example.1.0.0.nupkg");
            File.WriteAllText(packagePath, "package");

            using var server = new MockServer();
            string sourceUrl = $"{server.Uri}v3/index.json";
            string stagingUrl = $"{server.Uri}staging/";
            string? apiKey = null;
            server.Get.Add("/v3/index.json", _ => $$"""
                {
                  "version": "3.0.0",
                  "resources": [
                    {
                      "@id": "{{stagingUrl}}",
                      "@type": "PackageStaging/1.0.0"
                    }
                  ]
                }
                """);
            server.Put.Add("/staging/package", request =>
            {
                apiKey = request.Headers["X-NuGet-ApiKey"];
                return HttpStatusCode.Created;
            });
            server.Start();

            var source = new PackageSource(sourceUrl, "source")
            {
                AllowInsecureConnections = true,
            };
            var sourceProvider = CreateSourceProvider(source);
            var environment = new TestEnvironmentVariableReader(
                new Dictionary<string, string> { ["NUGET_API_KEY"] = "environment-key" });
            var runner = new StagePushCommandRunner(environment);

            int exitCode = await runner.ExecuteCommandAsync(
                CreateArgs(packagePath, new Mock<ILoggerWithColor>().Object),
                new Mock<ISettings>().Object,
                sourceProvider.Object);

            exitCode.Should().Be(ExitCodes.Success);
            apiKey.Should().Be("environment-key");
        }

        [Fact]
        public async Task ExecuteCommandAsync_WhenSymbolsFail_ReportsPartialFailure()
        {
            using var directory = TestDirectory.Create();
            string packagePath = Path.Combine(directory.Path, "Example.1.0.0.nupkg");
            string symbolsPath = Path.Combine(directory.Path, "Example.1.0.0.snupkg");
            File.WriteAllText(packagePath, "package");
            File.WriteAllText(symbolsPath, "symbols");

            var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                ["https://unit.test/staging/package"] = _ =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)),
                ["https://unit.test/staging/symbols"] = _ =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)),
            };
            using var httpSource = new TestHttpSource(
                new PackageSource("https://unit.test/v3/index.json"),
                responses);
            var resource = new PackageStagingResourceV3(
                new Uri("https://unit.test/staging/"),
                httpSource);
            var source = new PackageSource("https://unit.test/v3/index.json", "source");
            var sourceProvider = CreateSourceProvider(source);
            var logger = new Mock<ILoggerWithColor>();
            var runner = new StagePushCommandRunner(
                TestEnvironmentVariableReader.EmptyInstance,
                (_, _, _) => Task.FromResult<PackageStagingResourceV3?>(resource));

            int exitCode = await runner.ExecuteCommandAsync(
                CreateArgs(packagePath, logger.Object),
                new Mock<ISettings>().Object,
                sourceProvider.Object);

            exitCode.Should().Be(1);
            logger.Verify(
                value => value.LogMinimal(It.Is<string>(message => message.Contains(packagePath, StringComparison.Ordinal))),
                Times.Once);
            logger.Verify(
                value => value.LogError(It.Is<string>(message => message.Contains(symbolsPath, StringComparison.Ordinal))),
                Times.Once);
        }

        [Fact]
        public void FindSymbolsPackage_PrefersSnupkg()
        {
            using var directory = TestDirectory.Create();
            string packagePath = Path.Combine(directory.Path, "Example.1.0.0.nupkg");
            string snupkgPath = Path.Combine(directory.Path, "Example.1.0.0.snupkg");
            File.WriteAllText(packagePath, "package");
            File.WriteAllText(snupkgPath, "symbols");
            File.WriteAllText(Path.Combine(directory.Path, "Example.1.0.0.symbols.nupkg"), "legacy symbols");

            string? result = StagePushCommandRunner.FindSymbolsPackage(packagePath);

            result.Should().Be(snupkgPath);
        }

        private static StagePushCommandArgs CreateArgs(
            string packagePath,
            ILoggerWithColor logger,
            string? groupId = null,
            bool noSymbols = false)
        {
            return new StagePushCommandArgs
            {
                PackagePath = packagePath,
                Source = "source",
                GroupId = groupId,
                NoSymbols = noSymbols,
                Logger = logger,
                CancellationToken = CancellationToken.None,
            };
        }

        private static Mock<IPackageSourceProvider> CreateSourceProvider(PackageSource source)
        {
            var sourceProvider = new Mock<IPackageSourceProvider>();
            sourceProvider.SetupGet(value => value.DefaultPushSource).Returns(source.Source);
            sourceProvider.Setup(value => value.LoadPackageSources()).Returns([source]);
            return sourceProvider;
        }

        private static PackageStagingResourceV3 CreateResource(
            List<(Uri Uri, string? ApiKey, string? GroupId)> requests)
        {
            var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                ["https://unit.test/staging/package"] = Capture,
                ["https://unit.test/staging/symbols"] = Capture,
            };
            var httpSource = new TestHttpSource(
                new PackageSource("https://unit.test/v3/index.json"),
                responses);

            return new PackageStagingResourceV3(
                new Uri("https://unit.test/staging/"),
                httpSource);

            async Task<HttpResponseMessage> Capture(HttpRequestMessage request)
            {
                string? apiKey = request.Headers.TryGetValues("X-NuGet-ApiKey", out IEnumerable<string>? values)
                    ? string.Join(",", values)
                    : null;
                string? groupId = null;
                if (request.Content is MultipartFormDataContent content)
                {
                    foreach (HttpContent part in content)
                    {
                        if (part.Headers.ContentDisposition?.Name?.Trim('"') == "groupId")
                        {
                            groupId = await part.ReadAsStringAsync();
                        }
                    }
                }

                requests.Add((request.RequestUri!, apiKey, groupId));
                return new HttpResponseMessage(HttpStatusCode.Created);
            }
        }
    }
}
