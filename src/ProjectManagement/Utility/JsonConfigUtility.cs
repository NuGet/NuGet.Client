// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// nuget.json utils
    /// </summary>
    public static class JsonConfigUtility
    {
        /// <summary>
        /// Read dependencies from a nuget.json file
        /// </summary>
        public static IEnumerable<PackageDependency> GetDependencies(JObject json)
        {
            JToken node = null;
            if (json.TryGetValue("dependencies", out node))
            {
                foreach (var dependency in node)
                {
                    yield return ParseDependency(dependency);
                }
            }
        }

        /// <summary>
        /// Convert a dependency entry into an id and version range
        /// </summary>
        public static PackageDependency ParseDependency(JToken dependencyToken)
        {
            var property = dependencyToken as JProperty;

            var id = property.Name;

            VersionRange range = null;

            if (dependencyToken.Type == JTokenType.Property)
            {
                range = VersionRange.Parse(((JProperty)dependencyToken).Value.ToString());
            }
            else
            {
                range = VersionRange.Parse(((JProperty)dependencyToken["version"]).Value.ToString());
            }

            return new PackageDependency(id, range);
        }

        /// <summary>
        /// Add a dependency to a nuget.json file
        /// </summary>
        public static void AddDependency(JObject json, PackageDependency dependency)
        {
            // Removing the older package if it exists
            RemoveDependency(json, dependency.Id);

            JObject dependencySet = null;

            JToken node = null;
            if (json.TryGetValue("dependencies", out node))
            {
                dependencySet = node as JObject;
            }

            if (dependencySet == null)
            {
                dependencySet = new JObject();
                json["dependencies"] = dependencySet;
            }

            var packageProperty = new JProperty(dependency.Id, dependency.VersionRange.MinVersion.ToNormalizedString());
            dependencySet.Add(packageProperty);
        }

        /// <summary>
        /// Remove a dependency from a nuget.json file
        /// </summary>
        public static void RemoveDependency(JObject json, string packageId)
        {
            JToken node = null;
            if (json.TryGetValue("dependencies", out node))
            {
                foreach (var dependency in node.ToArray())
                {
                    var dependencyProperty = dependency as JProperty;
                    if (StringComparer.OrdinalIgnoreCase.Equals(dependencyProperty.Name, packageId))
                    {
                        dependency.Remove();
                    }
                }
            }
        }

        /// <summary>
        /// Retrieve the NuGetFrameworks under frameworks
        /// </summary>
        public static IEnumerable<NuGetFramework> GetFrameworks(JObject json)
        {
            var results = new List<NuGetFramework>();

            JToken node = null;
            if (json.TryGetValue("frameworks", out node))
            {
                foreach (var frameworkNode in node.ToArray())
                {
                    var frameworkProperty = frameworkNode as JProperty;

                    if (frameworkProperty != null)
                    {
                        results.Add(NuGetFramework.Parse(frameworkProperty.Name));
                    }
                }
            }

            return results;
        }
    }
}
