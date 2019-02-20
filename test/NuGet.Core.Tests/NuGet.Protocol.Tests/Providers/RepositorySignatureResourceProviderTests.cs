// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests.Providers
{
    public class RepositorySignatureResourceProviderTests
    {
        private const string _resourceType470 = "RepositorySignatures/4.7.0";
        private const string _resourceType490 = "RepositorySignatures/4.9.0";
        private const string _resourceType500 = "RepositorySignatures/5.0.0";
        private const string _resourceUri470 = "https://unit.test/4.7.0";
        private const string _resourceUri490 = "https://unit.test/4.9.0";
        private const string _resourceUri500 = "https://unit.test/5.0.0";

        private static readonly SemanticVersion _defaultVersion = new SemanticVersion(0, 0, 0);

        private readonly PackageSource _packageSource;
        private readonly RepositorySignatureResourceProvider _repositorySignatureResourceProvider;

        public RepositorySignatureResourceProviderTests()
        {
            _packageSource = new PackageSource("https://unit.test");
            _repositorySignatureResourceProvider = new RepositorySignatureResourceProvider();
        }

        [Fact]
        public async Task TryCreate_WhenResourceDoesNotExist_ReturnsNoResource()
        {
            var resourceProviders = new ResourceProvider[]
            {
                CreateServiceIndexResourceV3Provider(),
                _repositorySignatureResourceProvider
            };
            var sourceRepository = new SourceRepository(_packageSource, resourceProviders);

            var result = await _repositorySignatureResourceProvider.TryCreate(sourceRepository, CancellationToken.None);

            Assert.False(result.Item1);
            Assert.Null(result.Item2);
        }

        [Theory]
        [InlineData("http://unit.test/4.9.0", _resourceType490)]
        [InlineData(@"\\localhost\unit\test\4.9.0", _resourceType490)]
        public async Task TryCreate_WhenUrlIsInvalid_Throws(string resourceUrl, string resourceType)
        {
            var serviceEntry = new ServiceIndexEntry(new Uri(resourceUrl), resourceType, _defaultVersion);
            var resourceProviders = new ResourceProvider[]
            {
                CreateServiceIndexResourceV3Provider(serviceEntry),
                StaticHttpSource.CreateHttpSource(
                    new Dictionary<string, string>()
                    {
                        { serviceEntry.Uri.AbsoluteUri, GetRepositorySignaturesResourceJson(resourceUrl) }
                    }),
                _repositorySignatureResourceProvider
            };
            var sourceRepository = new SourceRepository(_packageSource, resourceProviders);

            var exception = await Assert.ThrowsAsync<FatalProtocolException>(
                () => _repositorySignatureResourceProvider.TryCreate(sourceRepository, CancellationToken.None));

            Assert.Equal($"Repository Signatures resouce must be served over HTTPS. Source: {_packageSource.Source}", exception.Message);
        }

        [Theory]
        [InlineData(_resourceUri500, _resourceType500)]
        [InlineData(_resourceUri490, _resourceType490)]
        [InlineData(_resourceUri470, _resourceType470)]
        public async Task TryCreate_WhenOnlyOneResourceIsPresent_ReturnsThatResource(string resourceUrl, string resourceType)
        {
            var serviceEntry = new ServiceIndexEntry(new Uri(resourceUrl), resourceType, _defaultVersion);
            var resourceProviders = new ResourceProvider[]
            {
                CreateServiceIndexResourceV3Provider(serviceEntry),
                StaticHttpSource.CreateHttpSource(
                    new Dictionary<string, string>()
                    {
                        { serviceEntry.Uri.AbsoluteUri, GetRepositorySignaturesResourceJson(resourceUrl) }
                    }),
                _repositorySignatureResourceProvider
            };
            var sourceRepository = new SourceRepository(_packageSource, resourceProviders);

            var result = await _repositorySignatureResourceProvider.TryCreate(sourceRepository, CancellationToken.None);

            Assert.True(result.Item1);

            var resource = result.Item2 as RepositorySignatureResource;

            Assert.NotNull(resource);
            Assert.Equal(_packageSource.Source, resource.Source);
            Assert.Single(resource.RepositoryCertificateInfos);
        }

        [Fact]
        public async Task TryCreate_WhenMultipleResourcesArePresent_Returns500Resource()
        {
            var serviceEntry470 = new ServiceIndexEntry(new Uri(_resourceUri470), _resourceType470, _defaultVersion);
            var serviceEntry490 = new ServiceIndexEntry(new Uri(_resourceUri490), _resourceType490, _defaultVersion);
            var serviceEntry500 = new ServiceIndexEntry(new Uri(_resourceUri500), _resourceType500, _defaultVersion);
            var resourceProviders = new ResourceProvider[]
            {
                CreateServiceIndexResourceV3Provider(serviceEntry470, serviceEntry490, serviceEntry500),
                StaticHttpSource.CreateHttpSource(
                    new Dictionary<string, string>()
                    {
                        { serviceEntry470.Uri.AbsoluteUri, GetRepositorySignaturesResourceJson(serviceEntry470.Uri.AbsoluteUri) },
                        { serviceEntry490.Uri.AbsoluteUri, GetRepositorySignaturesResourceJson(serviceEntry490.Uri.AbsoluteUri) },
                        { serviceEntry500.Uri.AbsoluteUri, GetRepositorySignaturesResourceJson(serviceEntry500.Uri.AbsoluteUri) }
                    }),
                _repositorySignatureResourceProvider
            };
            var sourceRepository = new SourceRepository(_packageSource, resourceProviders);

            var result = await _repositorySignatureResourceProvider.TryCreate(sourceRepository, CancellationToken.None);

            Assert.True(result.Item1);

            var resource = result.Item2 as RepositorySignatureResource;

            Assert.NotNull(resource);
            Assert.Equal(_packageSource.Source, resource.Source);
            Assert.Single(resource.RepositoryCertificateInfos);
            Assert.StartsWith(serviceEntry500.Uri.AbsoluteUri, resource.RepositoryCertificateInfos.Single().ContentUrl);
        }

        private static ServiceIndexResourceV3Provider CreateServiceIndexResourceV3Provider(params ServiceIndexEntry[] entries)
        {
            var provider = new Mock<ServiceIndexResourceV3Provider>();

            provider.Setup(x => x.Name)
                .Returns(nameof(ServiceIndexResourceV3Provider));
            provider.Setup(x => x.ResourceType)
                .Returns(typeof(ServiceIndexResourceV3));

            var resources = new JArray();

            foreach (var entry in entries)
            {
                resources.Add(
                    new JObject(
                        new JProperty("@id", entry.Uri.AbsoluteUri),
                        new JProperty("@type", entry.Type)));
            }

            var index = new JObject();

            index.Add("version", "3.0.0");
            index.Add("resources", resources);
            index.Add("@context",
                new JObject(
                    new JProperty("@vocab", "http://schema.nuget.org/schema#"),
                    new JProperty("comment", "http://www.w3.org/2000/01/rdf-schema#comment")));

            var serviceIndexResource = new ServiceIndexResourceV3(index, DateTime.UtcNow);
            var tryCreateResult = new Tuple<bool, INuGetResource>(true, serviceIndexResource);

            provider.Setup(x => x.TryCreate(It.IsAny<SourceRepository>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(tryCreateResult));

            return provider.Object;
        }

        private static string GetRepositorySignaturesResourceJson(string resourceBaseUri)
        {
            var jObject = new JObject(
                new JProperty("allRepositorySigned", false),
                new JProperty("signingCertificates",
                    new JArray(
                        new JObject(
                            new JProperty("fingerprints",
                                new JObject(
                                    new JProperty("2.16.840.1.101.3.4.2.1", "0e5f38f57dc1bcc806d8494f4f90fbcedd988b46760709cbeec6f4219aa6157d"))),
                            new JProperty("subject", "CN=NuGet.org Repository by Microsoft, O=NuGet.org Repository by Microsoft, L=Redmond, S=Washington, C=US"),
                            new JProperty("issuer", "CN=DigiCert SHA2 Assured ID Code Signing CA, OU=www.digicert.com, O=DigiCert Inc, C=US"),
                            new JProperty("notBefore", "2018-04-10T00:00:00.0000000Z"),
                            new JProperty("notAfter", "2021-04-14T12:00:00.0000000Z"),
                            new JProperty("contentUrl", $"{resourceBaseUri}/certificates/0e5f38f57dc1bcc806d8494f4f90fbcedd988b46760709cbeec6f4219aa6157d.crt")))));

            return jObject.ToString();
        }
    }
}