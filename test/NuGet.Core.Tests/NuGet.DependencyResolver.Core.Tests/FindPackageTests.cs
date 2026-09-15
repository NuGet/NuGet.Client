// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.DependencyResolver.Core.Tests
{
    public class FindPackageTests
    {
        [Fact]
        public async Task FindPackage_VerifyFloatingPackageIsRequiredOnlyFromASingleSource()
        {
            // Arrange
            var range = new LibraryRange("x", VersionRange.Parse("1.0.0-*"), LibraryDependencyTarget.Package);
            var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var context = new RemoteWalkContext(cacheContext, PackageSourceMapping.GetPackageSourceMapping(NullSettings.Instance), testLogger);
            var token = CancellationToken.None;
            var edge = new GraphEdge<RemoteResolveResult>(null, null, null);
            var actualIdentity = new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta.1"), LibraryType.Package);
            var higherIdentity = new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta.2"), LibraryType.Package);
            var dependencies = new[] { new LibraryDependency() { LibraryRange = new LibraryRange("y", VersionRange.All, LibraryDependencyTarget.Package) } };
            var dependencyInfo = LibraryDependencyInfo.Create(actualIdentity, framework, dependencies);
            var dependencyInfo2 = LibraryDependencyInfo.Create(higherIdentity, framework, dependencies);

            var downloadCount = 0;

            // Source1 returns 1.0.0-beta.1
            var remoteProvider = new Mock<IRemoteDependencyProvider>();
            remoteProvider.Setup(e => e.FindLibraryAsync(range, It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(actualIdentity);
            remoteProvider.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider.SetupGet(e => e.Source).Returns(new PackageSource("test"));
            remoteProvider.Setup(e => e.GetDependenciesAsync(It.IsAny<LibraryIdentity>(), It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(dependencyInfo)
                .Callback(() => ++downloadCount);
            context.RemoteLibraryProviders.Add(remoteProvider.Object);

            // Source2 returns 1.0.0-beta.2
            var remoteProvider2 = new Mock<IRemoteDependencyProvider>();
            remoteProvider2.Setup(e => e.FindLibraryAsync(range, It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(higherIdentity);
            remoteProvider2.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider2.SetupGet(e => e.Source).Returns(new PackageSource("test"));
            remoteProvider2.Setup(e => e.GetDependenciesAsync(It.IsAny<LibraryIdentity>(), It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(dependencyInfo2)
                .Callback(() => ++downloadCount);
            context.RemoteLibraryProviders.Add(remoteProvider2.Object);

            // Act
            var result = await ResolverUtility.FindLibraryEntryAsync(range, framework, null, null, context, token);

            // Assert
            // Verify only one download happened
            Assert.Equal(1, downloadCount);
            Assert.Equal("1.0.0-beta.2", result.Key.Version.ToString());
        }

        [Fact]
        public async Task FindPackage_VerifyMissingListedPackageSucceedsOnRetry()
        {
            // Arrange
            var range = new LibraryRange("x", VersionRange.Parse("1.0.0-beta"), LibraryDependencyTarget.Package);
            var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var context = new RemoteWalkContext(cacheContext, PackageSourceMapping.GetPackageSourceMapping(NullSettings.Instance), testLogger);
            var token = CancellationToken.None;
            var edge = new GraphEdge<RemoteResolveResult>(null, null, null);
            var actualIdentity = new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package);
            var dependencies = new[] { new LibraryDependency() { LibraryRange = new LibraryRange("y", VersionRange.All, LibraryDependencyTarget.Package) } };
            var dependencyInfo = LibraryDependencyInfo.Create(actualIdentity, framework, dependencies);

            var remoteProvider = new Mock<IRemoteDependencyProvider>();
            remoteProvider.Setup(e => e.FindLibraryAsync(range, framework, It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(actualIdentity);
            remoteProvider.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider.SetupGet(e => e.Source).Returns(new PackageSource("test"));

            var hitCount = 0;

            remoteProvider.Setup(e => e.GetDependenciesAsync(actualIdentity, framework, cacheContext, testLogger, token))
                .ThrowsAsync(new PackageNotFoundProtocolException(new PackageIdentity(actualIdentity.Name, actualIdentity.Version)))
                .Callback(() => ++hitCount);

            remoteProvider.Setup(e => e.GetDependenciesAsync(actualIdentity, framework, It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(dependencyInfo)
                .Callback(() => ++hitCount);

            context.RemoteLibraryProviders.Add(remoteProvider.Object);

            // Act
            var result = await ResolverUtility.FindLibraryEntryAsync(range, framework, null, null, context, token);

            // Assert
            Assert.Equal(1, hitCount);
            Assert.Equal("x", result.Key.Name);
        }

        [Theory]
        [InlineData(LibraryDependencyTarget.Package)]
        [InlineData(LibraryDependencyTarget.Project)]
        [InlineData(LibraryDependencyTarget.ExternalProject)]
        [InlineData(LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject)]
        public async Task FindLibraryEntryAsync_LogsOnlyPackages(LibraryDependencyTarget libraryDependencyTarget)
        {
            // Arrange
            const string packageX = "x", version = "1.0.0-beta.1", source = "source";
            var range = new LibraryRange(packageX, VersionRange.Parse(version), libraryDependencyTarget);
            var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var token = CancellationToken.None;
            var edge = new GraphEdge<RemoteResolveResult>(null, null, null);
            var actualIdentity = new LibraryIdentity(packageX, NuGetVersion.Parse(version), LibraryType.Package);
            var dependencies = new[] { new LibraryDependency() { LibraryRange = new LibraryRange("y", VersionRange.All, LibraryDependencyTarget.Package) } };
            var dependencyInfo = LibraryDependencyInfo.Create(actualIdentity, framework, dependencies);

            //package source mapping configuration
            Dictionary<string, IReadOnlyList<string>> patterns = new();
            patterns.Add(source, new List<string>() { packageX });
            PackageSourceMapping sourceMappingConfiguration = new(patterns);
            var context = new RemoteWalkContext(cacheContext, sourceMappingConfiguration, testLogger);

            var remoteProvider = new Mock<IRemoteDependencyProvider>();
            remoteProvider.Setup(e => e.FindLibraryAsync(range, It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(actualIdentity);
            remoteProvider.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider.SetupGet(e => e.Source).Returns(new PackageSource(source));
            remoteProvider.Setup(e => e.GetDependenciesAsync(It.IsAny<LibraryIdentity>(), It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(dependencyInfo);
            context.RemoteLibraryProviders.Add(remoteProvider.Object);

            // Act
            var result = await ResolverUtility.FindLibraryEntryAsync(range, framework, null, null, context, token);

            // Assert
            Assert.Equal(0, testLogger.Errors);
            testLogger.DebugMessages.TryPeek(out string message);
            if (libraryDependencyTarget == LibraryDependencyTarget.Package)
            {
                Assert.Equal($"Package source mapping matches found for package ID '{packageX}' are: '{source}'.", message);
                Assert.Equal(version, result.Key.Version.ToString());
                Assert.Equal(source, result.Data.Match.Provider.Source.Name);
            }
            else
            {
                Assert.Equal(message, null);
            }
        }

        [Fact]
        public async Task FindPackage_VerifyMissingListedPackage_ThrowsNotFoundAndLogsErrorAsync()
        {
            // Arrange
            var range = new LibraryRange("x", VersionRange.Parse("1.0.0-beta"), LibraryDependencyTarget.Package);
            var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var context = new RemoteWalkContext(cacheContext, PackageSourceMapping.GetPackageSourceMapping(NullSettings.Instance), testLogger);
            var token = CancellationToken.None;
            var edge = new GraphEdge<RemoteResolveResult>(null, null, null);
            var actualIdentity = new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package);
            var packageIdentity = new PackageIdentity(actualIdentity.Name, actualIdentity.Version);
            var dependencies = new[] { new LibraryDependency() { LibraryRange = new LibraryRange("y", VersionRange.All, LibraryDependencyTarget.Package) } };
            var dependencyInfo = LibraryDependencyInfo.Create(actualIdentity, framework, dependencies);

            var remoteProvider = new Mock<IRemoteDependencyProvider>();
            remoteProvider.Setup(e => e.FindLibraryAsync(range, framework, It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(actualIdentity);
            remoteProvider.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider.SetupGet(e => e.Source).Returns(new PackageSource("test"));

            var hitCount = 0;

            remoteProvider.Setup(e => e.GetDependenciesAsync(actualIdentity, framework, It.IsAny<SourceCacheContext>(), testLogger, token))
                .ThrowsAsync(new PackageNotFoundProtocolException(new PackageIdentity(actualIdentity.Name, actualIdentity.Version)))
                .Callback(() => ++hitCount);

            context.RemoteLibraryProviders.Add(remoteProvider.Object);

            // Act
            await Assert.ThrowsAsync<FatalProtocolException>(async () => await ResolverUtility.FindLibraryEntryAsync(range, framework, null, null, context, token));

            // Assert
            Assert.Equal(2, hitCount);
            Assert.Equal(1, testLogger.Errors);
            string errorMessage = string.Format(CultureInfo.CurrentCulture,
                                                Strings.Error_PackageNotFoundWhenExpected,
                                                remoteProvider.Object.Source,
                                                packageIdentity.ToString());
            Assert.Equal(errorMessage, testLogger.ErrorMessages.Single());
        }

        [Fact]
        public async Task FindPackage_VerifyFindLibraryEntryReturnsOriginalCase()
        {
            // Arrange
            var range = new LibraryRange("x", VersionRange.Parse("1.0.0-beta"), LibraryDependencyTarget.Package);
            var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var context = new RemoteWalkContext(cacheContext, PackageSourceMapping.GetPackageSourceMapping(NullSettings.Instance), testLogger);
            var token = CancellationToken.None;
            var edge = new GraphEdge<RemoteResolveResult>(null, null, null);
            var actualIdentity = new LibraryIdentity("X", NuGetVersion.Parse("1.0.0-bEta"), LibraryType.Package);
            var dependencies = new[] { new LibraryDependency() { LibraryRange = new LibraryRange("y", VersionRange.All, LibraryDependencyTarget.Package) } };
            var dependencyInfo = LibraryDependencyInfo.Create(actualIdentity, framework, dependencies);

            var remoteProvider = new Mock<IRemoteDependencyProvider>();
            remoteProvider.Setup(e => e.FindLibraryAsync(range, framework, cacheContext, testLogger, token))
                .ReturnsAsync(actualIdentity);

            remoteProvider.Setup(e => e.GetDependenciesAsync(actualIdentity, framework, cacheContext, testLogger, token))
                .ReturnsAsync(dependencyInfo);

            context.RemoteLibraryProviders.Add(remoteProvider.Object);

            // Act
            var result = await ResolverUtility.FindLibraryEntryAsync(range, framework, null, null, context, token);

            // Assert
            Assert.Equal(LibraryType.Package, result.Data.Match.Library.Type);
            Assert.Equal("X", result.Data.Match.Library.Name);
            Assert.Equal("1.0.0-bEta", result.Data.Match.Library.Version.ToString());
            Assert.Equal("y", result.Data.Dependencies.Single().Name);
        }

        [Fact]
        public async Task FindPackage_VerifyMissingVersionPackageReturnsUnresolved()
        {
            // Arrange
            var range = new LibraryRange("x", VersionRange.Parse("1.0.0-beta"), LibraryDependencyTarget.Package);
            var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var context = new RemoteWalkContext(cacheContext, PackageSourceMapping.GetPackageSourceMapping(NullSettings.Instance), testLogger);
            var edge = new GraphEdge<RemoteResolveResult>(null, null, null);

            var remoteProvider = new Mock<IRemoteDependencyProvider>();
            context.RemoteLibraryProviders.Add(remoteProvider.Object);

            // Act
            var result = await ResolverUtility.FindLibraryEntryAsync(range, framework, null, null, context, CancellationToken.None);

            // Assert
            Assert.Equal(LibraryType.Unresolved, result.Data.Match.Library.Type);
            Assert.Equal("x", result.Data.Match.Library.Name);
            Assert.Equal("1.0.0-beta", result.Data.Match.Library.Version.ToString());
        }

        [Fact]
        public async Task FindPackage_VerifyPackageSourcesAreFilteredWhenPackageSourceMappingIsEnabled_Success()
        {
            // Arrange
            const string packageX = "x", packageY = "y", version = "1.0.0-beta.1", source1 = "source1", source2 = "source2";
            var range = new LibraryRange(packageX, VersionRange.Parse(version), LibraryDependencyTarget.Package);
            var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var token = CancellationToken.None;
            var edge = new GraphEdge<RemoteResolveResult>(null, null, null);
            var actualIdentity = new LibraryIdentity(packageX, NuGetVersion.Parse(version), LibraryType.Package);
            var dependencies = new[] { new LibraryDependency() { LibraryRange = new LibraryRange(packageY, VersionRange.All, LibraryDependencyTarget.Package) } };
            var dependencyInfo = LibraryDependencyInfo.Create(actualIdentity, framework, dependencies);

            var downloadCount = 0;

            //package source mapping configuration
            Dictionary<string, IReadOnlyList<string>> patterns = new();
            patterns.Add(source2, new List<string>() { packageX });
            patterns.Add(source1, new List<string>() { packageY });
            PackageSourceMapping sourceMappingConfiguration = new(patterns);
            var context = new RemoteWalkContext(cacheContext, sourceMappingConfiguration, testLogger);

            // Source1 returns 1.0.0-beta.1
            var remoteProvider = new Mock<IRemoteDependencyProvider>();
            remoteProvider.Setup(e => e.FindLibraryAsync(range, It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(actualIdentity);
            remoteProvider.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider.SetupGet(e => e.Source).Returns(new PackageSource(source1));
            remoteProvider.Setup(e => e.GetDependenciesAsync(It.IsAny<LibraryIdentity>(), It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(dependencyInfo)
                .Callback(() => ++downloadCount);
            context.RemoteLibraryProviders.Add(remoteProvider.Object);

            // Source2 returns 1.0.0-beta.1
            var remoteProvider2 = new Mock<IRemoteDependencyProvider>();
            remoteProvider2.Setup(e => e.FindLibraryAsync(range, It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(actualIdentity);
            remoteProvider2.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider2.SetupGet(e => e.Source).Returns(new PackageSource(source2));
            remoteProvider2.Setup(e => e.GetDependenciesAsync(It.IsAny<LibraryIdentity>(), It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(dependencyInfo)
                .Callback(() => ++downloadCount);
            context.RemoteLibraryProviders.Add(remoteProvider2.Object);

            // Act
            var result = await ResolverUtility.FindLibraryEntryAsync(range, framework, null, null, context, token);

            // Assert
            // Verify only one download happened from the expected source i.e. source2
            Assert.Equal(1, downloadCount);
            Assert.Equal(1, testLogger.DebugMessages.Count);
            testLogger.DebugMessages.TryPeek(out string message);
            Assert.Equal($"Package source mapping matches found for package ID '{packageX}' are: '{source2}'.", message);
            Assert.Equal(version, result.Key.Version.ToString());
            Assert.Equal(source2, result.Data.Match.Provider.Source.Name);
        }

        [Fact]
        public async Task FindPackage_WhenNoPackageSourceMappingIsEnabledForAPackage_Fails()
        {
            // Arrange
            const string packageX = "x", version = "1.0.0-beta.1";
            var range = new LibraryRange(packageX, VersionRange.Parse(version), LibraryDependencyTarget.Package);
            var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");

            var token = CancellationToken.None;
            var edge = new GraphEdge<RemoteResolveResult>(null, null, null);
            var actualIdentity = new LibraryIdentity(packageX, NuGetVersion.Parse(version), LibraryType.Package);
            var dependencies = new[] { new LibraryDependency() { LibraryRange = new LibraryRange("y", VersionRange.All, LibraryDependencyTarget.Package) } };
            var dependencyInfo = LibraryDependencyInfo.Create(actualIdentity, framework, dependencies);

            var downloadCount = 0;

            //package source mapping configuration
            Dictionary<string, IReadOnlyList<string>> patterns = new();
            patterns.Add("source2", new List<string>() { "z" });
            patterns.Add("source1", new List<string>() { "y" });
            PackageSourceMapping sourceMappingConfiguration = new(patterns);
            var context = new RemoteWalkContext(cacheContext, sourceMappingConfiguration, testLogger);

            // Source1 returns 1.0.0-beta.1
            var remoteProvider = new Mock<IRemoteDependencyProvider>();
            remoteProvider.Setup(e => e.FindLibraryAsync(range, It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(actualIdentity);
            remoteProvider.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider.SetupGet(e => e.Source).Returns(new PackageSource("source1"));
            remoteProvider.Setup(e => e.GetDependenciesAsync(It.IsAny<LibraryIdentity>(), It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(dependencyInfo)
                .Callback(() => ++downloadCount);
            context.RemoteLibraryProviders.Add(remoteProvider.Object);

            // Source2 returns 1.0.0-beta.1
            var remoteProvider2 = new Mock<IRemoteDependencyProvider>();
            remoteProvider2.Setup(e => e.FindLibraryAsync(range, It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(actualIdentity);
            remoteProvider2.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider2.SetupGet(e => e.Source).Returns(new PackageSource("source2"));
            remoteProvider2.Setup(e => e.GetDependenciesAsync(It.IsAny<LibraryIdentity>(), It.IsAny<NuGetFramework>(), It.IsAny<SourceCacheContext>(), testLogger, token))
                .ReturnsAsync(dependencyInfo)
                .Callback(() => ++downloadCount);
            context.RemoteLibraryProviders.Add(remoteProvider2.Object);

            // Act
            var result = await ResolverUtility.FindLibraryEntryAsync(range, framework, null, null, context, token);

            Assert.Equal(0, downloadCount);
            Assert.Equal(0, testLogger.Errors);
            Assert.Equal(1, testLogger.DebugMessages.Count);
            testLogger.DebugMessages.TryPeek(out string message);
            Assert.Equal($"Package source mapping match not found for package ID '{packageX}'.", message);
        }

        [Fact]
        public async Task FindLibraryByVersionAsync_WhenOneHttpSourceHits_DoesNotQueryMissSourceAgain()
        {
            var range = new LibraryRange("x", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package);
            using var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var identity = new LibraryIdentity("x", NuGetVersion.Parse("1.0.0"), LibraryType.Package);
            var token = CancellationToken.None;

            int hitCalls = 0;
            int hitSuppress = 0;
            int missCalls = 0;
            int missUnsuppressed = 0;

            var hit = new Mock<IRemoteDependencyProvider>();
            hit.SetupGet(e => e.IsHttp).Returns(true);
            hit.SetupGet(e => e.Source).Returns(new PackageSource("https://hit.test"));
            hit.Setup(e => e.FindLibraryAsync(range, framework, It.IsAny<SourceCacheContext>(), testLogger, token))
                .Returns<LibraryRange, NuGetFramework, SourceCacheContext, ILogger, CancellationToken>((_, _, ctx, _, _) =>
                {
                    hitCalls++;
                    if (ctx.SuppressHttpCacheRefreshOnMiss)
                    {
                        hitSuppress++;
                    }

                    return Task.FromResult(identity);
                });

            var miss = new Mock<IRemoteDependencyProvider>();
            miss.SetupGet(e => e.IsHttp).Returns(true);
            miss.SetupGet(e => e.Source).Returns(new PackageSource("https://miss.test"));
            miss.Setup(e => e.FindLibraryAsync(range, framework, It.IsAny<SourceCacheContext>(), testLogger, token))
                .Returns<LibraryRange, NuGetFramework, SourceCacheContext, ILogger, CancellationToken>((_, _, ctx, _, _) =>
                {
                    missCalls++;
                    if (!ctx.SuppressHttpCacheRefreshOnMiss)
                    {
                        missUnsuppressed++;
                    }

                    return Task.FromResult<LibraryIdentity>(null);
                });

            var result = await ResolverUtility.FindLibraryByVersionAsync(
                range,
                framework,
                new[] { hit.Object, miss.Object },
                cacheContext,
                testLogger,
                token);

            Assert.Equal("1.0.0", result.Library.Version.ToString());
            Assert.Equal(1, hitCalls);
            Assert.Equal(1, hitSuppress);
            Assert.Equal(1, missCalls);
            Assert.Equal(0, missUnsuppressed);
        }

        [Fact]
        public async Task FindLibraryByVersionAsync_WhenAllHttpSourcesMiss_QueriesEachSourceAgainWithoutSuppress()
        {
            var range = new LibraryRange("x", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package);
            using var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var token = CancellationToken.None;

            int firstSuppress = 0;
            int firstUnsuppressed = 0;
            int secondSuppress = 0;
            int secondUnsuppressed = 0;

            Mock<IRemoteDependencyProvider> Create(Action<bool> onCall)
            {
                var mock = new Mock<IRemoteDependencyProvider>();
                mock.SetupGet(e => e.IsHttp).Returns(true);
                mock.SetupGet(e => e.Source).Returns(new PackageSource("https://miss.test"));
                mock.Setup(e => e.FindLibraryAsync(range, framework, It.IsAny<SourceCacheContext>(), testLogger, token))
                    .Returns<LibraryRange, NuGetFramework, SourceCacheContext, ILogger, CancellationToken>((_, _, ctx, _, _) =>
                    {
                        onCall(ctx.SuppressHttpCacheRefreshOnMiss);
                        return Task.FromResult<LibraryIdentity>(null);
                    });
                return mock;
            }

            var first = Create(suppress => { if (suppress) firstSuppress++; else firstUnsuppressed++; });
            var second = Create(suppress => { if (suppress) secondSuppress++; else secondUnsuppressed++; });

            var result = await ResolverUtility.FindLibraryByVersionAsync(
                range,
                framework,
                new[] { first.Object, second.Object },
                cacheContext,
                testLogger,
                token);

            Assert.Null(result);
            Assert.Equal(1, firstSuppress);
            Assert.Equal(1, firstUnsuppressed);
            Assert.Equal(1, secondSuppress);
            Assert.Equal(1, secondUnsuppressed);
        }

        [Fact]
        public async Task FindLibraryByVersionAsync_WhenSingleHttpSourceMisses_DoesNotSuppressAndQueriesOnce()
        {
            var range = new LibraryRange("x", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package);
            using var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var token = CancellationToken.None;

            int calls = 0;
            int suppressed = 0;

            var remote = new Mock<IRemoteDependencyProvider>();
            remote.SetupGet(e => e.IsHttp).Returns(true);
            remote.SetupGet(e => e.Source).Returns(new PackageSource("https://one.test"));
            remote.Setup(e => e.FindLibraryAsync(range, framework, It.IsAny<SourceCacheContext>(), testLogger, token))
                .Returns<LibraryRange, NuGetFramework, SourceCacheContext, ILogger, CancellationToken>((_, _, ctx, _, _) =>
                {
                    calls++;
                    if (ctx.SuppressHttpCacheRefreshOnMiss)
                    {
                        suppressed++;
                    }

                    return Task.FromResult<LibraryIdentity>(null);
                });

            var result = await ResolverUtility.FindLibraryByVersionAsync(
                range,
                framework,
                new[] { remote.Object },
                cacheContext,
                testLogger,
                token);

            Assert.Null(result);
            Assert.Equal(1, calls);
            Assert.Equal(0, suppressed);
        }

        [Fact]
        public async Task FindLibraryByVersionAsync_WhenSingleHttpSourceHits_QueriesOnceWithoutSuppress()
        {
            var range = new LibraryRange("x", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package);
            using var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var identity = new LibraryIdentity("x", NuGetVersion.Parse("1.0.0"), LibraryType.Package);
            var token = CancellationToken.None;

            int calls = 0;
            int suppressed = 0;

            var remote = new Mock<IRemoteDependencyProvider>();
            remote.SetupGet(e => e.IsHttp).Returns(true);
            remote.SetupGet(e => e.Source).Returns(new PackageSource("https://one.test"));
            remote.Setup(e => e.FindLibraryAsync(range, framework, It.IsAny<SourceCacheContext>(), testLogger, token))
                .Returns<LibraryRange, NuGetFramework, SourceCacheContext, ILogger, CancellationToken>((_, _, ctx, _, _) =>
                {
                    calls++;
                    if (ctx.SuppressHttpCacheRefreshOnMiss)
                    {
                        suppressed++;
                    }

                    return Task.FromResult(identity);
                });

            var result = await ResolverUtility.FindLibraryByVersionAsync(
                range,
                framework,
                new[] { remote.Object },
                cacheContext,
                testLogger,
                token);

            Assert.Equal("1.0.0", result.Library.Version.ToString());
            Assert.Equal(1, calls);
            Assert.Equal(0, suppressed);
        }

        [Fact]
        public async Task FindLibraryByVersionAsync_WhenOneHttpAndOneLocalMiss_QueriesHttpOnceWithoutSuppress()
        {
            var range = new LibraryRange("x", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package);
            using var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var token = CancellationToken.None;

            int httpCalls = 0;
            int httpSuppressed = 0;
            int localCalls = 0;

            var local = new Mock<IRemoteDependencyProvider>();
            local.SetupGet(e => e.IsHttp).Returns(false);
            local.SetupGet(e => e.Source).Returns(new PackageSource(@"C:\pkgs"));
            local.Setup(e => e.FindLibraryAsync(range, framework, It.IsAny<SourceCacheContext>(), testLogger, token))
                .Returns<LibraryRange, NuGetFramework, SourceCacheContext, ILogger, CancellationToken>((_, _, _, _, _) =>
                {
                    localCalls++;
                    return Task.FromResult<LibraryIdentity>(null);
                });

            var http = new Mock<IRemoteDependencyProvider>();
            http.SetupGet(e => e.IsHttp).Returns(true);
            http.SetupGet(e => e.Source).Returns(new PackageSource("https://one.test"));
            http.Setup(e => e.FindLibraryAsync(range, framework, It.IsAny<SourceCacheContext>(), testLogger, token))
                .Returns<LibraryRange, NuGetFramework, SourceCacheContext, ILogger, CancellationToken>((_, _, ctx, _, _) =>
                {
                    httpCalls++;
                    if (ctx.SuppressHttpCacheRefreshOnMiss)
                    {
                        httpSuppressed++;
                    }

                    return Task.FromResult<LibraryIdentity>(null);
                });

            var result = await ResolverUtility.FindLibraryByVersionAsync(
                range,
                framework,
                new[] { local.Object, http.Object },
                cacheContext,
                testLogger,
                token);

            Assert.Null(result);
            Assert.Equal(1, localCalls);
            Assert.Equal(1, httpCalls);
            Assert.Equal(0, httpSuppressed);
        }
    }
}
