// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.DependencyInfo
{
    internal static class Utils
    {
        public static VersionRange CreateVersionRange(string stringToParse)
        {
            var range = VersionRange.Parse(string.IsNullOrEmpty(stringToParse) ? "[0.0.0-alpha,)" : stringToParse);
            return new VersionRange(range.MinVersion, range.IsMinInclusive, range.MaxVersion, range.IsMaxInclusive);
        }

        public async static Task<IEnumerable<JObject>> LoadRanges(
            HttpSource httpClient,
            Uri registrationUri,
            VersionRange range,
            ILogger log,
            CancellationToken token)
        {
            var index = await httpClient.GetJObjectAsync(registrationUri, ignoreNotFounds: true, log: log, token: token);

            if (index == null)
            {
                // The server returned a 404, the package does not exist
                return Enumerable.Empty<JObject>();
            }

            IList<Task<JObject>> rangeTasks = new List<Task<JObject>>();

            foreach (JObject item in index["items"])
            {
                var lower = NuGetVersion.Parse(item["lower"].ToString());
                var upper = NuGetVersion.Parse(item["upper"].ToString());

                if (IsItemRangeRequired(range, lower, upper))
                {
                    JToken items;
                    if (!item.TryGetValue("items", out items))
                    {
                        var rangeUri = item["@id"].ToObject<Uri>();

                        rangeTasks.Add(httpClient.GetJObjectAsync(
                            rangeUri,
                            ignoreNotFounds: true,
                            log: log,
                            token: token));
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
                maxVersion: catalogItemUpper, includeMaxVersion: true);

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
