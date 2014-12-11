using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Data
{
    public class CacheHttpClient : HttpClient
    {
        public CacheHttpClient()
            : this(new WebRequestHandler { AllowPipelining = true }) { }

        public CacheHttpClient(HttpMessageHandler handler)
            : base(handler)
        {

        }

        public Task<JObject> GetJObjectAsync(Uri address)
        {
            Task<string> task = GetStringAsync(address);
            return task.ContinueWith<JObject>((t) =>
            {
                try
                {
                    return JObject.Parse(t.Result);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("GetJObjectAsync({0})", address), e);
                }
            });
        }
    }
}
