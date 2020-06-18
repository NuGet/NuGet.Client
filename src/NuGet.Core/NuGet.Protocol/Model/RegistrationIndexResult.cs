// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Model
{
    internal class RegistrationIndexResult
    {
        public RegistrationIndex Index { get; set; }
        public HttpSourceCacheContext CacheContext { get; set; }
    }
}
