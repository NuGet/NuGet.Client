using Newtonsoft.Json;
using NuGet.Client.V3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V3
{
    public static class V3Utilities
    {
        public static bool IsV3(PackageSource source)
        {
            var url = new Uri(source.Url);
            if (url.IsFile || url.IsUnc)
            {
                return File.Exists(url.LocalPath);
            }

            using (var client = new NuGetV3Client(source.Url, "host"))
            {
                var v3index = client.GetFile(url);
                if (v3index == null)
                {
                    return false;
                }

                var status = v3index.Result.Value<string>("version");
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
