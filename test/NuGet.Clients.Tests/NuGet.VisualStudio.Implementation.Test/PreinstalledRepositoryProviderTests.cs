﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Win32;
using Moq;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test
{
    public class PreinstalledRepositoryProviderTests : IDisposable
    {
        private readonly TestDirectory _testDirectory;

        public PreinstalledRepositoryProviderTests()
        {
            _testDirectory = TestDirectory.Create();
        }

        public void Dispose()
        {
            _testDirectory.Dispose();
        }

        [Fact]
        public void AddFromRegistry_WithValidRegistryValue_Succeeds()
        {
            var srp = Mock.Of<ISourceRepositoryProvider>();
            Mock.Get(srp)
                .Setup(x => x.CreateRepository(It.IsAny<PackageSource>(), It.IsAny<FeedType>()))
                .Returns((PackageSource ps, FeedType ft) => new SourceRepository(
                    ps, Enumerable.Empty<INuGetResourceProvider>()));

            var provider = new PreinstalledRepositoryProvider(_ => { }, srp);

            // Act
            using (var tc = new TestContext(_testDirectory.Path))
            {
                provider.AddFromRegistry(tc.RepoKeyName, isPreUnzipped: true);
            }

            Assert.NotNull(provider.GetRepositories().Single());

            Mock.Get(srp)
                .Verify(
                    x => x.CreateRepository(It.IsAny<PackageSource>(), It.IsAny<FeedType>()),
                    Times.Once());
        }

        [Fact]
        public void AddFromRegistry_WithMissingRegistryKey_Fails()
        {
            var srp = Mock.Of<ISourceRepositoryProvider>();

            string errorHandlerMessage = null;
            var provider = new PreinstalledRepositoryProvider(
                registryKeyRoot: $"NuGetTest_{Guid.NewGuid()}",
                errorHandler: errorMessage => { errorHandlerMessage = errorMessage; }, 
                provider: srp);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() =>
                provider.AddFromRegistry("InvalidKeyName", isPreUnzipped: true)
            );

            // VsResources.PreinstalledPackages_RegistryKeyError
            const string ExpectedErrorMessage = "error accessing registry key";

            Assert.Contains(ExpectedErrorMessage, errorHandlerMessage,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(ExpectedErrorMessage, exception.Message,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AddFromRegistry_WithInvalidRegistryValue_Fails()
        {
            var srp = Mock.Of<ISourceRepositoryProvider>();

            string errorHandlerMessage = null;
            var provider = new PreinstalledRepositoryProvider(
                errorMessage => { errorHandlerMessage = errorMessage; }, srp);

            // Act
            using (var tc = new TestContext(string.Empty))
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    provider.AddFromRegistry(tc.RepoKeyName, isPreUnzipped: true)
                );

                // VsResources.PreinstalledPackages_InvalidRegistryValue
                const string ExpectedErrorMessage = "Could not find a registry key with name";

                Assert.Contains(ExpectedErrorMessage, errorHandlerMessage,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains(ExpectedErrorMessage, exception.Message,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private class TestContext : IDisposable
        {
            private readonly RegistryKey _repoKey;
            public string RepoKeyName { get; } = $"NuGetTest_{Guid.NewGuid()}";

            public TestContext(string repositoryPath)
            {
                _repoKey = Registry.CurrentUser.CreateSubKey(
                    PreinstalledRepositoryProvider.DefaultRegistryKeyRoot);
                _repoKey.SetValue(RepoKeyName, repositoryPath);
            }

            public void Dispose()
            {
                _repoKey.DeleteValue(RepoKeyName);
                _repoKey.Dispose();
            }

        }
    }
}
