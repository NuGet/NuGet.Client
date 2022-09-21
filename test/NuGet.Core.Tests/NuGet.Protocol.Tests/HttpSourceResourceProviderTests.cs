// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class HttpSourceResourceProviderTests
    {
#if IS_DESKTOP
        [PlatformFact(Platform.Windows)]
        public async Task WhenMaxHttpRequestsPerSourceIsNotConfiguredThenItsValueIsSetToDefault_SuccessAsync()
        {
            // Arrange
            var packageSource = new PackageSource("https://contoso.com/v3/index.json");
            var sourceRepository = new SourceRepository(packageSource, new[] { new HttpSourceResourceProvider() });

            // Act
            HttpSourceResource httpSourceResource = await sourceRepository.GetResourceAsync<HttpSourceResource>();

            // Assert
            Assert.NotNull(httpSourceResource);
            Assert.Equal(64, sourceRepository.PackageSource.MaxHttpRequestsPerSource);
        }
#elif IS_CORECLR
        [Fact]
        public async Task WhenMaxHttpRequestsPerSourceIsNotConfiguredThenItsValueWillNotBeUpdated_SuccessAsync()
        {
            // Arrange
            var packageSource = new PackageSource("https://contoso.com/v3/index.json");
            var sourceRepository = new SourceRepository(packageSource, new[] { new HttpSourceResourceProvider() });

            // Act
            HttpSourceResource httpSourceResource = await sourceRepository.GetResourceAsync<HttpSourceResource>();

            // Assert
            Assert.NotNull(httpSourceResource);
            Assert.Equal(0, sourceRepository.PackageSource.MaxHttpRequestsPerSource);
        }
#endif

        [PlatformTheory]
        [InlineData(128)]
        [InlineData(256)]
        public async Task WhenMaxHttpRequestsPerSourceIsConfiguredThenItsValueWillNotBeUpdated_SuccessAsync(int maxHttpRequestsPerSource)
        {
            // Arrange
            var packageSource = new PackageSource("https://contoso.com/v3/index.json") { MaxHttpRequestsPerSource = maxHttpRequestsPerSource };
            var sourceRepository = new SourceRepository(packageSource, new[] { new HttpSourceResourceProvider() });

            // Act
            HttpSourceResource httpSourceResource = await sourceRepository.GetResourceAsync<HttpSourceResource>();

            // Assert
            Assert.NotNull(httpSourceResource);
            Assert.Equal(maxHttpRequestsPerSource, sourceRepository.PackageSource.MaxHttpRequestsPerSource);
        }
    }
}
