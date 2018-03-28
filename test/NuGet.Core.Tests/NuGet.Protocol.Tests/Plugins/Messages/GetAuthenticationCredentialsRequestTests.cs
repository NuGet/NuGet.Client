// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Protocol.Plugins;
using Xunit;

namespace NuGet.Protocol.Tests.Plugins.Messages
{
    public class GetAuthenticationCredentialsRequestTests
    {

        [Fact]
        public void Constructor_ThrowsForNullOrEmptyPackageSourceRepository()
        {
            Uri uri = null;
            var exception = Assert.Throws<ArgumentNullException>(
                () => new GetAuthenticationCredentialsRequest(
                    uri: uri,
                    isRetry: false,
                    nonInteractive: false
                    ));
            Assert.Equal("uri", exception.ParamName);
        }

    }
}
