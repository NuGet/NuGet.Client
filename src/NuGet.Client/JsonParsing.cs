using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NuGet.Client.Diagnostics;

namespace NuGet.Client
{
    internal static class JsonParsing
    {
        public static IEnumerable<Uri> ParseUrlArray(IEnumerable<JToken> array, Tracer trace, Uri documentRoot, string invalidUrlErrorFormat)
        {
            Guard.NotNull(trace, "trace");

            using (trace.EnterExit())
            {
                return array == null ?
                    Enumerable.Empty<Uri>() :
                    array.Select(token => {
                        var url = TryParseUrl(trace, documentRoot, token);
                        if (url == null)
                        {
                            trace.JsonParseWarning(token, String.Format(CultureInfo.CurrentCulture, invalidUrlErrorFormat, token.ToDisplayString()));
                        }
                        return url;
                    })
                    .Where(url => url != null);
            }
        }

        public static IEnumerable<KeyValuePair<string, Uri>> ParseUrlDictionary(JObject obj, Tracer trace, Uri documentRoot, string invalidUrlErrorFormat)
        {
            Guard.NotNull(trace, "trace");

            using (trace.EnterExit())
            {
                return obj == null ?
                    Enumerable.Empty<KeyValuePair<string, Uri>>() :
                    obj
                        .Properties()
                        .Select(prop =>
                        {
                            var url = TryParseUrl(trace, documentRoot, prop.Value);
                            if (url == null)
                            {
                                trace.JsonParseWarning(prop.Value, String.Format(CultureInfo.CurrentCulture, invalidUrlErrorFormat, prop.Value.ToDisplayString(), prop.Name));
                            }
                            return new KeyValuePair<string, Uri>(prop.Name, url);
                        })
                        .Where(pair => pair.Value != null);
            }
        }

        public static Uri TryParseUrl(Tracer trace, Uri documentRoot, JToken token)
        {
            Guard.NotNull(trace, "trace");
            using (trace.EnterExit())
            {
                if (token.Type == JTokenType.String)
                {
                    string val = token.Value<string>();
                    if (!String.IsNullOrEmpty(val))
                    {
                        Uri url;
                        if (Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out url))
                        {
                            // Resolve the url if possible
                            if (documentRoot != null && !url.IsAbsoluteUri)
                            {
                                url = new Uri(documentRoot, url);
                            }
                            return url;
                        }
                    }
                }
                return null;
            }
        }
    }
}
