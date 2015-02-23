using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace NuGet.ProjectModel
{
    public static class JTokenExtensions
    {
        public static T[] ValueAsArray<T>(this JToken jToken)
        {
            return jToken.Select(a => a.Value<T>()).ToArray();
        }

        public static T[] ValueAsArray<T>(this JToken jToken, string name)
        {
            return jToken?[name]?.ValueAsArray<T>();
        }

        public static T GetValue<T>(this JToken token, string name)
        {
            if (token == null)
            {
                return default(T);
            }

            var obj = token[name];

            if (obj == null)
            {
                return default(T);
            }

            return obj.Value<T>();
        }
        public static IDictionary<string, object> ToDictionary(this JObject obj)
        {
            return obj.Properties()
                .ToDictionary(
                    p => p.Name,
                    p => ConvertJsonValue(p.Value));
        }

        private static object ConvertJsonValue(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    return ((JObject)token).ToDictionary();
                case JTokenType.Array:
                    return ((JArray)token).Select(ConvertJsonValue).ToArray();
                case JTokenType.Integer:
                    return (long)token;
                case JTokenType.Float:
                    return (double)token;
                case JTokenType.Boolean:
                    return (bool)token;

                // Dump all the string-derived types that JSON.NET tries to sniff out back to strings
                case JTokenType.String:
                case JTokenType.Date:
                case JTokenType.Bytes:
                case JTokenType.Guid:
                case JTokenType.Uri:
                case JTokenType.TimeSpan:
                    return (string)token;
                default:
                    // This covers BOTH the case where the property value is null
                    // AND the case where the property value is of unknown type
                    return null;
            }
        }
    }
}