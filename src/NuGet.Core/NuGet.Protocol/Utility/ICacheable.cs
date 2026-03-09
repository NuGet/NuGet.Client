// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol
{
    /// <summary>
    /// Implemented by types whose string properties can be deduplicated
    /// via <see cref="MetadataReferenceCache"/>.
    /// </summary>
    internal interface ICacheable
    {
        /// <summary>
        /// Replaces all string property values with cached equivalents.
        /// </summary>
        void CacheStrings(MetadataReferenceCache cache);
    }
}
