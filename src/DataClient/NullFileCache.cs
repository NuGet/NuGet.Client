using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Data
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

        public override void Add(Uri uri, TimeSpan lifeSpan, System.IO.Stream stream)
        {
            // do nothing
        }
    }
}
