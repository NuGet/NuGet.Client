// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Shared;

namespace NuGet.Build.Tasks
{
    public class GetRestorePackageDownloadsTask : Task
    {
        /// <summary>
        /// Full path to the msbuild project.
        /// </summary>
        [Required]
        public string ProjectUniqueName { get; set; }

        [Required]
        public ITaskItem[] PackageDownloads { get; set; }

        /// <summary>
        /// Target frameworks to apply this for. If empty this applies to all.
        /// </summary>
        public string TargetFrameworks { get; set; }

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public ITaskItem[] RestoreGraphItems { get; set; }

        public override bool Execute()
        {
            var log = new MSBuildLogger(Log);
            log.LogDebug($"(in) ProjectUniqueName '{ProjectUniqueName}'");
            log.LogDebug($"(in) TargetFrameworks '{TargetFrameworks}'");
            log.LogDebug($"(in) PackageDownloads '{string.Join(";", PackageDownloads.Select(p => p.ItemSpec))}'");

            var entries = new List<ITaskItem>();
            var seenIds = new HashSet<Tuple<string, string>>(new CustomEqualityComparer());

            foreach (var msbuildItem in PackageDownloads)
            {
                var packageId = msbuildItem.ItemSpec;

                var properties = new Dictionary<string, string>();
                properties.Add("ProjectUniqueName", ProjectUniqueName);
                properties.Add("Type", "DownloadDependency");
                properties.Add("Id", packageId);
                BuildTasksUtility.CopyPropertyIfExists(msbuildItem, properties, "Version", "VersionRange");

                properties.TryGetValue("VersionRange", out var versionRange);

                var key = new Tuple<string, string>(packageId, versionRange);

                if (string.IsNullOrEmpty(packageId) || !seenIds.Add(key))
                {
                    // Skip duplicate id/version combinations
                    continue;
                }

                if (!string.IsNullOrEmpty(TargetFrameworks))
                {
                    properties.Add("TargetFrameworks", TargetFrameworks);
                }

                entries.Add(new TaskItem(Guid.NewGuid().ToString(), properties));
            }

            RestoreGraphItems = entries.ToArray();

            return true;
        }

        private class CustomEqualityComparer : IEqualityComparer<Tuple<string, string>>
        {

            public bool Equals(Tuple<string, string> lhs, Tuple<string, string> rhs)
            {
                return StringComparer.OrdinalIgnoreCase.Equals(lhs.Item1, rhs.Item1)
               && StringComparer.OrdinalIgnoreCase.Equals(lhs.Item2, rhs.Item2);
            }

            public int GetHashCode(Tuple<string, string> tuple)
            {
                var combiner = new HashCodeCombiner();
                combiner.AddStringIgnoreCase(tuple.Item1);
                combiner.AddStringIgnoreCase(tuple.Item2);
                return combiner.CombinedHash;
            }
        }
    }
}