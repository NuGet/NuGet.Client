// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;

namespace NuGet.Protocol.Core.v3.Data
{
    public class MemoryFileCache : FileCacheBase
    {
        private readonly ConcurrentDictionary<Uri, MemoryStream> _entries;

        public MemoryFileCache()
            : base()
        {
            _entries = new ConcurrentDictionary<Uri, MemoryStream>();
        }

        public override void Remove(Uri uri)
        {
            MemoryStream entry = null;
            if (_entries.TryRemove(uri, out entry)
                && entry != null)
            {
                entry.Dispose();
            }
        }

        public override bool TryGet(Uri uri, out Stream stream)
        {
            stream = null;

            MemoryStream cacheStream = null;
            if (_entries.TryGetValue(uri, out cacheStream))
            {
                stream = new MemoryStream();
                cacheStream.Seek(0, SeekOrigin.Begin);
                cacheStream.CopyTo(stream);
                return true;
            }

            return false;
        }

        public override void Add(Uri uri, TimeSpan lifeSpan, Stream stream)
        {
            var cacheStream = new MemoryStream();

            stream.CopyTo(cacheStream);
            stream.Seek(0, SeekOrigin.Begin);

            _entries.AddOrUpdate(uri, cacheStream, (k, v) => cacheStream);
        }
    }
}
