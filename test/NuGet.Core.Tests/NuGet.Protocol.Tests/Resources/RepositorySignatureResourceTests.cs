// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class RepositorySignatureResourceTests
    {
        private const string _fingerprint = "3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece";
        private const string _subject = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";
        private const string _contentUrl = "https://api.nuget.org/v3-index/repository-signatures/certificates/3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece.crt";
        private const string _issuer = "CN=DigiCert SHA2 Assured ID Code Signing CA, OU=www.digicert.com, O=DigiCert Inc, C=US";
        private const string _notAfter = "2021-01-27T12:00:00.0000000Z";
        private const string _notBefore = "2018-02-26T00:00:00.0000000Z";

        [Fact]
        public async Task RepositorySignatureResource_Basic()
        {
            // Arrange
            var source = $"http://unit.test/{Guid.NewGuid()}/v3-with-flat-container/index.json";
            var responses = new Dictionary<string, string>();
            responses.Add(source, JsonData.RepoSignIndexJsonData);
            responses.Add("https://api.nuget.org/v3-index/repository-signatures/index.json", JsonData.RepoSignData);

            var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);

            // Act
            var repositorySignatureResource = await repo.GetResourceAsync<RepositorySignatureResource>();

            // Assert
            Assert.False(repositorySignatureResource.AllRepositorySigned);
            Assert.NotNull(repositorySignatureResource.RepositoryCertificateInfos);
            Assert.Equal(1, repositorySignatureResource.RepositoryCertificateInfos.Count());

            var certInfor = repositorySignatureResource.RepositoryCertificateInfos.FirstOrDefault();
            VerifyCertInfo(certInfor);
        }

        [Fact]
        public async Task RepositorySignatureResource_WithoutReposignEndpoint()
        {
            // Arrange
            var source = $"http://unit.test{Guid.NewGuid()}/v3-with-flat-container/index.json";
            var responses = new Dictionary<string, string>();
            responses.Add(source, JsonData.IndexWithFlatContainer);

            var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);

            // Act
            var repositorySignatureResource = await repo.GetResourceAsync<RepositorySignatureResource>();

            // Assert
            Assert.Null(repositorySignatureResource);
        }

        [Fact]
        public void RepositorySignatureResource_NoAllRepositorySigned()
        {
            // Arrange
            var source = "http://unit.test1/v3-with-flat-container/index.json";
            var responses = new Dictionary<string, string>();
            var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);
            var jobject = JObject.Parse(JsonData.RepoSignDataNoAllRepositorySigned);


            // Act & Assert
            Assert.Throws<FatalProtocolException>(() => new RepositorySignatureResource(jobject, repo));
        }

        [Fact]
        public void RepositorySignatureResource_NoCertInfo()
        {
            // Arrange
            var source = "http://unit.test1/v3-with-flat-container/index.json";
            var responses = new Dictionary<string, string>();
            var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);
            var jobject = JObject.Parse(JsonData.RepoSignDataNoCertInfo);

            // Act & Assert
            Assert.Throws<FatalProtocolException>(() => new RepositorySignatureResource(jobject, repo));
        }

        [Fact]
        public async Task RepositorySignatureResource_RepositorySignatureInfo()
        {
            // Arrange
            var source = $"http://unit.test/{Guid.NewGuid()}/v3-with-flat-container/index.json";
            var responses = new Dictionary<string, string>();
            responses.Add(source, JsonData.RepoSignIndexJsonData);
            responses.Add("https://api.nuget.org/v3-index/repository-signatures/index.json", JsonData.RepoSignData);

            var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);

            // Act
            var repositorySignatureResource = await repo.GetResourceAsync<RepositorySignatureResource>();
            repositorySignatureResource.UpdateRepositorySignatureInfo();

            // Assert

            RepositorySignatureInfoProvider.Instance.TryGetRepositorySignatureInfo(source, out var repositorySignatureInfo);

            Assert.False(repositorySignatureInfo.AllRepositorySigned);
            Assert.NotNull(repositorySignatureInfo.RepositoryCertificateInfos);
            Assert.Equal(1, repositorySignatureInfo.RepositoryCertificateInfos.Count());

            var certInfo = repositorySignatureInfo.RepositoryCertificateInfos.FirstOrDefault();
            VerifyCertInfo(certInfo);
        }

        [Fact]
        public async Task RepositorySignatureResource_RepositorySignatureInfoConcurrency()
        {
            // Arrange
            var source1 = $"http://unit.test/{Guid.NewGuid()}1/v3-with-flat-container/index.json";
            var source2 = $"http://unit.test/{Guid.NewGuid()}2/v3-with-flat-container/index.json";
            var source3 = $"http://unit.test/{Guid.NewGuid()}3/v3-with-flat-container/index.json";

            var sources = new List<string>() { source1, source2, source3 };

            var responses = new Dictionary<string, string>();
            responses.Add(source1, JsonData.RepoSignIndexJsonData);
            responses.Add(source2, JsonData.RepoSignIndexJsonData);
            responses.Add(source3, JsonData.RepoSignIndexJsonData);

            responses.Add("https://api.nuget.org/v3-index/repository-signatures/index.json", JsonData.RepoSignData);

            var repos = sources.Select(p => StaticHttpHandler.CreateSource(p, Repository.Provider.GetCoreV3(), responses));

            // Act
            var findPackageByIdResourceTasks = repos.Select(p => p.GetResourceAsync<FindPackageByIdResource>());

            await Task.WhenAll(findPackageByIdResourceTasks);

            // Assert
            RepositorySignatureInfoProvider.Instance.TryGetRepositorySignatureInfo(source1, out var repositorySignatureInfo1);
            RepositorySignatureInfoProvider.Instance.TryGetRepositorySignatureInfo(source2, out var repositorySignatureInfo2);
            RepositorySignatureInfoProvider.Instance.TryGetRepositorySignatureInfo(source3, out var repositorySignatureInfo3);

            var repositorySignatureInfos = new List<RepositorySignatureInfo>() { repositorySignatureInfo1, repositorySignatureInfo2, repositorySignatureInfo3 };

            Assert.True(repositorySignatureInfos.All(p => p.AllRepositorySigned == false));
            Assert.True(repositorySignatureInfos.All(p => p.RepositoryCertificateInfos != null));
            Assert.True(repositorySignatureInfos.All(p => p.RepositoryCertificateInfos.Count() == 1));

            repositorySignatureInfos.ForEach(p => VerifyCertInfo(p.RepositoryCertificateInfos.FirstOrDefault()));
        }

        public static RepositorySignatureResource GetRepositorySignatureResource()
        {
            var certInfo = new Mock<IRepositoryCertificateInfo>();

            var fingerPrints = new Dictionary<string, string>() { { "2.16.840.1.101.3.4.2.1", _fingerprint } };

            certInfo.SetupGet(p => p.Issuer).Returns(_issuer);
            certInfo.SetupGet(p => p.Fingerprints).Returns(new Fingerprints(fingerPrints));
            certInfo.SetupGet(p => p.NotBefore).Returns(DateTime.Parse(_notBefore));
            certInfo.SetupGet(p => p.NotAfter).Returns(DateTime.Parse(_notAfter));
            certInfo.SetupGet(p => p.Subject).Returns(_subject);
            certInfo.SetupGet(p => p.ContentUrl).Returns(_contentUrl);

            var certInfos = new List<IRepositoryCertificateInfo>() { certInfo.Object };
            return new RepositorySignatureResource(allRepositorySigned: false, repositoryCertInfos: certInfos);
        }

        public static void VerifyCertInfo(IRepositoryCertificateInfo certInfo)
        {
            Assert.Equal(_fingerprint, certInfo.Fingerprints["2.16.840.1.101.3.4.2.1"]);
            Assert.Equal(_issuer, certInfo.Issuer);
            Assert.Equal(_subject, certInfo.Subject);
            Assert.Equal(_contentUrl, certInfo.ContentUrl);
            Assert.Equal(_notAfter, certInfo.NotAfter.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            Assert.Equal(_notBefore, certInfo.NotBefore.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
        }
    }
}
