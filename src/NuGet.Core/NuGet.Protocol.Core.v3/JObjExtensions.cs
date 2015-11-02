// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.Core.v3
{
    public static class JObjectExtensions
    {
        public static int? GetInt(this JObject json, string property)
        {
            var value = json[property] as JValue;
            if (value == null || value.Type != JTokenType.Integer)
            {
                return null;
            }

            return (int)value.Value;
        }

        public static long? GetLong(this JObject json, string property)
        {
            var value = json[property] as JValue;
            if (value == null || value.Type != JTokenType.Integer)
            {
                return null;
            }

            return (long)value.Value;
        }

        public static bool? GetBoolean(this JObject json, string property)
        {
            var value = json[property] as JValue;
            if (value == null || value.Type != JTokenType.Boolean)
            {
                return null;
            }

            return (bool)value.Value;
        }

        public static DateTimeOffset? GetDateTime(this JObject json, string property)
        {
            var value = json[property] as JValue;
            if (value == null || value.Type != JTokenType.Date)
            {
                return null;
            }

            return value.ToObject<DateTimeOffset>();
        }

        public static Uri GetUri(this JObject json, string property)
        {
            var str = json.Value<string>(property);
            Uri uri = null;
            if (Uri.TryCreate(str, UriKind.Absolute, out uri))
            {
                return uri;
            }
            else
            {
                return null;
            }
        }

        public static JArray GetJArray(this JObject json, string property)
        {
            var value = json[property] as JArray;
            return value;
        }
    }
}