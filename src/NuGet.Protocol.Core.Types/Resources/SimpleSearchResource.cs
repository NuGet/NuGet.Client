// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Returns basic search results from the source
    /// </summary>
    public abstract class SimpleSearchResource : INuGetResource
    {
        /// <summary>
        /// Returns search entries
        /// </summary>
        public abstract Task<IEnumerable<SimpleSearchMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken);
    }
}
