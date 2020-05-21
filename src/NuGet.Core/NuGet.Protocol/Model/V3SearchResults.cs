using Newtonsoft.Json;
using System.Collections.Generic;

namespace NuGet.Protocol.Model
{
    public class V3SearchResults
    {
        [JsonProperty("totalHits")]
        public long TotalHits { get; set; }

        [JsonProperty("data")]
        public List<PackageSearchMetadata> Data { get; private set; } = new List<PackageSearchMetadata>();
    }
}
