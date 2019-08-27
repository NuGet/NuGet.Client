// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class FactoryExtensionsV3Tests
    {
        static INuGetResourceProvider GetProvider(IEnumerable<Lazy<INuGetResourceProvider>> providers, Type type)
        {
            foreach (var provider in providers)
            {
                if (provider.Value.ResourceType == type)
                {
                    return provider.Value;
                }
             }
            return null;
        }

        [Fact]
        public void DefaultHttpHandlerProvider()
        {
            var providers = Repository.Provider.GetCoreV3().ToList();
            INuGetResourceProvider provider = GetProvider (providers, typeof(HttpHandlerResource));

            Assert.IsType<HttpHandlerResourceV3Provider>(provider);
        }

        [Fact]
        public void CustomHttpHandlerProvider()
        {
            var originalProvider = FactoryExtensionsV3.CreateHttpHandlerResourceV3Provider;

            try
            {
                Func<HttpClientHandler> messageHandlerFactory = () => new HttpClientHandler();
                var testProvider = new TestHttpHandlerProvider(messageHandlerFactory);
                FactoryExtensionsV3.CreateHttpHandlerResourceV3Provider = () => testProvider;
                var providers = Repository.Provider.GetCoreV3().ToList();
                INuGetResourceProvider provider = GetProvider(providers, typeof(HttpHandlerResource));

                Assert.IsType<TestHttpHandlerProvider>(provider);
            }
            finally
            {
                FactoryExtensionsV3.CreateHttpHandlerResourceV3Provider = originalProvider;
            }
        }
    }
}
