// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Protocol.Core.v3.Data
{
    public abstract class FileCacheBase
    {
        public abstract bool TryGet(Uri uri, out Stream stream);

        public abstract void Remove(Uri uri);

        public abstract void Add(Uri uri, TimeSpan lifeSpan, Stream stream);
    }
}
