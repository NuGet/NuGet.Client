// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.DependencyInfo
{
    internal static class Utils
    {
        public static VersionRange CreateVersionRange(string stringToParse, bool includePrerelease)
        {
            var range = VersionRange.Parse(string.IsNullOrEmpty(stringToParse) ? "[0.0.0-alpha,)" : stringToParse);
            return new VersionRange(range.MinVersion, range.IsMinInclusive, range.MaxVersion, range.IsMaxInclusive, includePrerelease);
        }

        public static async Task<JObject> GetJObjectAsync(HttpClient httpClient, Uri registrationUri)
        {
            var json = await httpClient.GetStringAsync(registrationUri);
            return JObject.Parse(json);
        }

        public static string Indent(int depth)
        {
            return new string(Enumerable.Repeat(' ', depth).ToArray());
        }

        public static async Task<JObject> LoadResource(HttpClient httpClient, Uri uri, CancellationToken token)
        {
            var response = await httpClient.GetAsync(uri, token);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(json);

            return obj;
        }

        public async static Task<IEnumerable<JObject>> LoadRanges(
            HttpClient httpClient,
            Uri registrationUri,
            VersionRange range,
            CancellationToken token)
        {
            var index = await LoadResource(httpClient, registrationUri, token);

            if (index == null)
            {
                // The server returned a 404, the package does not exist
                return Enumerable.Empty<JObject>();
            }

            var preFilterRange = VersionRange.SetIncludePrerelease(range, includePrerelease: true);

            IList<Task<JObject>> rangeTasks = new List<Task<JObject>>();

            foreach (JObject item in index["items"])
            {
                var lower = NuGetVersion.Parse(item["lower"].ToString());
                var upper = NuGetVersion.Parse(item["upper"].ToString());

                if (IsItemRangeRequired(preFilterRange, lower, upper))
                {
                    JToken items;
                    if (!item.TryGetValue("items", out items))
                    {
                        var rangeUri = item["@id"].ToObject<Uri>();

                        rangeTasks.Add(LoadResource(httpClient, rangeUri, token));
                    }
                    else
                    {
                        rangeTasks.Add(Task.FromResult(item));
                    }
                }
            }

            await Task.WhenAll(rangeTasks.ToArray());

            return rangeTasks.Select((t) => t.Result);
        }

        private static bool IsItemRangeRequired(VersionRange dependencyRange, NuGetVersion catalogItemLower, NuGetVersion catalogItemUpper)
        {
            var catalogItemVersionRange = new VersionRange(minVersion: catalogItemLower, includeMinVersion: true,
                maxVersion: catalogItemUpper, includeMaxVersion: true, includePrerelease: true);

            if (dependencyRange.HasLowerAndUpperBounds) // Mainly to cover the '!dependencyRange.IsMaxInclusive && !dependencyRange.IsMinInclusive' case
            {
                return catalogItemVersionRange.Satisfies(dependencyRange.MinVersion) || catalogItemVersionRange.Satisfies(dependencyRange.MaxVersion);
            }
            else
            {
                return dependencyRange.Satisfies(catalogItemLower) || dependencyRange.Satisfies(catalogItemUpper);
            }
        }
    }
}
