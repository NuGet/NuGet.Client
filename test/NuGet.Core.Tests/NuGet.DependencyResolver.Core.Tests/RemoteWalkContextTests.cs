// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.LibraryModel;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.DependencyResolver.Core.Tests
{
    public class RemoteWalkContextTests
    {
        [Fact]
        public void FilterDependencyProvidersForLibrary_WhenLibraryRangeIsNull_Throws()
        {
            var context = new TestRemoteWalkContext();

            Assert.Throws<ArgumentNullException>(() => context.FilterDependencyProvidersForLibrary(libraryRange: null));
        }

        [Fact]
        public void FilterDependencyProvidersForLibrary_WhenPackageNamespacesAreNotConfiguredReturnsAllProviders_Success()
        {
            var context = new TestRemoteWalkContext();

            // Source1
            var remoteProvider1 = CreateRemoteDependencyProvider("Source1");
            context.RemoteLibraryProviders.Add(remoteProvider1.Object);

            // Source2
            var remoteProvider2 = CreateRemoteDependencyProvider("Source2");
            context.RemoteLibraryProviders.Add(remoteProvider2.Object);

            var libraryRange = new LibraryRange("packageA", Versioning.VersionRange.None, LibraryDependencyTarget.Package);

            IList<IRemoteDependencyProvider> providers = context.FilterDependencyProvidersForLibrary(libraryRange);

            Assert.Equal(2, providers.Count);
            Assert.Equal(context.RemoteLibraryProviders, providers);
        }

        [Fact]
        public void FilterDependencyProvidersForLibrary_WhenPackageNamespacesAreConfiguredReturnsOnlyApplicableProviders_Success()
        {
            //package namespaces configuration
            Dictionary<string, IReadOnlyList<string>> namespaces = new();
            namespaces.Add("Source1", new List<string>() { "x" });
            namespaces.Add("Source2", new List<string>() { "y" });
            PackageNamespacesConfiguration namespacesConfiguration = new(namespaces);

            var context = new TestRemoteWalkContext(namespacesConfiguration, NullLogger.Instance);

            // Source1
            var remoteProvider1 = CreateRemoteDependencyProvider("Source1");
            context.RemoteLibraryProviders.Add(remoteProvider1.Object);

            // Source2
            var remoteProvider2 = CreateRemoteDependencyProvider("Source2");
            context.RemoteLibraryProviders.Add(remoteProvider2.Object);

            var libraryRange = new LibraryRange("x", Versioning.VersionRange.None, LibraryDependencyTarget.Package);

            IList<IRemoteDependencyProvider> providers = context.FilterDependencyProvidersForLibrary(libraryRange);

            Assert.Equal(1, providers.Count);
            Assert.Equal("Source1", providers[0].Source.Name);
        }

        [Fact]
        public void FilterDependencyProvidersForLibrary_WhenPackageNamespaceToSourceMappingIsNotConfiguredReturnsNoProviders_Success()
        {
            var logger = new TestLogger();

            //package namespaces configuration
            Dictionary<string, IReadOnlyList<string>> namespaces = new();
            namespaces.Add("Source1", new List<string>() { "y" });
            namespaces.Add("Source2", new List<string>() { "z" });
            PackageNamespacesConfiguration namespacesConfiguration = new(namespaces);

            var context = new TestRemoteWalkContext(namespacesConfiguration, logger);

            // Source1
            var remoteProvider1 = CreateRemoteDependencyProvider("Source1");
            context.RemoteLibraryProviders.Add(remoteProvider1.Object);

            // Source2
            var remoteProvider2 = CreateRemoteDependencyProvider("Source2");
            context.RemoteLibraryProviders.Add(remoteProvider2.Object);

            var libraryRange = new LibraryRange("x", Versioning.VersionRange.None, LibraryDependencyTarget.Package);

            IList<IRemoteDependencyProvider> providers = context.FilterDependencyProvidersForLibrary(libraryRange);

            Assert.Empty(providers);
        }

        private Mock<IRemoteDependencyProvider> CreateRemoteDependencyProvider(string source)
        {
            var remoteProvider = new Mock<IRemoteDependencyProvider>();
            remoteProvider.SetupGet(e => e.Source).Returns(new PackageSource(source));

            return remoteProvider;
        }
    }
}
