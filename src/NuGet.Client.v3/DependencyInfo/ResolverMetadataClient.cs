using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Client.DependencyInfo
{
    internal static class ResolverMetadataClient
    {
        public static async Task<RegistrationInfo> GetTree(HttpClient httpClient, RegistrationInfo registrationInfo, NuGetFramework projectFramework, ConcurrentDictionary<Uri, JObject> sessionCache, Func<IDictionary<NuGetVersion, HashSet<string>>, IDictionary<NuGetVersion, HashSet<string>>> filter = null)
        {
            //ApplyFilter(registrationInfo, filter);

            await InlineDependencies(httpClient, registrationInfo, projectFramework, sessionCache, filter);

            return registrationInfo;
        }

        private static async Task InlineDependencies(HttpClient httpClient, RegistrationInfo registrationInfo, NuGetFramework projectFramework, ConcurrentDictionary<Uri, JObject> sessionCache, Func<IDictionary<NuGetVersion, HashSet<string>>, IDictionary<NuGetVersion, HashSet<string>>> filter)
        {
            foreach (PackageInfo packageInfo in registrationInfo.Packages)
            {
                foreach (DependencyInfo dependencyInfo in packageInfo.Dependencies)
                {
                    dependencyInfo.RegistrationInfo = await GetRegistrationInfo(httpClient, dependencyInfo.RegistrationUri, dependencyInfo.Range, projectFramework, sessionCache);

                    //ApplyFilter(dependencyInfo.RegistrationInfo, filter);

                    await InlineDependencies(httpClient, dependencyInfo.RegistrationInfo, projectFramework, sessionCache, filter);
                }
            }
        }

        //private static void ApplyFilter(RegistrationInfo registrationInfo, Func<IDictionary<NuGetVersion, HashSet<string>>, IDictionary<NuGetVersion, HashSet<string>>> filter)
        //{
        //    if (registrationInfo.Id == null)
        //    {
        //        return;
        //    }

        //    IDictionary<NuGetVersion, HashSet<string>> before = new Dictionary<NuGetVersion, HashSet<string>>();

        //    foreach (PackageInfo packageInfo in registrationInfo.Packages)
        //    {
        //        HashSet<string> targetFrameworks = new HashSet<string>();
        //        foreach (DependencyGroupInfo dependencyGroupInfo in packageInfo.Dependencies)
        //        {
        //            targetFrameworks.Add(dependencyGroupInfo.TargetFramework);
        //        }
        //        before.Add(packageInfo.Version, targetFrameworks);
        //    }

        //    IDictionary<NuGetVersion, HashSet<string>> after = filter(before);

        //    foreach (PackageInfo packageInfo in registrationInfo.Packages)
        //    {
        //        HashSet<string> dependencyGroupsToRetain = after[packageInfo.Version];

        //        IList<DependencyGroupInfo> dependencyGroupsToRemove = new List<DependencyGroupInfo>();

        //        foreach (DependencyGroupInfo dependencyGroupInfo in packageInfo.Dependencies)
        //        {
        //            if (!dependencyGroupsToRetain.Contains(dependencyGroupInfo.TargetFramework))
        //            {
        //                dependencyGroupsToRemove.Add(dependencyGroupInfo);
        //            }
        //        }

        //        foreach (DependencyGroupInfo dependencyGroupInfo in dependencyGroupsToRemove)
        //        {
        //            packageInfo.Dependencies.Remove(dependencyGroupInfo);
        //        }
        //    }
        //}

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

        public static async Task<RegistrationInfo> GetRegistrationInfo(HttpClient httpClient, Uri registrationUri, VersionRange range, NuGetFramework projectTargetFramework, ConcurrentDictionary<Uri, JObject> sessionCache = null)
        {
            FrameworkReducer frameworkReducer = new FrameworkReducer();
            JObject index = await LoadResource(httpClient, registrationUri, sessionCache);

            if (index == null)
            {
                throw new ArgumentException(registrationUri.AbsoluteUri);
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

                        rangeTasks.Add(LoadResource(httpClient, registrationUri, sessionCache));
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

                    if (range.Satisfies(packageVersion))
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

                            foreach (JObject dependencyGroupObj in dependencyGroupsArray)
                            {
                                if (GetFramework(dependencyGroupObj).Equals(targetFramework))
                                {
                                    foreach (JObject dependencyObj in dependencyGroupObj["dependencies"])
                                    {
                                        DependencyInfo dependencyInfo = new DependencyInfo();
                                        dependencyInfo.Id = dependencyObj["id"].ToString();
                                        dependencyInfo.Range = Utils.CreateVersionRange((string)dependencyObj["range"], range.IncludePrerelease);
                                        dependencyInfo.RegistrationUri = dependencyObj["registration"].ToObject<Uri>();

                                        packageInfo.Dependencies.Add(dependencyInfo);
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

        private static bool IsItemRangeRequired(VersionRange preFilterRange, NuGetVersion lower, NuGetVersion upper)
        {
            VersionRange itemRange = new VersionRange(lower, true, upper, true, true);

            if (preFilterRange.HasLowerAndUpperBounds)
            {
                return itemRange.Satisfies(preFilterRange.MinVersion) || itemRange.Satisfies(preFilterRange.MaxVersion);
            }
            else if (preFilterRange.HasLowerBound)
            {
                return upper > preFilterRange.MinVersion;
            }
            else if (preFilterRange.HasUpperBound)
            {
                return lower < preFilterRange.MaxVersion;
            }
            return false;
        }
    }
}
