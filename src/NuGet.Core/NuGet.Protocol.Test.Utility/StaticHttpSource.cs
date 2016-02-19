// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Test.Utility
{
    public static class StaticHttpSource
    {
        /// <summary>
        /// Creates a handler to override url requests to static content
        /// </summary>
        public static TestHttpSourceProvider CreateHttpSource(Dictionary<string, string> responses, string errorContent = "")
        {
            var handlerProvider = StaticHttpHandler.CreateHttpHandler(responses, errorContent);

            return new TestHttpSourceProvider(responses);
        }

        /// <summary>
        /// Creates a source and injects an http handler to override the normal http calls
        /// </summary>
        public static SourceRepository CreateSource(string sourceUrl, IEnumerable<Lazy<INuGetResourceProvider>> providers, Dictionary<string, string> responses)
        {
            var handler = new Lazy<INuGetResourceProvider>(() => CreateHttpSource(responses));

            return new SourceRepository(new PackageSource(sourceUrl), providers.Concat(new Lazy<INuGetResourceProvider>[] { handler }));
        }
    }

    public class TestHttpSourceProvider : ResourceProvider
    {
        private Dictionary<string, string> _responses;

        public TestHttpSourceProvider(Dictionary<string, string> responses)
            : base(typeof(HttpSourceResource), "testhttpsource", NuGetResourceProviderPositions.First)
        {
            _responses = responses;
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            var httpSource = new TestHttpSource(source.PackageSource, _responses);

            var result = new Tuple<bool, INuGetResource>(true, new HttpSourceResource(httpSource));
            return Task.FromResult(result);
        }
    }
}
