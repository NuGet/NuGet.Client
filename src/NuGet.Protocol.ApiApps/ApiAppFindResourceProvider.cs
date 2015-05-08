// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.ApiApps
{
    public class ApiAppFindResourceProvider : ResourceProvider
    {
        public ApiAppFindResourceProvider()
            : base(typeof(ApiAppFindResource))
        {
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
