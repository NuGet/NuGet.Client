// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class HttpHandlerResourceV3ProviderTests
    {
#if IS_DESKTOP
        [PlatformFact(Platform.Windows)]
        public void DefaultMaxHttpRequestsPerSourceForwardedToV3HttpClientHandler_Success()
        {
            // Arrange
            var packageSource = new PackageSource("https://contoso.com/v3/index.json");

            var sourceRepository = new SourceRepository(packageSource, new List<INuGetResourceProvider>() { new HttpSourceResourceProvider(), new HttpHandlerResourceV3Provider() });

            // Act
            _ = sourceRepository.GetResource<HttpSourceResource>();
            HttpHandlerResource httpHandlerResource = sourceRepository.GetResource<HttpHandlerResource>();

            // Assert
            Assert.NotNull(httpHandlerResource);
            Assert.Equal(64, httpHandlerResource.ClientHandler.MaxConnectionsPerServer);
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(128)]
        [InlineData(256)]
        public void PackageSourceMaxHttpRequestsPerSourceForwardedToV3HttpClientHandler_Success(int maxHttpRequestsPerSource)
        {
            // Arrange
            var packageSource = new PackageSource("https://contoso.com/v3/index.json") { MaxHttpRequestsPerSource = maxHttpRequestsPerSource };

            var sourceRepository = new SourceRepository(packageSource, new List<INuGetResourceProvider>() { new HttpSourceResourceProvider(), new HttpHandlerResourceV3Provider() });

            // Act
            _ = sourceRepository.GetResource<HttpSourceResource>();
            HttpHandlerResource httpHandlerResource = sourceRepository.GetResource<HttpHandlerResource>();

            // Assert
            Assert.NotNull(httpHandlerResource);
            Assert.Equal(maxHttpRequestsPerSource, httpHandlerResource.ClientHandler.MaxConnectionsPerServer);
        }
#endif

#if IS_CORECLR
        [Theory]
        [InlineData(64)]
        [InlineData(128)]
        [InlineData(2)]
        public void PackageSourceMaxHttpRequestsPerSourceNotForwardedToV3HttpClientHandler_Success(int maxHttpRequestsPerSource)
        {
            // Arrange
            var packageSource = new PackageSource("https://contoso.com/v3/index.json") { MaxHttpRequestsPerSource = maxHttpRequestsPerSource };

            var sourceRepository = new SourceRepository(packageSource, new[] { new HttpHandlerResourceV3Provider() });

            // Act
            _ = sourceRepository.GetResource<HttpSourceResource>();
            HttpHandlerResource httpHandlerResource = sourceRepository.GetResource<HttpHandlerResource>();

            // Assert
            Assert.NotNull(httpHandlerResource);
            Assert.NotEqual(maxHttpRequestsPerSource, httpHandlerResource.ClientHandler.MaxConnectionsPerServer);
        }
#endif
    }
}
