using JsonLD.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Data
{
    public static class JsonExtensions
    {

        /// <summary>
        /// Returns the context nearest above or on this jObject
        /// </summary>
        /// <param name="jObject"></param>
        /// <returns></returns>
        public static JToken NearestContext(this JToken jToken)
        {
            JToken parent = jToken;

            JToken context = null;
            while (parent != null && context == null)
            {
                context = parent.Where(n => n.Path == "@context").FirstOrDefault();

                parent = parent.Parent;
            }

            return context;
        }
    }
}
