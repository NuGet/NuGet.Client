// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using NuGet.ProjectManagement;

namespace NuGet.SolutionRestoreManager
{
    internal record ProjectRestoreInfo3Adapter : IVsProjectRestoreInfo3
    {
        public required string BaseIntermediatePath { get; init; }

        public required string OriginalTargetFrameworks { get; init; }

        public required IReadOnlyList<IVsTargetFrameworkInfo4> TargetFrameworks { get; init; }

        public required IReadOnlyList<IVsReferenceItem> ToolReferences { get; init; }

        public static ProjectRestoreInfo3Adapter Create(IVsProjectRestoreInfo projectRestoreInfo)
        {
            if (projectRestoreInfo is null) { throw new ArgumentNullException(nameof(projectRestoreInfo)); }

            return new ProjectRestoreInfo3Adapter
            {
                BaseIntermediatePath = projectRestoreInfo.BaseIntermediatePath,
                OriginalTargetFrameworks = projectRestoreInfo.OriginalTargetFrameworks,
                TargetFrameworks = GetTargetFrameworksAdapter(projectRestoreInfo.TargetFrameworks),
                ToolReferences = GetToolReferencesAdapter(projectRestoreInfo.ToolReferences)
            };
        }

        public static ProjectRestoreInfo3Adapter Create(IVsProjectRestoreInfo2 projectRestoreInfo)
        {
            if (projectRestoreInfo is null) { throw new ArgumentNullException(nameof(projectRestoreInfo)); }

            return new ProjectRestoreInfo3Adapter
            {
                BaseIntermediatePath = projectRestoreInfo.BaseIntermediatePath,
                OriginalTargetFrameworks = projectRestoreInfo.OriginalTargetFrameworks,
                TargetFrameworks = GetTargetFrameworksAdapter(projectRestoreInfo.TargetFrameworks),
                ToolReferences = GetToolReferencesAdapter(projectRestoreInfo.ToolReferences)
            };
        }

        private static IReadOnlyList<IVsReferenceItem> GetToolReferencesAdapter(IVsReferenceItems toolReferences)
        {
            var result = new List<IVsReferenceItem>(toolReferences.Count);

            for (var i = 0; i < toolReferences.Count; i++)
            {
                result.Add(toolReferences.Item(i));
            }

            return result;
        }

        private static IReadOnlyList<IVsTargetFrameworkInfo4> GetTargetFrameworksAdapter(IVsTargetFrameworks targetFrameworks)
        {
            var result = new List<IVsTargetFrameworkInfo4>(targetFrameworks.Count);

            for (var i = 0; i < targetFrameworks.Count; i++)
            {
                IVsTargetFrameworkInfo targetFramework = targetFrameworks.Item(i);
                IVsTargetFrameworkInfo4 targetFrameworkAdapter = new TargetFramework4Adapter(targetFramework);
                result.Add(targetFrameworkAdapter);
            }

            return result;
        }

        private static IReadOnlyList<IVsTargetFrameworkInfo4> GetTargetFrameworksAdapter(IVsTargetFrameworks2 targetFrameworks)
        {
            var result = new List<IVsTargetFrameworkInfo4>(targetFrameworks.Count);

            for (var i = 0; i < targetFrameworks.Count; i++)
            {
                IVsTargetFrameworkInfo2 targetFramework = targetFrameworks.Item(i);
                IVsTargetFrameworkInfo4 targetFrameworkAdapter = new TargetFramework4Adapter(targetFramework);
                result.Add(targetFrameworkAdapter);
            }

            return result;
        }

        private class TargetFramework4Adapter : IVsTargetFrameworkInfo4
        {
            public TargetFramework4Adapter(IVsTargetFrameworkInfo targetFrameworkInfo)
            {
                TargetFrameworkMoniker = targetFrameworkInfo.TargetFrameworkMoniker;
                Items = ToItemsDictionary(targetFrameworkInfo);
                Properties = ToPropertiesDictionary(targetFrameworkInfo.Properties);
            }

            private IReadOnlyDictionary<string, IReadOnlyList<IVsReferenceItem>> ToItemsDictionary(IVsTargetFrameworkInfo targetFrameworkInfo)
            {
                var result = new Dictionary<string, IReadOnlyList<IVsReferenceItem>>(StringComparer.OrdinalIgnoreCase);

                if (targetFrameworkInfo.ProjectReferences?.Count > 0)
                {
                    IReadOnlyList<IVsReferenceItem> projectReferences = ToReadOnlyList(targetFrameworkInfo.ProjectReferences);
                    result[ProjectItems.ProjectReference] = projectReferences;
                }

                if (targetFrameworkInfo.PackageReferences?.Count > 0)
                {
                    IReadOnlyList<IVsReferenceItem> packageReferences = ToReadOnlyList(targetFrameworkInfo.PackageReferences);
                    result[ProjectItems.PackageReference] = packageReferences;
                }

                if (targetFrameworkInfo is IVsTargetFrameworkInfo2 targetFrameworkInfo2)
                {
                    if (targetFrameworkInfo2.PackageDownloads?.Count > 0)
                    {
                        var packageDownloads = ToReadOnlyList(targetFrameworkInfo2.PackageDownloads);
                        result["PackageDownload"] = packageDownloads;
                    }

                    if (targetFrameworkInfo2.FrameworkReferences?.Count > 0)
                    {
                        var frameworkReferences = ToReadOnlyList(targetFrameworkInfo2.FrameworkReferences);
                        result["FrameworkReference"] = frameworkReferences;
                    }
                }

                if (targetFrameworkInfo is IVsTargetFrameworkInfo3 targetFrameworkInfo3
                    && targetFrameworkInfo3.CentralPackageVersions?.Count > 0)
                {
                    var centralPackageVersions = ToReadOnlyList(targetFrameworkInfo3.CentralPackageVersions);
                    result["PackageVersion"] = centralPackageVersions;
                }

                return result;

                static IReadOnlyList<IVsReferenceItem> ToReadOnlyList(IVsReferenceItems referenceItems)
                {
                    var list = new List<IVsReferenceItem>(referenceItems.Count);
                    for (var i = 0; i < referenceItems.Count; i++)
                    {
                        IVsReferenceItem projectReference = referenceItems.Item(i);
                        list.Add(projectReference);
                    }

                    return list;
                }
            }

            private IReadOnlyDictionary<string, string> ToPropertiesDictionary(IVsProjectProperties properties)
            {
                var result = new Dictionary<string, string>(properties.Count, StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < properties.Count; i++)
                {
                    var property = properties.Item(i);
                    result.Add(property.Name, property.Value);
                }

                return result;
            }

            public string TargetFrameworkMoniker { get; }

            public IReadOnlyDictionary<string, IReadOnlyList<IVsReferenceItem>> Items { get; }

            public IReadOnlyDictionary<string, string> Properties { get; }
        }
    }
}
