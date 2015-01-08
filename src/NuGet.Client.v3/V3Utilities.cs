using Newtonsoft.Json;
using NuGet.Client.V3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Data;

namespace NuGet.Client.V3
{
    public static class V3Utilities
    {
        public async static Task<bool> IsV3(PackageSource source)
        {
            var url = new Uri(source.Url);
            if (url.IsFile && url.LocalPath.EndsWith(".json",StringComparison.OrdinalIgnoreCase))  //Hook to enable local index.json for development and testing purposes.
            {
                return File.Exists(url.LocalPath);
            }

            using (var client = new DataClient())
            {
                var v3index = await client.GetJObjectAsync(url);
                if (v3index == null)
                {
                    return false;
                }

                var status = v3index.Value<string>("version");
                if (status != null && status.StartsWith("3.0"))
                {
                    return true;
                }

                return false;
            }
        }
          public static NuGetV3Client GetV3Client(PackageSource source, string host)
        {                     
           return new NuGetV3Client(source.Url, host);         
        }
    }
}
