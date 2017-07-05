// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement.VisualStudio
{
    public class VSRestoreSettingsUtilities
    {
        private static string RestoreAdditionalProjectFallbackFolders = nameof(RestoreAdditionalProjectFallbackFolders);
        private static string RestoreAdditionalProjectSources = nameof(RestoreAdditionalProjectSources);

        public static string GetPackagesPath(ISettings settings, PackageSpec project)
        {
            // Set from Settings if not given. Clear is not an option here.
            if (string.IsNullOrEmpty(project.RestoreMetadata.PackagesPath))
            {
                return SettingsUtility.GetGlobalPackagesFolder(settings);
            }

            // Resolve relative paths
            return UriUtility.GetAbsolutePathFromFile(
                sourceFile: project.RestoreMetadata.ProjectPath,
                path: project.RestoreMetadata.PackagesPath);
        }

        public static IList<PackageSource> GetSources(ISettings settings, PackageSpec project)
        {
            var sources = new List<string>();
            var additionalSources = new List<string>();

            var readingAdditionalSources = false;
            foreach (var source in project.RestoreMetadata.Sources)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(RestoreAdditionalProjectSources,source.Source))
                {
                    readingAdditionalSources = true;
                }
                else
                {
                    if (readingAdditionalSources)
                    {
                        additionalSources.Add(source.Source);
                    }
                    else
                    {
                        sources.Add(source.Source);
                    }
                }
            }

            var processedSources = (
                        ShouldReadFromSettings(sources) ?
                            SettingsUtility.GetEnabledSources(settings).Select(e => e.Source) :
                            HandleClear(sources))
                        .Concat(additionalSources);

            // Resolve relative paths
            return processedSources.Select(e => new PackageSource(
                UriUtility.GetAbsolutePathFromFile(
                    sourceFile: project.RestoreMetadata.ProjectPath,
                    path: e)))
                .ToList();
        }

        public static IList<string> GetFallbackFolders(ISettings settings, PackageSpec project)
        {
            var fallbackFolders = new List<string>();
            var additionalFallbackFolders = new List<string>();

            var readingAdditionalFallbackFolders = false;
            foreach (var fallbackFolder in project.RestoreMetadata.FallbackFolders)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(RestoreAdditionalProjectFallbackFolders,fallbackFolder))
                {
                    readingAdditionalFallbackFolders = true;
                }
                else
                {
                    if (readingAdditionalFallbackFolders)
                    {
                        additionalFallbackFolders.Add(fallbackFolder);
                    }
                    else
                    {
                        fallbackFolders.Add(fallbackFolder);
                    }
                }
            }

            var processedFallbackFolders = (
                        ShouldReadFromSettings(fallbackFolders) ?
                            SettingsUtility.GetFallbackPackageFolders(settings) :
                            HandleClear(fallbackFolders))
                        .Concat(additionalFallbackFolders);


            // Resolve relative paths
            return processedFallbackFolders.Select(e =>
                UriUtility.GetAbsolutePathFromFile(
                    sourceFile: project.RestoreMetadata.ProjectPath,
                    path: e))
                .ToList();
        }

        private static bool ShouldReadFromSettings(IEnumerable<string> values)
        {
            return !values.Any() && !MSBuildRestoreUtility.ContainsClearKeyword(values);
        }

        public static IEnumerable<string> HandleClear(IEnumerable<string> values)
        {
            
            if ((MSBuildRestoreUtility.ContainsClearKeyword(values)))
            {
                return Enumerable.Empty<string>();
            }

            return values;
        }
    }
}
