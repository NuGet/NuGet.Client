// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.VisualStudio
{
    internal static class JObjectExtensions
    {
        /// <summary>
        /// Returns a field value or the empty string. Arrays will become comma delimited strings.
        /// </summary>
        public static string GetField(this JObject json, string property)
        {
            var value = json[property];

            if (value == null)
            {
                return string.Empty;
            }

            var array = value as JArray;

            if (array != null)
            {
                return string.Join(", ", array.Select(e => e.ToString()));
            }

            return value.ToString();
        }

        public static int GetInt(this JObject json, string property)
        {
            var value = json[property];

            if (value == null)
            {
                return 0;
            }

            return value.ToObject<int>();
        }

        public static DateTimeOffset? GetDateTime(this JObject json, string property)
        {
            var value = json[property];

            if (value == null)
            {
                return null;
            }

            return value.ToObject<DateTimeOffset>();
        }

        public static Uri GetUri(this JObject json, string property)
        {
            if (json[property] == null)
            {
                return null;
            }

            var str = json[property].ToString();

            if (String.IsNullOrEmpty(str))
            {
                return null;
            }

            return new Uri(str);
        }
    }
}
