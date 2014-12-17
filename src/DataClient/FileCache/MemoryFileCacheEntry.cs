using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Data
{
    public class MemoryFileCacheEntry : FileCacheEntry
    {
        private readonly JObject _jObj;

        public MemoryFileCacheEntry(JObject jObj, Uri uri, DateTime added, TimeSpan lifeSpan)
            : base(uri, added, lifeSpan)
        {
            _jObj = jObj;
        }

        public override async Task<JObject> GetJObject()
        {
            return _jObj;
        }

        public override async Task<Stream> GetStream()
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(_jObj.ToString()));
        }
    }
}
