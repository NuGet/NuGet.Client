using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace NuGet.Protocol.Core.v3.DependencyInfo
{
    internal static class Utils
    {
        public static VersionRange CreateVersionRange(string stringToParse, bool includePrerelease)
        {
            VersionRange range = VersionRange.Parse(string.IsNullOrEmpty(stringToParse) ? "[0.0.0-alpha,)" : stringToParse);
            return new VersionRange(range.MinVersion, range.IsMinInclusive, range.MaxVersion, range.IsMaxInclusive, includePrerelease);
        }

        public static async Task<JObject> GetJObjectAsync(HttpClient httpClient, Uri registrationUri)
        {
            string json = await httpClient.GetStringAsync(registrationUri);
            return JObject.Parse(json);
        }

        public static VersionRange SetIncludePrerelease(VersionRange range, bool includePrerelease)
        {
            return new VersionRange(range.MinVersion, range.IsMinInclusive, range.MaxVersion, range.IsMaxInclusive, includePrerelease);
        }

        public static string Indent(int depth)
        {
            return new string(Enumerable.Repeat(' ', depth).ToArray());
        }

    }
}
