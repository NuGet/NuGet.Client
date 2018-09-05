// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using NuGet.Protocol.Test;

namespace NuGet.DependencyResolver.Tests
{
    public class TestRemoteWalkContext : RemoteWalkContext
    {
        public TestRemoteWalkContext() : base(new TestSourceCacheContext(), NullLogger.Instance)
        {
        }
    }
}
