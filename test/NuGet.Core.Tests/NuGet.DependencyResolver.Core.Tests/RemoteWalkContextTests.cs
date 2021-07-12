// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Configuration;
using NuGet.LibraryModel;
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
            var remoteProvider1 = new Mock<IRemoteDependencyProvider>();
            remoteProvider1.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider1.SetupGet(e => e.Source).Returns(new PackageSource("Source1"));
            context.RemoteLibraryProviders.Add(remoteProvider1.Object);

            // Source2
            var remoteProvider2 = new Mock<IRemoteDependencyProvider>();
            remoteProvider2.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider2.SetupGet(e => e.Source).Returns(new PackageSource("Source2"));
            context.RemoteLibraryProviders.Add(remoteProvider2.Object);

            var libraryRange = new LibraryRange("packageA", Versioning.VersionRange.None, LibraryDependencyTarget.Package);

            IList<IRemoteDependencyProvider> providers = context.FilterDependencyProvidersForLibrary(libraryRange);

            Assert.Equal(2, providers.Count);
            Assert.Equal(context.RemoteLibraryProviders, providers);
        }

        [Fact]
        public void FilterDependencyProvidersForLibrary_WhenPackageNamespacesAreConfiguredReturnsOnlyApplicableProviders_Success()
        {
            var context = new TestRemoteWalkContext();

            // Source1
            var remoteProvider1 = new Mock<IRemoteDependencyProvider>();
            remoteProvider1.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider1.SetupGet(e => e.Source).Returns(new PackageSource("Source1"));
            context.RemoteLibraryProviders.Add(remoteProvider1.Object);

            // Source2
            var remoteProvider2 = new Mock<IRemoteDependencyProvider>();
            remoteProvider2.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider2.SetupGet(e => e.Source).Returns(new PackageSource("Source2"));
            context.RemoteLibraryProviders.Add(remoteProvider2.Object);

            var libraryRange = new LibraryRange("x", Versioning.VersionRange.None, LibraryDependencyTarget.Package);

            //package namespaces configuration
            Dictionary<string, IReadOnlyList<string>> namespaces = new();
            namespaces.Add("Source1", new List<string>() { "x" });
            namespaces.Add("Source2", new List<string>() { "y" });
            PackageNamespacesConfiguration namespacesConfiguration = new(namespaces);
            context.PackageNamespaces = namespacesConfiguration;

            IList<IRemoteDependencyProvider> providers = context.FilterDependencyProvidersForLibrary(libraryRange);

            Assert.Equal(1, providers.Count);
            Assert.Equal("Source1", providers[0].Source.Name);
        }

        [Fact]
        public void FilterDependencyProvidersForLibrary_WhenPackageNamespacesAreConfiguredReturnsAndNoMatchingSourceFound_Throws()
        {
            var context = new TestRemoteWalkContext();

            // Source1
            var remoteProvider1 = new Mock<IRemoteDependencyProvider>();
            remoteProvider1.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider1.SetupGet(e => e.Source).Returns(new PackageSource("Source1"));
            context.RemoteLibraryProviders.Add(remoteProvider1.Object);

            // Source2
            var remoteProvider2 = new Mock<IRemoteDependencyProvider>();
            remoteProvider2.SetupGet(e => e.IsHttp).Returns(true);
            remoteProvider2.SetupGet(e => e.Source).Returns(new PackageSource("Source2"));
            context.RemoteLibraryProviders.Add(remoteProvider2.Object);

            var libraryRange = new LibraryRange("x", Versioning.VersionRange.None, LibraryDependencyTarget.Package);

            //package namespaces configuration
            Dictionary<string, IReadOnlyList<string>> namespaces = new();
            namespaces.Add("Source1", new List<string>() { "y" });
            namespaces.Add("Source2", new List<string>() { "z" });
            PackageNamespacesConfiguration namespacesConfiguration = new(namespaces);
            context.PackageNamespaces = namespacesConfiguration;

            var exception = Assert.Throws<Exception>(() => context.FilterDependencyProvidersForLibrary(libraryRange));

            Assert.Equal("Package Namespaces are configured but no matching source found for 'x' package. Update the namespaces configuration and run the restore again.", exception.Message);
        }
    }
}
