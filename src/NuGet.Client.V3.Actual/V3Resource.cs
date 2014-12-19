using NuGet.Client.V3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V3
{
    /// <summary>
    /// Represents a resource provided by a V3 server. [ Like search resource, metadata resource]
    /// </summary>
    public class V3Resource : Resource
    {
        protected NuGetV3Client _v3Client;    

        public V3Resource(V3Resource v3Resource)
        {
            _v3Client = v3Resource.V3Client;
            _host = v3Resource.Host;           
        }
        public V3Resource(NuGetV3Client client,string host)
        {
            _v3Client = client;
            _host = host;
        }

        public NuGetV3Client V3Client
        {
            get
            {
                return _v3Client;
            }
        }     
    }
}
