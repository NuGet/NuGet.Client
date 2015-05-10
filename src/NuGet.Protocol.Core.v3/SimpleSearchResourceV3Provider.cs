// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// V3 Simple search resource aimed at command line searches
    /// </summary>
    public class SimpleSearchResourceV3Provider : ResourceProvider
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public SimpleSearchResourceV3Provider()
            : base(typeof(SimpleSearchResource),
                  nameof(SimpleSearchResourceV3Provider),
                  "SimpleSearchResourceV2Provider")
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            SimpleSearchResourceV3 curResource = null;

            var rawSearch = await source.GetResourceAsync<RawSearchResourceV3>(token);

            if (rawSearch != null
                && rawSearch is RawSearchResourceV3)
            {
                curResource = new SimpleSearchResourceV3(rawSearch);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
