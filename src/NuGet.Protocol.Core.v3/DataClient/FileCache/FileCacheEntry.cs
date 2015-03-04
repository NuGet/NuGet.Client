using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            get
            {
                return _uri;
            }
        }

        public async virtual Task<Stream> GetStream()
        {
            await Task.Delay(1);
            throw new NotImplementedException();
        }

        public async virtual Task<JObject> GetJObject()
        {
            await Task.Delay(1);
            throw new NotImplementedException();
        }
    }
}
