// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Test
{
    /// <summary>
    /// This is a test source cache context that should be used in places where it is not convenient to dispose the
    /// <see cref="SourceCacheContext"/>. Since <see cref="SourceCacheContext.GeneratedTempFolder"/> must be called
    /// before <see cref="SourceCacheContext.Dispose"/> does anything meaningful, this implementation disables that
    /// property.
    /// </summary>
    public class TestSourceCacheContext : SourceCacheContext
    {
        public override string GeneratedTempFolder
        {
            get
            {
                throw new NotSupportedException(
                    "The test source cache context does not support building a generated temp folder.");
            }
        }
    }
}
