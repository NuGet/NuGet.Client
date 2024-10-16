// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable CS1591

using System;

namespace Internal.NuGet.Testing.SignedPackages
{
    public sealed class OcspResponderOptions
    {
        public DateTimeOffset? ThisUpdate { get; set; }
        public DateTimeOffset? NextUpdate { get; set; }
    }
}
