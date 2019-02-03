// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Shared;
using NuGet.Versioning;
using NuGet.VisualStudio;
using static NuGet.Frameworks.FrameworkConstants;

namespace NuGet.SolutionRestoreManager
{
    internal class VSNominationUtilities
    {
        #region IVsProjectRestoreInfoAPIs

        internal static void ProcessToolReferences(ProjectNames projectNames, IVsProjectRestoreInfo projectRestoreInfo, DependencyGraphSpec dgSpec)
        {
            var toolFramework = GetToolFramework(projectRestoreInfo.TargetFrameworks);
            var packagesPath = GetRestoreProjectPath(projectRestoreInfo.TargetFrameworks);
            var fallbackFolders = GetRestoreFallbackFolders(projectRestoreInfo.TargetFrameworks).AsList();
            var sources = GetRestoreSources(projectRestoreInfo.TargetFrameworks)
                .Select(e => new PackageSource(e))
                .ToList();

            projectRestoreInfo
                .ToolReferences
                .Cast<IVsReferenceItem>()
                .Select(r => ToolRestoreUtility.GetSpec(
                    projectNames.FullName,
                    r.Name,
                    GetVersionRange(r),
                    toolFramework,
                    packagesPath,
                    fallbackFolders,
                    sources,
                    projectWideWarningProperties: null))
                .ForEach(ts =>
                {
                    dgSpec.AddRestore(ts.RestoreMetadata.ProjectUniqueName);
                    dgSpec.AddProject(ts);
                });
        }

        internal static RuntimeGraph GetRuntimeGraph(IVsProjectRestoreInfo projectRestoreInfo)
        {
            var runtimes = projectRestoreInfo
                .TargetFrameworks
                .Cast<IVsTargetFrameworkInfo>()
                .SelectMany(tfi => new[]
                {
                    GetPropertyValueOrNull(tfi.Properties, ProjectBuildProperties.RuntimeIdentifier),
                    GetPropertyValueOrNull(tfi.Properties, ProjectBuildProperties.RuntimeIdentifiers),
                })
                .SelectMany(MSBuildStringUtility.Split)
                .Distinct(StringComparer.Ordinal)
                .Select(rid => new RuntimeDescription(rid))
                .ToList();

            var supports = projectRestoreInfo
                .TargetFrameworks
                .Cast<IVsTargetFrameworkInfo>()
                .Select(tfi => GetPropertyValueOrNull(tfi.Properties, ProjectBuildProperties.RuntimeSupports))
                .SelectMany(MSBuildStringUtility.Split)
                .Distinct(StringComparer.Ordinal)
                .Select(s => new CompatibilityProfile(s))
                .ToList();

            return new RuntimeGraph(runtimes, supports);
        }
        #endregion IVsProjectRestoreInfoAPIs

        #region IVSTargetFrameworkInfoAPIs

        internal static TargetFrameworkInformation ToTargetFrameworkInformation(
            IVsTargetFrameworkInfo targetFrameworkInfo)
        {
            var tfi = new TargetFrameworkInformation
            {
                FrameworkName = NuGetFramework.Parse(targetFrameworkInfo.TargetFrameworkMoniker)
            };

            var ptf = MSBuildStringUtility.Split(GetPropertyValueOrNull(targetFrameworkInfo.Properties, ProjectBuildProperties.PackageTargetFallback))
                                          .Select(NuGetFramework.Parse)
                                          .ToList();

            var atf = MSBuildStringUtility.Split(GetPropertyValueOrNull(targetFrameworkInfo.Properties, ProjectBuildProperties.AssetTargetFallback))
                                          .Select(NuGetFramework.Parse)
                                          .ToList();

            // Update TFI with fallback properties
            AssetTargetFallbackUtility.ApplyFramework(tfi, ptf, atf);

            if (targetFrameworkInfo.PackageReferences != null)
            {
                tfi.Dependencies.AddRange(
                    targetFrameworkInfo.PackageReferences
                        .Cast<IVsReferenceItem>()
                        .Select(ToPackageLibraryDependency));
            }

            return tfi;
        }

        internal static ProjectRestoreMetadataFrameworkInfo ToProjectRestoreMetadataFrameworkInfo(
            IVsTargetFrameworkInfo targetFrameworkInfo,
            string projectDirectory)
        {
            var tfi = new ProjectRestoreMetadataFrameworkInfo
            {
                FrameworkName = NuGetFramework.Parse(targetFrameworkInfo.TargetFrameworkMoniker)
            };

            if (targetFrameworkInfo.ProjectReferences != null)
            {
                tfi.ProjectReferences.AddRange(
                    targetFrameworkInfo.ProjectReferences
                        .Cast<IVsReferenceItem>()
                        .Where(IsReferenceOutputAssemblyTrueOrEmpty)
                        .Select(item => ToProjectRestoreReference(item, projectDirectory)));
            }

            return tfi;
        }

        #endregion IVSTargetFrameworkInfoAPIs

        #region IVSTargetFrameworksAPIs

        internal static string GetPackageId(ProjectNames projectNames, IVsTargetFrameworks tfms)
        {
            var packageId = GetSingleNonEvaluatedPropertyOrNull(tfms, ProjectBuildProperties.PackageId, v => v);
            return packageId ?? projectNames.ShortName;
        }

        internal static NuGetVersion GetPackageVersion(IVsTargetFrameworks tfms)
        {
            // $(PackageVersion) property if set overrides the $(Version)
            var versionPropertyValue =
                GetSingleNonEvaluatedPropertyOrNull(tfms, ProjectBuildProperties.PackageVersion, NuGetVersion.Parse)
                ?? GetSingleNonEvaluatedPropertyOrNull(tfms, ProjectBuildProperties.Version, NuGetVersion.Parse);

            return versionPropertyValue ?? PackageSpec.DefaultVersion;
        }

        internal static string GetRestoreProjectPath(IVsTargetFrameworks tfms)
        {
            return GetSingleNonEvaluatedPropertyOrNull(tfms, ProjectBuildProperties.RestorePackagesPath, e => e);
        }

        internal static RestoreLockProperties GetRestoreLockProperties(IVsTargetFrameworks tfms)
        {
            return new RestoreLockProperties(
                        GetRestorePackagesWithLockFile(tfms),
                        GetNuGetLockFilePath(tfms),
                        IsLockFileFreezeOnRestore(tfms));
        }

        internal static WarningProperties GetProjectWideWarningProperties(IVsTargetFrameworks targetFrameworks)
        {
            return WarningProperties.GetWarningProperties(
                        treatWarningsAsErrors: GetSingleOrDefaultPropertyValue(targetFrameworks, ProjectBuildProperties.TreatWarningsAsErrors, e => e),
                        warningsAsErrors: GetSingleOrDefaultNuGetLogCodes(targetFrameworks, ProjectBuildProperties.WarningsAsErrors, e => MSBuildStringUtility.GetNuGetLogCodes(e)),
                        noWarn: GetSingleOrDefaultNuGetLogCodes(targetFrameworks, ProjectBuildProperties.NoWarn, e => MSBuildStringUtility.GetNuGetLogCodes(e)));
        }

        /// <summary>
        /// The result will contain CLEAR and no sources specified in RestoreSources if the clear keyword is in it.
        /// If there are additional sources specified, the value AdditionalValue will be set in the result and then all the additional sources will follow
        /// </summary>
        internal static IEnumerable<string> GetRestoreSources(IVsTargetFrameworks tfms)
        {
            var sources = HandleClear(MSBuildStringUtility.Split(GetSingleNonEvaluatedPropertyOrNull(tfms, ProjectBuildProperties.RestoreSources, e => e)));

            // Read RestoreAdditionalProjectSources from the inner build, these may be different between frameworks.
            // Exclude is not allowed for sources
            var additional = MSBuildRestoreUtility.AggregateSources(
                values: GetAggregatePropertyValues(tfms, ProjectBuildProperties.RestoreAdditionalProjectSources),
                excludeValues: Enumerable.Empty<string>());

            return VSRestoreSettingsUtilities.GetEntriesWithAdditional(sources, additional.ToArray());
        }

        /// <summary>
        /// The result will contain CLEAR and no sources specified in RestoreFallbackFolders if the clear keyword is in it.
        /// If there are additional fallback folders specified, the value AdditionalValue will be set in the result and then all the additional fallback folders will follow
        /// </summary>
        internal static IEnumerable<string> GetRestoreFallbackFolders(IVsTargetFrameworks tfms)
        {
            var folders = HandleClear(MSBuildStringUtility.Split(GetSingleNonEvaluatedPropertyOrNull(tfms, ProjectBuildProperties.RestoreFallbackFolders, e => e)));

            // Read RestoreAdditionalProjectFallbackFolders from the inner build.
            // Remove all excluded fallback folders listed in RestoreAdditionalProjectFallbackFoldersExcludes.
            var additional = MSBuildRestoreUtility.AggregateSources(
                values: GetAggregatePropertyValues(tfms, ProjectBuildProperties.RestoreAdditionalProjectFallbackFolders),
                excludeValues: GetAggregatePropertyValues(tfms, ProjectBuildProperties.RestoreAdditionalProjectFallbackFoldersExcludes));

            return VSRestoreSettingsUtilities.GetEntriesWithAdditional(folders, additional.ToArray());
        }

        private static string GetRestorePackagesWithLockFile(IVsTargetFrameworks tfms)
        {
            return GetSingleNonEvaluatedPropertyOrNull(tfms, ProjectBuildProperties.RestorePackagesWithLockFile, v => v);
        }

        private static string GetNuGetLockFilePath(IVsTargetFrameworks tfms)
        {
            return GetSingleNonEvaluatedPropertyOrNull(tfms, ProjectBuildProperties.NuGetLockFilePath, v => v);
        }

        private static bool IsLockFileFreezeOnRestore(IVsTargetFrameworks tfms)
        {
            return GetSingleNonEvaluatedPropertyOrNull(tfms, ProjectBuildProperties.RestoreLockedMode, MSBuildStringUtility.IsTrue);
        }

        private static NuGetFramework GetToolFramework(IVsTargetFrameworks targetFrameworks)
        {
            return GetSingleNonEvaluatedPropertyOrNull(
                    targetFrameworks,
                    ProjectBuildProperties.DotnetCliToolTargetFramework,
                    NuGetFramework.Parse) ?? CommonFrameworks.NetCoreApp10;
        }

        private static TValue GetSingleOrDefaultPropertyValue<TValue>(
            IVsTargetFrameworks tfms,
            string propertyName,
            Func<string, TValue> valueFactory)
        {
            var properties = GetNonEvaluatedPropertyOrNull(tfms, propertyName, valueFactory);

            return properties.Count() > 1 ? default(TValue) : properties.SingleOrDefault();
        }

        private static IEnumerable<NuGetLogCode> GetSingleOrDefaultNuGetLogCodes(
            IVsTargetFrameworks tfms,
            string propertyName,
            Func<string, IEnumerable<NuGetLogCode>> valueFactory)
        {
            var logCodeProperties = GetNonEvaluatedPropertyOrNull(tfms, propertyName, valueFactory);

            return MSBuildStringUtility.GetDistinctNuGetLogCodesOrDefault(logCodeProperties);
        }

        /// <summary>
        /// Trying to fetch a list of property value from all tfm property bags.
        /// </summary>
        private static IEnumerable<TValue> GetNonEvaluatedPropertyOrNull<TValue>(
            IVsTargetFrameworks tfms,
            string propertyName,
            Func<string, TValue> valueFactory)
        {
            return tfms
                .Cast<IVsTargetFrameworkInfo>()
                .Select(tfm =>
                {
                    var val = GetPropertyValueOrNull(tfm.Properties, propertyName);
                    return val != null ? valueFactory(val) : default(TValue);
                })
                .Distinct();
        }

        /// <summary>
        ///Trying to fetch a property value from tfm property bags.
        /// If defined the property should have identical values in all of the occurances.
        /// </summary>
        private static TValue GetSingleNonEvaluatedPropertyOrNull<TValue>(
            IVsTargetFrameworks tfms,
            string propertyName,
            Func<string, TValue> valueFactory)
        {
            return GetNonEvaluatedPropertyOrNull(tfms, propertyName, valueFactory).SingleOrDefault();
        }

        /// <summary>
        /// Fetch all property values from each target framework and combine them.
        /// </summary>
        private static IEnumerable<string> GetAggregatePropertyValues(
                IVsTargetFrameworks tfms,
                string propertyName)
        {
            // Only non-null values are added to the list as part of the split.
            return tfms
                .Cast<IVsTargetFrameworkInfo>()
                .SelectMany(tfm => MSBuildStringUtility.Split(GetPropertyValueOrNull(tfm.Properties, propertyName)));
        }
        #endregion IVSTargetFrameworksAPIs

        #region IVSReferenceItemAPIs

        private static LibraryDependency ToPackageLibraryDependency(IVsReferenceItem item)
        {
            var dependency = new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                    name: item.Name,
                    versionRange: GetVersionRange(item),
                    typeConstraint: LibraryDependencyTarget.Package),

                // Mark packages coming from the SDK as AutoReferenced
                AutoReferenced = GetPropertyBoolOrFalse(item, "IsImplicitlyDefined"),

                GeneratePathProperty = GetPropertyBoolOrFalse(item, "GeneratePathProperty")
            };

            // Add warning suppressions
            foreach (var code in MSBuildStringUtility.GetNuGetLogCodes(GetPropertyValueOrNull(item, ProjectBuildProperties.NoWarn)))
            {
                dependency.NoWarn.Add(code);
            }

            MSBuildRestoreUtility.ApplyIncludeFlags(
                dependency,
                includeAssets: GetPropertyValueOrNull(item, ProjectBuildProperties.IncludeAssets),
                excludeAssets: GetPropertyValueOrNull(item, ProjectBuildProperties.ExcludeAssets),
                privateAssets: GetPropertyValueOrNull(item, ProjectBuildProperties.PrivateAssets));

            return dependency;
        }

        private static ProjectRestoreReference ToProjectRestoreReference(IVsReferenceItem item, string projectDirectory)
        {
            // The path may be a relative path, to match the project unique name as a
            // string this should be the full path to the project
            // Remove ../../ and any other relative parts of the path that were used in the project file
            var referencePath = Path.GetFullPath(Path.Combine(projectDirectory, item.Name));

            var dependency = new ProjectRestoreReference
            {
                ProjectPath = referencePath,
                ProjectUniqueName = referencePath,
            };

            MSBuildRestoreUtility.ApplyIncludeFlags(
                dependency,
                includeAssets: GetPropertyValueOrNull(item, ProjectBuildProperties.IncludeAssets),
                excludeAssets: GetPropertyValueOrNull(item, ProjectBuildProperties.ExcludeAssets),
                privateAssets: GetPropertyValueOrNull(item, ProjectBuildProperties.PrivateAssets));

            return dependency;
        }

        private static VersionRange GetVersionRange(IVsReferenceItem item)
        {
            var versionRange = GetPropertyValueOrNull(item, "Version");

            if (versionRange != null)
            {
                return VersionRange.Parse(versionRange);
            }

            return VersionRange.All;
        }

        /// <summary>
        /// True if ReferenceOutputAssembly is true or empty.
        /// All other values will be false.
        /// </summary>
        private static bool IsReferenceOutputAssemblyTrueOrEmpty(IVsReferenceItem item)
        {
            var value = GetPropertyValueOrNull(item, ProjectBuildProperties.ReferenceOutputAssembly);

            return MSBuildStringUtility.IsTrueOrEmpty(value);
        }

        private static bool GetPropertyBoolOrFalse(
        IVsReferenceItem item, string propertyName)
        {
            try
            {
                return MSBuildStringUtility.IsTrue(item.Properties?.Item(propertyName)?.Value);
            }
            catch (ArgumentException)
            {
            }
            catch (KeyNotFoundException)
            {
            }

            return false;
        }

        private static string GetPropertyValueOrNull(
            IVsReferenceItem item, string propertyName)
        {
            try
            {
                return MSBuildStringUtility.TrimAndGetNullForEmpty(item.Properties?.Item(propertyName)?.Value);
            }
            catch (ArgumentException)
            {
            }
            catch (KeyNotFoundException)
            {
            }

            return null;
        }

        private static string GetPropertyValueOrNull(
            IVsProjectProperties properties, string propertyName)
        {
            try
            {
                return MSBuildStringUtility.TrimAndGetNullForEmpty(properties?.Item(propertyName)?.Value);
            }
            catch (ArgumentException)
            {
            }
            catch (KeyNotFoundException)
            {
            }

            return null;
        }
        #endregion IVSReferenceItemAPIs

        #region Common
        private static string[] HandleClear(string[] input)
        {
            if (input.Any(e => StringComparer.OrdinalIgnoreCase.Equals(ProjectBuildProperties.Clear, e)))
            {
                return new string[] { ProjectBuildProperties.Clear };
            }

            return input;
        }
        #endregion Common
    }
}
