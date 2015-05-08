// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.Core.v3.Data
{
    public abstract class FileCacheEntry
    {
        private readonly Uri _uri;
        private readonly DateTime _added;
        private readonly TimeSpan _lifeSpan;

        public FileCacheEntry(Uri uri, DateTime added, TimeSpan lifeSpan)
        {
            _uri = uri;
            _added = added;
            _lifeSpan = lifeSpan;
        }

        public Uri Uri
        {
            get { return _uri; }
        }

        public virtual async Task<Stream> GetStream()
        {
            await Task.Delay(1);
            throw new NotImplementedException();
        }

        public virtual async Task<JObject> GetJObject()
        {
            await Task.Delay(1);
            throw new NotImplementedException();
        }
    }
}
