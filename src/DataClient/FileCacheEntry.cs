using JsonLD.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Data
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
            get
            {
                return _uri;
            }
        }

        public async virtual Task<Stream> GetStream()
        {
            throw new NotImplementedException();
        }

        public async virtual Task<JArray> GetExpanded()
        {
            return JsonLdProcessor.Expand(await GetJObject());
        }

        public async virtual Task<JObject> GetJObject()
        {
            throw new NotImplementedException();
        }
    }
}
