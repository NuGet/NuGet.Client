// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.FuncTest
{
    public class FindPackageByIdResourceTests
    {
        [PackageSourceTheory]
        [PackageSourceData(TestSources.ProGet, TestSources.Klondike, TestSources.Artifactory, TestSources.MyGet)]
        public async Task FindPackageByIdResource_NormalizedVersion(string packageSource)
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(packageSource);
            var findPackageByIdResource = await repo.GetResourceAsync<FindPackageByIdResource>();
            var logger = new TestLogger();

            using (var context = new SourceCacheContext())
            {
                context.NoCache = true;

                // Act
                var packages = await findPackageByIdResource.GetAllVersionsAsync(
                    "owin",
                    context,
                    logger,
                    CancellationToken.None);

                // Assert
                Assert.Single(packages);
                Assert.Equal("1.0", packages.Single().ToString());
            }
        }

        [PackageSourceTheory]
        [PackageSourceData(TestSources.ProGet, TestSources.Klondike, TestSources.Artifactory, TestSources.MyGet)]
        public async Task FindPackageByIdResource_NoDependencyVersion(string packageSource)
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(packageSource);
            var findPackageByIdResource = await repo.GetResourceAsync<FindPackageByIdResource>();
            var logger = new TestLogger();

            using (var context = new SourceCacheContext())
            {
                context.NoCache = true;

                // Act
                var packages = await findPackageByIdResource.GetAllVersionsAsync(
                    "costura.fody",
                    context,
                    logger,
                    CancellationToken.None);

                // Assert
                Assert.Single(packages);
                Assert.Equal("1.3.3.0", packages.Single().ToString());
            }
        }

        [PackageSourceTheory]
        [PackageSourceData(TestSources.ProGet, TestSources.Klondike, TestSources.Artifactory, TestSources.MyGet)]
        public async Task FindPackageByIdResource_Basic(string packageSource)
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(packageSource);
            var findPackageByIdResource = await repo.GetResourceAsync<FindPackageByIdResource>();
            var logger = new TestLogger();

            using (var context = new SourceCacheContext())
            {
                context.NoCache = true;

                // Act
                var packages = await findPackageByIdResource.GetAllVersionsAsync(
                    "Newtonsoft.json",
                    context,
                    logger,
                    CancellationToken.None);

                // Assert
                Assert.Single(packages);
                Assert.Equal("8.0.3", packages.Single().ToString());
            }
        }

        [PackageSourceTheory]
        [PackageSourceData(TestSources.NuGetServer, TestSources.VSTS)]
        public async Task FindPackageByIdResource_Credential(PackageSource packageSource)
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV2(packageSource);
            var findPackageByIdResource = await repo.GetResourceAsync<FindPackageByIdResource>();
            var logger = new TestLogger();

            using (var context = new SourceCacheContext())
            {
                context.NoCache = true;

                // Act
                var packages = await findPackageByIdResource.GetAllVersionsAsync(
                    "Newtonsoft.json",
                    context,
                    logger,
                    CancellationToken.None);

                // Assert
                Assert.Single(packages);
                Assert.Equal("8.0.3", packages.Single().ToString());
            }
        }

        [PackageSourceTheory]
        [PackageSourceData(TestSources.NuGetServer, TestSources.VSTS)]
        public async Task FindPackageByIdResource_CredentialNoDependencyVersion(PackageSource packageSource)
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV2(packageSource);
            var findPackageByIdResource = await repo.GetResourceAsync<FindPackageByIdResource>();
            var logger = new TestLogger();

            using (var context = new SourceCacheContext())
            {
                context.NoCache = true;

                // Act
                var packages = await findPackageByIdResource.GetAllVersionsAsync(
                    "costura.fody",
                    context,
                    logger,
                    CancellationToken.None);

                // Assert
                Assert.Single(packages);
                Assert.Equal("1.3.3.0", packages.Single().ToString());
            }
        }

        [PackageSourceTheory]
        [PackageSourceData(TestSources.NuGetServer, TestSources.VSTS)]
        public async Task FindPackageByIdResource_CredentialNormalizedVersion(PackageSource packageSource)
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV2(packageSource);
            var findPackageByIdResource = await repo.GetResourceAsync<FindPackageByIdResource>();
            var logger = new TestLogger();

            using (var context = new SourceCacheContext())
            {
                context.NoCache = true;

                // Act
                var packages = await findPackageByIdResource.GetAllVersionsAsync(
                    "owin",
                    context,
                    logger,
                    CancellationToken.None);

                // Assert
                Assert.Single(packages);
                Assert.Equal("1.0", packages.Single().ToString());
            }
        }
    }
}
