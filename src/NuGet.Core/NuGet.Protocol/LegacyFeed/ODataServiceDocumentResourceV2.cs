// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class ODataServiceDocumentResourceV2 : INuGetResource
    {
        public ODataServiceDocumentResourceV2(string baseAddress, DateTime requestTime)
        {
            BaseAddress = baseAddress.Trim('/');
            RequestTime = requestTime;
        }

        public virtual DateTime RequestTime { get; }

        public string BaseAddress { get; }
    }
}
