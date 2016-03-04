// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Credentials;
using NuGet.VisualStudio;
using NuGetVSExtension;
using Xunit;

namespace NuGet.VsExtension.Test
{
    public class VsCredentialProviderAdapterTests
    {
        private class TestVsCredentialProvider : IVsCredentialProvider
        {
            private readonly ICredentials _testResponse;

            public TestVsCredentialProvider(ICredentials testResponse)
            {
                _testResponse = testResponse;
            }

            public Task<ICredentials> GetCredentialsAsync(Uri uri, IWebProxy proxy, bool isProxyRequest, bool isRetry, bool nonInteractive,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_testResponse);
            }
        }

        [Fact]
        public async Task WhenCredsNull_ThenReturnProviderNotApplicable()
        {
            var provider = new TestVsCredentialProvider(null);
            var adapter = new VsCredentialProviderAdapter(provider);

            var result = await adapter.Get(new Uri("http://host"), null, false, false, false, CancellationToken.None);

            Assert.Null(result.Credentials);
            Assert.Equal(CredentialStatus.ProviderNotApplicable, result.Status);
        }

        [Fact]
        public async Task WhenAnyValidVsCredentialResponse_Ok()
        {
            var expected = new NetworkCredential("foo", "bar");
            var provider = new TestVsCredentialProvider(expected);
            var adapter = new VsCredentialProviderAdapter(provider);

            var result = await adapter.Get(new Uri("http://host"), null, false, false, false, CancellationToken.None);

            Assert.Same(expected, result.Credentials);
            Assert.Equal(CredentialStatus.Success, result.Status);
        }
    }
}
