using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGet.Protocol.Model
{
    public class PackageSearchMetadataRegistration : PackageSearchMetadata
    {
        [JsonProperty(PropertyName = JsonProperties.SubjectId)]
        public Uri CatalogUri { get; private set; }
    }
}
