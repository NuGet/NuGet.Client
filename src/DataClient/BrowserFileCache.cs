using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Data
{
    public class BrowserFileCache : FileCacheBase
    {
        /// <summary>
        /// Browser cache based on mshtml
        /// </summary>
        public BrowserFileCache()
        {

        }

        public override void Remove(Uri uri)
        {
            throw new NotImplementedException();
        }

        public override bool TryGet(Uri uri, out Stream stream)
        {
            stream = BrowserCache.Get(uri.AbsoluteUri);

            return stream != null;
        }

        public override void Add(Uri uri, TimeSpan lifeSpan, Stream stream)
        {
            bool result = BrowserCache.Add(uri.AbsoluteUri, stream, DateTime.Now.AddHours(1));

            Debug.Assert(result, "failed to add to cache");
        }
    }
}
