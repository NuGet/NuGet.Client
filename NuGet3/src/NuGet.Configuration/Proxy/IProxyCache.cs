// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if !DNXCORE50
using System;
using System.Net;

namespace NuGet.Configuration
{
    public interface IProxyCache
    {
        void Add(IWebProxy proxy);
        IWebProxy GetProxy(Uri uri);
    }
}

#endif
