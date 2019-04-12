// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Test.Utility
{
    public class TestHttpSourceProvider : ResourceProvider
    {
        private readonly Dictionary<string, string> _responses;
        private readonly TestHttpSource _httpSource;

        public TestHttpSourceProvider(Dictionary<string, string> responses)
            : this(responses, httpSource: null)
        {
        }

        public TestHttpSourceProvider(Dictionary<string, string> responses, TestHttpSource httpSource)
            : base(typeof(HttpSourceResource), "testhttpsource", NuGetResourceProviderPositions.First)
        {
            _responses = responses;
            _httpSource = httpSource;
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            var httpSource = _httpSource ?? new TestHttpSource(source.PackageSource, _responses);
            var result = new Tuple<bool, INuGetResource>(true, new HttpSourceResource(httpSource));

            return Task.FromResult(result);
        }
    }
}