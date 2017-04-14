// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
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
            var context = new RemoteWalkContext(cacheContext, testLogger);
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
            var result = await ResolverUtility.FindLibraryEntryAsync(range, framework, edge, context, token);

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
            var context = new RemoteWalkContext(cacheContext, testLogger);
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
            var result = await ResolverUtility.FindLibraryEntryAsync(range, framework, edge, context, token);

            // Assert
            Assert.Equal(1, hitCount);
            Assert.Equal("x", result.Key.Name);
        }

        [Fact]
        public async Task FindPackage_VerifyMissingListedPackageThrowsNotFound()
        {
            // Arrange
            var range = new LibraryRange("x", VersionRange.Parse("1.0.0-beta"), LibraryDependencyTarget.Package);
            var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var context = new RemoteWalkContext(cacheContext, testLogger);
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

            remoteProvider.Setup(e => e.GetDependenciesAsync(actualIdentity, framework, It.IsAny<SourceCacheContext>(), testLogger, token))
                .ThrowsAsync(new PackageNotFoundProtocolException(new PackageIdentity(actualIdentity.Name, actualIdentity.Version)))
                .Callback(() => ++hitCount);

            context.RemoteLibraryProviders.Add(remoteProvider.Object);

            // Act
            await Assert.ThrowsAsync<FatalProtocolException>(async () => await ResolverUtility.FindLibraryEntryAsync(range, framework, edge, context, token));

            // Assert
            Assert.Equal(2, hitCount);
        }

        [Fact]
        public async Task FindPackage_VerifyFindLibraryEntryReturnsOriginalCase()
        {
            // Arrange
            var range = new LibraryRange("x", VersionRange.Parse("1.0.0-beta"), LibraryDependencyTarget.Package);
            var cacheContext = new SourceCacheContext();
            var testLogger = new TestLogger();
            var framework = NuGetFramework.Parse("net45");
            var context = new RemoteWalkContext(cacheContext, testLogger);
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
            var result = await ResolverUtility.FindLibraryEntryAsync(range, framework, edge, context, token);

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
            var context = new RemoteWalkContext(cacheContext, testLogger);
            var edge = new GraphEdge<RemoteResolveResult>(null, null, null);

            var remoteProvider = new Mock<IRemoteDependencyProvider>();
            context.RemoteLibraryProviders.Add(remoteProvider.Object);

            // Act
            var result = await ResolverUtility.FindLibraryEntryAsync(range, framework, edge, context, CancellationToken.None);

            // Assert
            Assert.Equal(LibraryType.Unresolved, result.Data.Match.Library.Type);
            Assert.Equal("x", result.Data.Match.Library.Name);
            Assert.Equal("1.0.0-beta", result.Data.Match.Library.Version.ToString());
        }
    }
}
