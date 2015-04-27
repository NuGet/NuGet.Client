using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3.Data
{
    /// <summary>
    /// Options used for DataClient http requests.
    /// </summary>
    public sealed class DataCacheOptions
    {
        public DataCacheOptions()
        {
            UseFileCache = false;
            MaxCacheLife = TimeSpan.Zero;
            Refresh = false;
        }

        /// <summary>
        /// If set the file will be stored in the file cache.
        /// </summary>
        public bool UseFileCache { get; set; }

        /// <summary>
        /// Maximum allowed file age. If the file is older than the limit it will be refetched.
        /// </summary>
        public TimeSpan MaxCacheLife { get; set; }

        /// <summary>
        /// If set pages will be requested and updated in the cache
        /// </summary>
        public bool Refresh { get; set; }
    }
}
