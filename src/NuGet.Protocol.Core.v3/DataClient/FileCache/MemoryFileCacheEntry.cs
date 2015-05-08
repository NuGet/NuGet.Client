// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.Core.v3.Data
{
    public class MemoryFileCacheEntry : FileCacheEntry
    {
        private readonly JObject _jObj;

        public MemoryFileCacheEntry(JObject jObj, Uri uri, DateTime added, TimeSpan lifeSpan)
            : base(uri, added, lifeSpan)
        {
            _jObj = jObj;
        }

        public override Task<JObject> GetJObject()
        {
            return Task.FromResult(_jObj);
        }

        public override Task<Stream> GetStream()
        {
            Stream result = new MemoryStream(Encoding.UTF8.GetBytes(_jObj.ToString()));
            return Task.FromResult(result);
        }
    }
}
