// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.FuncTest
{
    public class FindPackageByIdResourceTests
    {
        [CIOnlyTheory]
        [InlineData(TestServers.ProGet)]
        [InlineData(TestServers.Klondike)]
        //[InlineData(TestServers.Artifactory)]
        [InlineData(TestServers.MyGet)]
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
                Assert.Equal(1, packages.Count());
                Assert.Equal("1.0", packages.FirstOrDefault().ToString());
            }
        }

        [CIOnlyTheory]
        [InlineData(TestServers.ProGet)]
        [InlineData(TestServers.Klondike)]
        //[InlineData(TestServers.Artifactory)]
        [InlineData(TestServers.MyGet)]
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
                Assert.Equal(1, packages.Count());
                Assert.Equal("1.3.3.0", packages.FirstOrDefault().ToString());
            }
        }

        [CIOnlyTheory]
        [InlineData(TestServers.ProGet)]
        [InlineData(TestServers.Klondike)]
        //[InlineData(TestServers.Artifactory)]
        [InlineData(TestServers.MyGet)]
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
                Assert.Equal(1, packages.Count());
                Assert.Equal("8.0.3", packages.FirstOrDefault().ToString());
            }
        }

        [CIOnlyTheory]
        [InlineData(TestServers.NuGetServer, "NuGetServer")]
        [InlineData(TestServers.Vsts, "Vsts")]
        public async Task FindPackageByIdResource_Credential(string packageSource, string feedName)
        {
            // Arrange
            var credential = Utility.ReadCredential(feedName);
            var source = new PackageSource(packageSource);
            var sourceCredential = new PackageSourceCredential(packageSource, credential.Item1, credential.Item2, true);
            source.Credentials = sourceCredential;
            var repo = Repository.Factory.GetCoreV2(source);
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
                Assert.Equal(1, packages.Count());
                Assert.Equal("8.0.3", packages.FirstOrDefault().ToString());
            }
        }

        [CIOnlyTheory]
        [InlineData(TestServers.NuGetServer, "NuGetServer")]
        [InlineData(TestServers.Vsts, "Vsts")]
        public async Task FindPackageByIdResource_CredentialNoDependencyVersion(string packageSource, string feedName)
        {
            // Arrange
            var credential = Utility.ReadCredential(feedName);
            var source = new PackageSource(packageSource);
            var sourceCredential = new PackageSourceCredential(packageSource, credential.Item1, credential.Item2, true);
            source.Credentials = sourceCredential;
            var repo = Repository.Factory.GetCoreV2(source);
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
                Assert.Equal(1, packages.Count());
                Assert.Equal("1.3.3.0", packages.FirstOrDefault().ToString());
            }
        }

        [CIOnlyTheory]
        [InlineData(TestServers.NuGetServer, "NuGetServer")]
        [InlineData(TestServers.Vsts, "Vsts")]
        public async Task FindPackageByIdResource_CredentialNormalizedVersion(string packageSource, string feedName)
        {
            // Arrange
            var credential = Utility.ReadCredential(feedName);
            var source = new PackageSource(packageSource);
            var sourceCredential = new PackageSourceCredential(packageSource, credential.Item1, credential.Item2, true);
            source.Credentials = sourceCredential;
            var repo = Repository.Factory.GetCoreV2(source);
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
                Assert.Equal(1, packages.Count());
                Assert.Equal("1.0", packages.FirstOrDefault().ToString());
            }
        }
    }
}
