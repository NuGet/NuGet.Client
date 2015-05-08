// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

namespace NuGet.Protocol.ApiApps
{
    internal static class JsonHelpers
    {
        internal static int GetIntOrZero(JObject json, string propertyName)
        {
            JToken token = null;
            var x = 0;

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

            var s = GetStringOrNull(json, propertyName);

            if (!String.IsNullOrEmpty(s))
            {
                uri = new Uri(s);
            }

            return uri;
        }

        internal static Guid GetGuidOrEmpty(JObject json, string propertyName)
        {
            var guid = Guid.Empty;

            var s = GetStringOrNull(json, propertyName);

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
                var array = token as JArray;

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

            var s = GetStringOrNull(json, propertyName);

            if (!String.IsNullOrEmpty(s))
            {
                NuGetVersion.TryParse(s, out version);
            }

            return version;
        }
    }
}
