using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
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
        /// Retrieve the <see cref="RemoteSourceDependencyInfo"/> for a registration.
        /// </summary>
        /// <returns>Returns an empty sequence if the package does not exist.</returns>
        public static async Task<IEnumerable<RemoteSourceDependencyInfo>> GetDependencies(
            HttpClient httpClient,
            Uri registrationUri,
            VersionRange range,
            ConcurrentDictionary<Uri, JObject> sessionCache)
        {
            JObject index = await LoadResource(httpClient, registrationUri, sessionCache);

            if (index == null)
            {
                // The server returned a 404, the package does not exist
                return Enumerable.Empty<RemoteSourceDependencyInfo>();
            }

            VersionRange preFilterRange = Utils.SetIncludePrerelease(range, true);

            IList<Task<JObject>> rangeTasks = new List<Task<JObject>>();

            foreach (JObject item in index["items"])
            {
                NuGetVersion lower = NuGetVersion.Parse(item["lower"].ToString());
                NuGetVersion upper = NuGetVersion.Parse(item["upper"].ToString());

                if (IsItemRangeRequired(preFilterRange, lower, upper))
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

            string id = string.Empty;

            var results = new HashSet<RemoteSourceDependencyInfo>();
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
                    if (range.Satisfies(packageVersion) && (publishedDate != 19000101))
                    {
                        var identity = new PackageIdentity(id, packageVersion);
                        var dependencyGroups = new List<PackageDependencyGroup>();

                        JArray dependencyGroupsArray = (JArray)catalogEntry["dependencyGroups"];

                        if (dependencyGroupsArray != null)
                        {
                            foreach (JObject dependencyGroupObj in dependencyGroupsArray)
                            {
                                NuGetFramework currentFramework = GetFramework(dependencyGroupObj);

                                var groupDependencies = new List<PackageDependency>();

                                JToken dependenciesObj;

                                // Packages with no dependencies have 'dependencyGroups' but no 'dependencies'
                                if (dependencyGroupObj.TryGetValue("dependencies", out dependenciesObj))
                                {
                                    foreach (JObject dependencyObj in dependenciesObj)
                                    {
                                        var dependencyId = dependencyObj["id"].ToString();
                                        var dependencyRange = Utils.CreateVersionRange((string)dependencyObj["range"], range.IncludePrerelease);

                                        groupDependencies.Add(new PackageDependency(dependencyId, dependencyRange));
                                    }
                                }

                                dependencyGroups.Add(new PackageDependencyGroup(currentFramework, groupDependencies));
                            }
                        }

                        var contentUri = packageObj.Value<string>("packageContent");
                        var dependencyInfo = new RemoteSourceDependencyInfo(identity, dependencyGroups, contentUri);

                        results.Add(dependencyInfo);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Retrieve a registration blob
        /// </summary>
        /// <returns>Returns Null if the package does not exist</returns>
        public static async Task<RegistrationInfo> GetRegistrationInfo(
            HttpClient httpClient,
            Uri registrationUri,
            VersionRange range,
            NuGetFramework projectTargetFramework,
            ConcurrentDictionary<Uri, JObject> sessionCache = null)
        {
            NuGetFrameworkFullComparer frameworkComparer = new NuGetFrameworkFullComparer();
            FrameworkReducer frameworkReducer = new FrameworkReducer();
            var dependencies = await GetDependencies(httpClient, registrationUri, range, sessionCache);

            var result = new HashSet<RegistrationInfo>();
            RegistrationInfo registrationInfo = new RegistrationInfo();

            registrationInfo.IncludePrerelease = range.IncludePrerelease;
            foreach (var item in dependencies)
            {
                PackageInfo packageInfo = new PackageInfo
                {
                    Version = item.Identity.Version,
                    PackageContent = new Uri(item.ContentUri)
                };

                // only one target framework group will be used at install time, which means 
                // we can filter down to that group now by using the project target framework
                var depFrameworks = item.DependencyGroups.Select(e => e.TargetFramework);
                var targetFramework = frameworkReducer.GetNearest(projectTargetFramework, depFrameworks);

                // If no frameworks are compatible we just ignore them - Should this be an exception?
                if (targetFramework != null)
                {
                    var dependencyGroup = item.DependencyGroups.FirstOrDefault(d => frameworkComparer.Equals(targetFramework, d.TargetFramework));
                    if (dependencyGroup != null)
                    {
                        foreach (var dependency in dependencyGroup.Packages)
                        {
                            var dependencyInfo = new DependencyInfo
                            {
                                Id = dependency.Id,
                                Range = dependency.VersionRange
                            };

                            packageInfo.Dependencies.Add(dependencyInfo);
                        }
                    }
                }

                registrationInfo.Add(packageInfo);
                registrationInfo.Id = item.Identity.Id;
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

        private static bool IsItemRangeRequired(VersionRange dependencyRange, NuGetVersion catalogItemLower, NuGetVersion catalogItemUpper)
        {
            VersionRange catalogItemVersionRange = new VersionRange(minVersion: catalogItemLower, includeMinVersion: true,
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
