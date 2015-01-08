using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Stores/caches a service index json file.
    /// </summary>
    public class V3ServiceIndexResource : INuGetResource
    {
        private readonly JObject _index;
        private readonly DateTime _requestTime;

        public V3ServiceIndexResource(JObject index, DateTime requestTime)
        {
            _index = index;
            _requestTime = requestTime;
        }

        /// <summary>
        /// Raw json
        /// </summary>
        public virtual JObject Index
        {
            get
            {
                return _index;
            }
        }

        /// <summary>
        /// Time the index was requested
        /// </summary>
        public virtual DateTime RequestTime
        {
            get
            {
                return _requestTime;
            }
        }

        /// <summary>
        /// A list of endpoints for a service type
        /// </summary>
        public virtual IList<Uri> this[string type]
        {
            get
            {
                return Index["resources"].Where(j => ((string)j["@type"]) == type).Select(o => o["@id"].ToObject<Uri>()).ToList();
            }
        }
    }
}
