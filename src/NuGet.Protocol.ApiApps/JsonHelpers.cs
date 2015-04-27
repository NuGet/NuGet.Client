using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Protocol.ApiApps
{
    internal static class JsonHelpers
    {
        internal static int GetIntOrZero(JObject json, string propertyName)
        {
            JToken token = null;
            int x = 0;

            if (json.TryGetValue(propertyName, out token))
            {
                Int32.TryParse(token.ToString(), out x);
            }

            return x;
        }

        internal static string GetStringOrNull(JObject json, string propertyName)
        {
            JToken token = null;
            string s = null;

            if (json.TryGetValue(propertyName, out token))
            {
                s = token.ToString();
            }

            return s;
        }

        internal static Uri GetUriOrNull(JObject json, string propertyName)
        {
            Uri uri = null;

            string s = GetStringOrNull(json, propertyName);

            if (!String.IsNullOrEmpty(s))
            {
                uri = new Uri(s);
            }

            return uri;
        }

        internal static Guid GetGuidOrEmpty(JObject json, string propertyName)
        {
            Guid guid = Guid.Empty;

            string s = GetStringOrNull(json, propertyName);

            if (!String.IsNullOrEmpty(s))
            {
                Guid.TryParse(s, out guid);
            }

            return guid;
        }

        internal static IEnumerable<string> GetStringArray(JObject json, string propertyName)
        {
            JToken token = null;
            IEnumerable<string> result = null;

            if (json.TryGetValue(propertyName, out token))
            {
                JArray array = token as JArray;

                if (array != null)
                {
                    result = array.Children().Select(e => e.ToString()).ToArray();
                }
            }

            return result ?? Enumerable.Empty<string>();
        }

        internal static NuGetVersion GetVersionOrNull(JObject json, string propertyName)
        {
            NuGetVersion version = null;

            string s = GetStringOrNull(json, propertyName);

            if (!String.IsNullOrEmpty(s))
            {
                NuGetVersion.TryParse(s, out version);
            }

            return version;
        }
    }
}