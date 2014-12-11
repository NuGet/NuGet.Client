using JsonLD.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Data
{
    public static class Utility
    {
        /// <summary>
        /// True if this object has @context
        /// </summary>
        /// <param name="compacted"></param>
        /// <returns></returns>
        public static bool IsValidJsonLd(JObject compacted)
        {
            return compacted != null && GetContext(compacted) != null;
        }

        public static readonly string[] IdNames = new string[] { Constants.UrlIdName, Constants.IdName };

        public static Uri GetUriWithoutHash(Uri uri)
        {
            if (uri != null)
            {
                string s = uri.AbsoluteUri;
                int hash = s.IndexOf('#');

                if (hash > -1)
                {
                    s = s.Substring(0, hash);
                    return new Uri(s);
                }
            }

            return uri;
        }

        /// <summary>
        /// Check if the entity url matches the root url
        /// </summary>
        /// <param name="token">entity token</param>
        /// <param name="entityUri">Optional field, if this is given the method will not try to parse it out again.</param>
        /// <returns>true if the root uri is the base of the entity uri</returns>
        public static bool? IsEntityFromPage(JToken token, Uri entityUri = null)
        {
            bool? result = null;
            Uri uri = entityUri == null ? GetEntityUri(token) : entityUri;

            if (uri != null)
            {
                var rootUri = GetEntityUri(GetRoot(token));

                result = CompareRootUris(uri, rootUri);
            }

            return result;
        }

        /// <summary>
        /// True if the uri does not have a #
        /// </summary>
        public static bool IsRootUri(Uri uri)
        {
            return (uri.AbsoluteUri.IndexOf('#') == -1);
        }

        /// <summary>
        /// Checks if the uris match or differ only in the # part. 
        /// If either are null this returns false.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool CompareRootUris(Uri a, Uri b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            var x = Utility.GetUriWithoutHash(a);
            var y = Utility.GetUriWithoutHash(b);

            return x.Equals(y);
        }

        public static JToken GetRoot(JToken token)
        {
            JToken parent = token;

            while (parent.Parent != null)
            {
                parent = parent.Parent;
            }

            return parent;
        }

        public static JToken GetContext(JObject jObj)
        {
            if (jObj != null)
            {
                JToken context;

                if (jObj.TryGetValue(Constants.ContextName, out context))
                {
                    return context;
                }
            }

            return null;
        }

        public static Uri GetEntityUri(JObject jObj)
        {
            if (jObj != null)
            {
                JToken urlValue;

                foreach (string idName in IdNames)
                {
                    if (jObj.TryGetValue(idName, out urlValue))
                    {
                        return new Uri(urlValue.ToString());
                    }
                }
            }

            return null;
        }

        public static Uri GetEntityUri(JToken token)
        {
            return GetEntityUri(token as JObject);
        }

        public static async Task<JToken> FindEntityInJson(Uri entity, JObject json)
        {
            string search = entity.AbsoluteUri;

            var idNode = json.Descendants().Concat(json).Where(n =>
            {
                JProperty prop = n as JProperty;

                if (prop != null)
                {
                    string url = prop.Value.ToString();

                    return StringComparer.Ordinal.Equals(url, search);
                }

                return false;
            }).FirstOrDefault();

            if (idNode != null)
            {
                return idNode.Parent.DeepClone();
            }

            return null;
        }

        public static bool IsInContext(JToken token)
        {
            JToken parent = token;

            while (parent != null)
            {
                JProperty prop = parent as JProperty;

                if (prop != null && StringComparer.Ordinal.Equals(prop.Name, Constants.ContextName))
                {
                    return true;
                }

                parent = parent.Parent;
            }

            return false;
        }

        public static void JsonEntityVisitor(JObject root, Action<JObject> visitor)
        {
            var props = root
                .Descendants()
                .Where(t => t.Type == JTokenType.Property)
                .Cast<JProperty>()
                .Where(p => Utility.IdNames.Contains(p.Name))
                .ToList();

            foreach (var prop in props)
            {
                visitor((JObject)prop.Parent);
            }
        }
        public static BasicGraph GetGraphFromCompacted(JToken compacted)
        {
            var flattened = JsonLdProcessor.Flatten(compacted, new JsonLdOptions());
            return GetGraph(flattened);
        }

        public static BasicGraph GetGraph(JToken flattened)
        {
            BasicGraph graph = new BasicGraph();

            RDFDataset dataSet = (RDFDataset)JsonLD.Core.JsonLdProcessor.ToRDF(flattened);

            foreach (var graphName in dataSet.GraphNames())
            {
                foreach (var quad in dataSet.GetQuads(graphName))
                {
                    graph.Assert(quad);
                }
            }

            return graph;
        }
    }
}
