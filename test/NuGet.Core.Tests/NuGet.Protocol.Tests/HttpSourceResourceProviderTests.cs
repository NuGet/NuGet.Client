// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        public void MaxHttpRequestsPerSourceIsToDefaultValue_Success()
        {
            // Arrange
            var packageSource = new PackageSource("https://contoso.com/v3/index.json");
            var sourceRepository = new SourceRepository(packageSource, new[] { new HttpSourceResourceProvider() });

            // Act
            HttpSourceResource httpSourceResource = sourceRepository.GetResource<HttpSourceResource>();

            // Assert
            Assert.NotNull(httpSourceResource);
            Assert.Equal(64, sourceRepository.PackageSource.MaxHttpRequestsPerSource);
        }
#endif

#if IS_CORECLR
        [Fact]
        public void MaxHttpRequestsPerSourceIsSetToDefaultValue_Success()
        {
            // Arrange
            var packageSource = new PackageSource("https://contoso.com/v3/index.json");
            var sourceRepository = new SourceRepository(packageSource, new[] { new HttpSourceResourceProvider() });

            // Act
            HttpSourceResource httpSourceResource = sourceRepository.GetResource<HttpSourceResource>();

            // Assert
            Assert.NotNull(httpSourceResource);
            Assert.Equal(0, sourceRepository.PackageSource.MaxHttpRequestsPerSource);
        }
#endif
    }
}
