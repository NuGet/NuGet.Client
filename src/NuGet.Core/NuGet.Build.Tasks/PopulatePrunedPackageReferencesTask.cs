// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace NuGet.Build.Tasks
{
    public class PopulatePrunedPackageReferencesTask : Task
    {
        /// <summary>
        /// Full path to the msbuild project.
        /// </summary>
        [Required]
        public string RuntimeGraphPath { get; set; }

        [Required]
        public string TargetFrameworkIdentifier { get; set; }

        [Required]
        public string TargetFrameworkVersion { get; set; }

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public ITaskItem[] PrunedPackageReferences { get; set; }

        public override bool Execute()
        {
            var entries = new List<ITaskItem>();
            var rootDotnetPath = Path.GetFullPath(Path.Combine(RuntimeGraphPath, "..", "..", ".."));
            var directory = new DirectoryInfo(Path.Combine(rootDotnetPath, "packs", "Microsoft.NETCore.App.Ref"));

            if (directory.Exists && TargetFrameworkIdentifier == ".NETCoreApp")
            {
                var files = directory.GetDirectories();
                string packageOverridesFile = null;
                foreach (var file in files)
                {
                    var frameworkVersion = TargetFrameworkVersion.TrimStart('v'); // TODO NK.
                    if (file.Name.StartsWith(frameworkVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        packageOverridesFile = Path.Combine(file.FullName, "data", "PackageOverrides.txt");
                        break;
                    }
                }

                if (packageOverridesFile != null)
                {
                    if (File.Exists(packageOverridesFile))
                    {
                        string[] text = File.ReadAllLines(packageOverridesFile);
                        foreach (var line in text)
                        {
                            string[] elements = line.Split('|');
                            var packageId = elements[0];
                            var packageVersion = elements[1];
                            var properties = new Dictionary<string, string>
                            {
                                { "Id", packageId },
                                { "Version", packageVersion }
                            };
                            entries.Add(new TaskItem(Guid.NewGuid().ToString(), properties));
                        }
                    }
                }
            }
            PrunedPackageReferences = entries.ToArray();
            return true;
        }
    }
}
