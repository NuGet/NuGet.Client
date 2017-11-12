// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.ProjectManagement.Projects;
using NuGet.Versioning;

namespace NuGet.ProjectManagement
{
    public class DependencyVersionLookup
    {
        private DateTime? _assetsFileCreatedAt;
        private Dictionary<string, NuGetVersion> _versionLookup;

        public DependencyVersionLookup()
        {
            _versionLookup = new Dictionary<string, NuGetVersion>();
            _assetsFileCreatedAt = null;
        }

        public void Update(Dictionary<string, NuGetVersion> newLookup, DateTime createdAt)
        {
            _versionLookup = newLookup;
            _assetsFileCreatedAt = createdAt;
        }

        public bool TryGet(string packageId, out NuGetVersion version)
        {
            version = null;
            if (_versionLookup == null ||!_versionLookup.Any())
            {
                return false;
            }
            return _versionLookup.TryGetValue(packageId, out version);
        }

        public bool AssetsFileHasBeenRead()
        {
            return _assetsFileCreatedAt != null;
        }

        public bool AssetsFileHasChanged(DateTime assetsFileTimestamp)
        {
            return _assetsFileCreatedAt.Equals(assetsFileTimestamp);
        }

        // This method reads the assets file and constructs the appropriate object with references
        // to the installed dependencies found there. It only reads the assets file if it hasn't
        // changed or it hasn't been read before.
        public static async Task LoadAssetsFileAndCreateLookupForProjectAsync(INuGetIntegratedProject project)
        {
            if (project != null)
            {
                var assetFilechanged = false;
                DateTime? assetsFileTimestamp = null;
                if (project.Lookup.AssetsFileHasBeenRead())
                {
                    assetsFileTimestamp = await project.GetAssetsFileTimestampIFExistsAsync();
                    if (assetsFileTimestamp != null)
                    {
                        assetFilechanged = project.Lookup.AssetsFileHasChanged(assetsFileTimestamp.Value);
                    }
                }

                // We should only read the assets file if we haven't read it before or if the file changed
                if (!project.Lookup.AssetsFileHasBeenRead() || assetFilechanged)
                {
                    // If there is no assets file this will return an empty list
                    var dependencies = await project.GetTopLevelDependencies();
                    if (dependencies != null && dependencies.Any())
                    {
                        if (assetsFileTimestamp == null)
                        {
                            assetsFileTimestamp = await project.GetAssetsFileTimestampIFExistsAsync();
                        }
                        // If we are targeting multiple frameworks we should get the Min version to show (WIP: Add to spec and ask for feedback)
                        project.Lookup.Update(dependencies.GroupBy(item => item.Id).ToDictionary(x => x.Key, x => x.Min(y => y.Version)), assetsFileTimestamp.Value);
                    }
                }
            }
        }
    }
}