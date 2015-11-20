// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Core.Types
{
    public class HttpSourceCacheContext
    {
        public HttpSourceCacheContext(SourceCacheContext context)
        {
            MaxAge = context.ListMaxAgeTimeSpan;
            RootTempFolder = context.GeneratedTempFolder;
        }

        public HttpSourceCacheContext(SourceCacheContext context, TimeSpan overrideMaxAge)
        {
            MaxAge = overrideMaxAge;
            RootTempFolder = context.GeneratedTempFolder;
        }

        public TimeSpan MaxAge { get; }

        public string RootTempFolder { get; }
    }
}
