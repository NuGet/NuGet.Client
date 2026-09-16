// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Globalization;
using System.IO;
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
        public async Task ExecuteCommandAsync_UnsupportedFile_FailsBeforeResourceDiscovery()
        {
            // Arrange
            using var directory = TestDirectory.Create();
            string packagePath = Path.Combine(directory.Path, "Contoso.1.0.0.zip");
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

            // Act
            Func<Task> action = () => runner.ExecuteCommandAsync(
                CreateArgs(packagePath, new Mock<ILoggerWithColor>().Object),
                new Mock<ISettings>().Object,
                sourceProvider.Object);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>();
            resourceRequested.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteCommandAsync_SourceWithoutStagingResource_Throws()
        {
            // Arrange
            using var directory = TestDirectory.Create();
            string packagePath = Path.Combine(directory.Path, "Contoso.1.0.0.nupkg");
            File.WriteAllText(packagePath, "package");
            var source = new PackageSource("https://unit.test/v3/index.json", "source");
            var sourceProvider = CreateSourceProvider(source);
            var runner = new StagePushCommandRunner(
                TestEnvironmentVariableReader.EmptyInstance,
                (_, _, _) => Task.FromResult<PackageStagingResourceV3?>(null));
            string expectedMessage = string.Format(
                CultureInfo.CurrentCulture,
                CommandLine.XPlat.Strings.StagePushCommand_Error_ResourceNotFound,
                source.Source);

            // Act
            Func<Task> action = () => runner.ExecuteCommandAsync(
                CreateArgs(packagePath, new Mock<ILoggerWithColor>().Object),
                new Mock<ISettings>().Object,
                sourceProvider.Object);

            // Assert
            await action.Should().ThrowAsync<FatalProtocolException>()
                .WithMessage(expectedMessage);
        }

        [Fact]
        public void FindSymbolsPackage_PrefersSnupkg()
        {
            // Arrange
            using var directory = TestDirectory.Create();
            string packagePath = Path.Combine(directory.Path, "Contoso.1.0.0.nupkg");
            string snupkgPath = Path.Combine(directory.Path, "Contoso.1.0.0.snupkg");
            File.WriteAllText(packagePath, "package");
            File.WriteAllText(snupkgPath, "symbols");
            File.WriteAllText(Path.Combine(directory.Path, "Contoso.1.0.0.symbols.nupkg"), "legacy symbols");

            // Act
            string? result = StagePushCommandRunner.FindSymbolsPackage(packagePath);

            // Assert
            result.Should().Be(snupkgPath);
        }

        private static StagePushCommandArgs CreateArgs(
            string packagePath,
            ILoggerWithColor logger)
        {
            return new StagePushCommandArgs
            {
                PackagePath = packagePath,
                Source = "source",
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
    }
}
