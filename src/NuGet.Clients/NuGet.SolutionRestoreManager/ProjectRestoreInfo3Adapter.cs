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

        public string? OriginalTargetFrameworks { get; init; }

        public required IReadOnlyList<IVsTargetFrameworkInfo4> TargetFrameworks { get; init; }

        public IReadOnlyList<IVsReferenceItem2>? ToolReferences { get; init; }

        public static ProjectRestoreInfo3Adapter Create(IVsProjectRestoreInfo projectRestoreInfo)
        {
            if (projectRestoreInfo is null) { throw new ArgumentNullException(nameof(projectRestoreInfo)); }

            return new ProjectRestoreInfo3Adapter
            {
                BaseIntermediatePath = projectRestoreInfo.BaseIntermediatePath,
                OriginalTargetFrameworks = projectRestoreInfo.OriginalTargetFrameworks,
                TargetFrameworks = GetTargetFrameworksAdapter(projectRestoreInfo.TargetFrameworks),
                ToolReferences = projectRestoreInfo.ToolReferences is null ? null : GetToolReferencesAdapter(projectRestoreInfo.ToolReferences)
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

        private static IReadOnlyList<IVsReferenceItem2>? GetToolReferencesAdapter(IVsReferenceItems? toolReferences)
        {
            if (toolReferences is null) { return null; }

            var result = new List<IVsReferenceItem2>(toolReferences.Count);

            for (var i = 0; i < toolReferences.Count; i++)
            {
                var reference = toolReferences.Item(i);
                IVsReferenceItem2? toolReference = reference is not null ? new ReferenceItem2Adapter(reference) : null;
                // null needs to be handled in VsSolutionRestoreService.ToDependencySpec and reported back to caller
                result.Add(toolReference!);
            }

            return result;
        }

        private static IReadOnlyList<IVsTargetFrameworkInfo4> GetTargetFrameworksAdapter(IVsTargetFrameworks targetFrameworks)
        {
            var result = new List<IVsTargetFrameworkInfo4>(targetFrameworks.Count);

            for (var i = 0; i < targetFrameworks.Count; i++)
            {
                IVsTargetFrameworkInfo? targetFramework = targetFrameworks.Item(i);
                IVsTargetFrameworkInfo4? targetFrameworkAdapter = targetFramework is not null ? new TargetFramework4Adapter(targetFramework) : null;
                // null needs to be handled in VsSolutionRestoreService.ToDependencySpec and reported back to caller
                result.Add(targetFrameworkAdapter!);
            }

            return result;
        }

        private static IReadOnlyList<IVsTargetFrameworkInfo4> GetTargetFrameworksAdapter(IVsTargetFrameworks2 targetFrameworks)
        {
            var result = new List<IVsTargetFrameworkInfo4>(targetFrameworks.Count);

            for (var i = 0; i < targetFrameworks.Count; i++)
            {
                IVsTargetFrameworkInfo2? targetFramework = targetFrameworks.Item(i);
                IVsTargetFrameworkInfo4? targetFrameworkAdapter = targetFramework is not null ? new TargetFramework4Adapter(targetFramework) : null;
                // null needs to be handled in VsSolutionRestoreService.ToDependencySpec and reported back to caller
                result.Add(targetFrameworkAdapter!);
            }

            return result;
        }

        private static IReadOnlyDictionary<string, string?> ToPropertiesDictionary(IVsProjectProperties properties)
        {
            var result = new Dictionary<string, string?>(properties.Count, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < properties.Count; i++)
            {
                var property = properties.Item(i) ?? throw new Exception($"Property index {i} returned null");
                result.Add(property.Name, property.Value);
            }

            return result;
        }

        private static IReadOnlyDictionary<string, string?>? ToPropertiesDictionary(IVsReferenceProperties? properties)
        {
            if (properties is null) { return null; }

            var result = new Dictionary<string, string?>(properties.Count, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < properties.Count; i++)
            {
                var property = properties.Item(i) ?? throw new Exception($"Property index {i} returned null");
                result.Add(property.Name, property.Value);
            }

            return result;
        }

        private record TargetFramework4Adapter : IVsTargetFrameworkInfo4
        {
            public TargetFramework4Adapter(IVsTargetFrameworkInfo targetFrameworkInfo)
            {
                if (targetFrameworkInfo is null) { throw new ArgumentNullException(nameof(targetFrameworkInfo)); }

                TargetFrameworkMoniker = targetFrameworkInfo.TargetFrameworkMoniker;
                Items = ToItemsDictionary(targetFrameworkInfo);
                // null needs to be handled in VsSolutionRestoreService.ToDependencySpec and reported back to caller
                Properties = targetFrameworkInfo.Properties is not null ? ToPropertiesDictionary(targetFrameworkInfo.Properties) : null!;
            }

            private IReadOnlyDictionary<string, IReadOnlyList<IVsReferenceItem2>> ToItemsDictionary(IVsTargetFrameworkInfo targetFrameworkInfo)
            {
                var result = new Dictionary<string, IReadOnlyList<IVsReferenceItem2>>(StringComparer.OrdinalIgnoreCase);

                if (targetFrameworkInfo.ProjectReferences?.Count > 0)
                {
                    IReadOnlyList<IVsReferenceItem2> projectReferences = ToReferenceItem2ReadOnlyList(targetFrameworkInfo.ProjectReferences);
                    result[ProjectItems.ProjectReference] = projectReferences;
                }

                if (targetFrameworkInfo.PackageReferences?.Count > 0)
                {
                    IReadOnlyList<IVsReferenceItem2> packageReferences = ToReferenceItem2ReadOnlyList(targetFrameworkInfo.PackageReferences);
                    result[ProjectItems.PackageReference] = packageReferences;
                }

                if (targetFrameworkInfo is IVsTargetFrameworkInfo2 targetFrameworkInfo2)
                {
                    if (targetFrameworkInfo2.PackageDownloads?.Count > 0)
                    {
                        IReadOnlyList<IVsReferenceItem2> packageDownloads = ToReferenceItem2ReadOnlyList(targetFrameworkInfo2.PackageDownloads);
                        result["PackageDownload"] = packageDownloads;
                    }

                    if (targetFrameworkInfo2.FrameworkReferences?.Count > 0)
                    {
                        IReadOnlyList<IVsReferenceItem2> frameworkReferences = ToReferenceItem2ReadOnlyList(targetFrameworkInfo2.FrameworkReferences);
                        result["FrameworkReference"] = frameworkReferences;
                    }
                }

                if (targetFrameworkInfo is IVsTargetFrameworkInfo3 targetFrameworkInfo3
                    && targetFrameworkInfo3.CentralPackageVersions?.Count > 0)
                {
                    IReadOnlyList<IVsReferenceItem2> centralPackageVersions = ToReferenceItem2ReadOnlyList(targetFrameworkInfo3.CentralPackageVersions);
                    result["PackageVersion"] = centralPackageVersions;
                }

                return result;

                static IReadOnlyList<IVsReferenceItem2> ToReferenceItem2ReadOnlyList(IVsReferenceItems referenceItems)
                {
                    var list = new List<IVsReferenceItem2>(referenceItems.Count);
                    for (var i = 0; i < referenceItems.Count; i++)
                    {
                        IVsReferenceItem? reference = referenceItems.Item(i);
                        IVsReferenceItem2? projectReference = reference is not null ? new ReferenceItem2Adapter(reference) : null;
                        // null needs to be handled in VsSolutionRestoreService.ToDependencySpec and reported back to caller
                        list.Add(projectReference!);
                    }

                    return list;
                }
            }

            public string TargetFrameworkMoniker { get; }

            public IReadOnlyDictionary<string, IReadOnlyList<IVsReferenceItem2>>? Items { get; }

            public IReadOnlyDictionary<string, string?> Properties { get; }
        }

        private record ReferenceItem2Adapter : IVsReferenceItem2
        {
            public string Name { get; }

            public IReadOnlyDictionary<string, string?>? Properties { get; }

            public ReferenceItem2Adapter(IVsReferenceItem referenceItem)
            {
                Name = referenceItem.Name;
                Properties = ToPropertiesDictionary(referenceItem.Properties);
            }
        }
    }
}
