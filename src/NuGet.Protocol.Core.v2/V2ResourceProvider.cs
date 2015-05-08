// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v2
{
    /// <summary>
    /// Partial implementation for IResourceProvider to do the common V2 specific stuff.
    /// </summary>
    public abstract class V2ResourceProvider : ResourceProvider
    {
        public V2ResourceProvider(Type resourceType)
            : this(resourceType, string.Empty, null)
        {
        }

        public V2ResourceProvider(Type resourceType, string name)
            : this(resourceType, name, null)
        {
        }

        public V2ResourceProvider(Type resourceType, string name, string before)
            : base(resourceType, name, ToArray(before), Enumerable.Empty<string>())
        {
        }

        protected async Task<V2Resource> GetRepository(SourceRepository source, CancellationToken token)
        {
            var repositoryResource = await source.GetResourceAsync<PackageRepositoryResourceV2>(token);

            if (repositoryResource != null
                && repositoryResource.V2Client != null)
            {
                return repositoryResource;
            }

            return null;
        }

        private static IEnumerable<string> ToArray(string s)
        {
            if (!String.IsNullOrEmpty(s))
            {
                return new string[] { s };
            }

            return Enumerable.Empty<string>();
        }
    }
}
