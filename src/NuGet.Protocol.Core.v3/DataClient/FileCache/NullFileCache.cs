// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Protocol.Core.v3.Data
{
    public class NullFileCache : FileCacheBase
    {
        public NullFileCache()
            : base()
        {
        }

        public override void Remove(Uri uri)
        {
            // do nothing
        }

        public override bool TryGet(Uri uri, out Stream stream)
        {
            // do nothing
            stream = null;
            return false;
        }

        public override void Add(Uri uri, TimeSpan lifeSpan, Stream stream)
        {
            // do nothing
        }
    }
}
