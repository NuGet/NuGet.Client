using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.DependencyInfo
{
    internal static class ResolverMetadataClient
    {
        private static async Task<JObject> LoadResource(HttpClient httpClient, Uri uri, ConcurrentDictionary<Uri, JObject> sessionCache)
        {
            JObject obj;
            if (sessionCache != null && sessionCache.TryGetValue(uri, out obj))
            {
                return obj;
            }

            HttpResponseMessage response = await httpClient.GetAsync(uri);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            obj = JObject.Parse(json);

            if (sessionCache != null)
            {
                sessionCache.TryAdd(uri, obj);
            }

            return obj;
        }

        /// <summary>
        /// Retrieve a registration blob
        /// </summary>
        /// <returns>Returns Null if the package does not exist</returns>
        public static async Task<RegistrationInfo> GetRegistrationInfo(HttpClient httpClient, Uri registrationUri, VersionRange range, NuGetFramework projectTargetFramework, ConcurrentDictionary<Uri, JObject> sessionCache = null)
        {
            NuGetFrameworkFullComparer frameworkComparer = new NuGetFrameworkFullComparer();
            FrameworkReducer frameworkReducer = new FrameworkReducer();
            JObject index = await LoadResource(httpClient, registrationUri, sessionCache);

            if (index == null)
            {
                // The server returned a 404, the package does not exist
                return null;
            }

            VersionRange preFilterRange = Utils.SetIncludePrerelease(range, true);

            IList<Task<JObject>> rangeTasks = new List<Task<JObject>>();

            foreach (JObject item in index["items"])
            {
                NuGetVersion lower = NuGetVersion.Parse(item["lower"].ToString());
                NuGetVersion upper = NuGetVersion.Parse(item["upper"].ToString());

                if (ResolverMetadataClientUtility.IsItemRangeRequired(preFilterRange, lower, upper))
                {
                    JToken items;
                    if (!item.TryGetValue("items", out items))
                    {
                        Uri rangeUri = item["@id"].ToObject<Uri>();

                        rangeTasks.Add(LoadResource(httpClient, rangeUri, sessionCache));
                    }
                    else
                    {
                        rangeTasks.Add(Task.FromResult(item));
                    }
                }
            }

            await Task.WhenAll(rangeTasks.ToArray());

            RegistrationInfo registrationInfo = new RegistrationInfo();

            registrationInfo.IncludePrerelease = range.IncludePrerelease;

            string id = string.Empty;

            foreach (JObject rangeObj in rangeTasks.Select((t) => t.Result))
            {
                if (rangeObj == null)
                {
                    throw new InvalidDataException(registrationUri.AbsoluteUri);
                }

                foreach (JObject packageObj in rangeObj["items"])
                {
                    JObject catalogEntry = (JObject)packageObj["catalogEntry"];

                    NuGetVersion packageVersion = NuGetVersion.Parse(catalogEntry["version"].ToString());

                    id = catalogEntry["id"].ToString();

                    int publishedDate = 0;
                    JToken publishedValue;

                    if (catalogEntry.TryGetValue("published", out publishedValue))
                    {
                        publishedDate = int.Parse(publishedValue.ToObject<DateTime>().ToString("yyyyMMdd"));
                    }

                    //publishedDate = 0 means the property doesn't exist in index.json
                    //publishedDate = 19000101 means the property exists but the package is unlisted
                    if (range.Satisfies(packageVersion) && (publishedDate!= 19000101))
                    {
                        PackageInfo packageInfo = new PackageInfo();
                        packageInfo.Version = packageVersion;
                        packageInfo.PackageContent = new Uri(packageObj["packageContent"].ToString());

                        JArray dependencyGroupsArray = (JArray)catalogEntry["dependencyGroups"];

                        if (dependencyGroupsArray != null)
                        {
                            // only one target framework group will be used at install time, which means 
                            // we can filter down to that group now by using the project target framework
                            var depFrameworks = dependencyGroupsArray.Select(e => GetFramework(e as JObject));

                            var targetFramework = frameworkReducer.GetNearest(projectTargetFramework, depFrameworks);

                            // If no frameworks are compatible we just ignore them - Should this be an exception?
                            if (targetFramework != null)
                            {
                                foreach (JObject dependencyGroupObj in dependencyGroupsArray)
                                {
                                    NuGetFramework currentFramework = GetFramework(dependencyGroupObj);

                                    if (frameworkComparer.Equals(currentFramework, targetFramework))
                                    {
                                        JToken dependenciesObj = null;

                                        // Packages with no dependencies have 'dependencyGroups' but no 'dependencies'
                                        if (dependencyGroupObj.TryGetValue("dependencies", out dependenciesObj))
                                        {
                                            foreach (JObject dependencyObj in dependenciesObj)
                                            {
                                                DependencyInfo dependencyInfo = new DependencyInfo();
                                                dependencyInfo.Id = dependencyObj["id"].ToString();
                                                dependencyInfo.Range = Utils.CreateVersionRange((string)dependencyObj["range"], range.IncludePrerelease);
                                                dependencyInfo.RegistrationUri = dependencyObj["registration"].ToObject<Uri>();

                                                packageInfo.Dependencies.Add(dependencyInfo);
                                            }
                                        }

                                        // Take the first group that matches
                                        break;
                                    }
                                }
                            }
                        }

                        registrationInfo.Add(packageInfo);
                    }
                }

                registrationInfo.Id = id;
            }

            return registrationInfo;
        }

        /// <summary>
        /// Retrieve the target framework from a dependency group obj
        /// </summary>
        private static NuGetFramework GetFramework(JObject dependencyGroupObj)
        {
            NuGetFramework framework = NuGetFramework.AnyFramework;

            if (dependencyGroupObj["targetFramework"] != null)
            {
                framework = NuGetFramework.Parse(dependencyGroupObj["targetFramework"].ToString());
            }

            return framework;
        }
    }

    public static class ResolverMetadataClientUtility
    {
        public static bool IsItemRangeRequired(VersionRange dependencyRange, NuGetVersion catalogItemLower, NuGetVersion catalogItemUpper)
        {
            VersionRange catalogItemVersionRange = new VersionRange(minVersion: catalogItemLower, includeMinVersion: true,
                maxVersion: catalogItemUpper, includeMaxVersion: true, includePrerelease: true);

            if(dependencyRange.HasLowerAndUpperBounds) // Mainly to cover the '!dependencyRange.IsMaxInclusive && !dependencyRange.IsMinInclusive' case
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
