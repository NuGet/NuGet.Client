// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public class ProjectRestoreMetadata : IEquatable<ProjectRestoreMetadata>
    {
        /// <summary>
        /// Restore behavior type.
        /// </summary>
        public ProjectStyle ProjectStyle { get; set; } = ProjectStyle.Unknown;

        /// <summary>
        /// MSBuild project file path.
        /// </summary>
        public string ProjectPath { get; set; }

        /// <summary>
        /// Full path to the project.json file if it exists.
        /// </summary>
        public string ProjectJsonPath { get; set; }

        /// <summary>
        /// Assets file output path.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Friendly project name.
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// Name unique to the project across the solution.
        /// </summary>
        public string ProjectUniqueName { get; set; }

        /// <summary>
        /// Package feed sources.
        /// </summary>
        public IList<PackageSource> Sources { get; set; } = new List<PackageSource>();

        /// <summary>
        /// User packages folder path.
        /// </summary>
        public string PackagesPath { get; set; }

        /// <summary>
        /// Cache file path
        /// </summary>
        public string CacheFilePath { get; set; }

        /// <summary>
        /// Fallback folders.
        /// </summary>
        public IList<string> FallbackFolders { get; set; } = new List<string>();

        /// <summary>
        /// ConfigFilePaths used.
        /// </summary>
        public IList<string> ConfigFilePaths { get; set; } = new List<string>();

        /// <summary>
        /// Framework specific metadata, this may be a subset of the project's frameworks.
        /// Operations to determine the nearest framework should be done against the project's frameworks, 
        /// and then matched directly to this section.
        /// </summary>
        public IList<ProjectRestoreMetadataFrameworkInfo> TargetFrameworks { get; set; } = new List<ProjectRestoreMetadataFrameworkInfo>();

        /// <summary>
        /// Original target frameworks strings. These are used to match msbuild conditionals to $(TargetFramework)
        /// </summary>
        public IList<string> OriginalTargetFrameworks { get; set; } = new List<string>();

        /// <summary>
        /// True if $(TargetFrameworks) is used and the build is using Cross Targeting.
        /// </summary>
        public bool CrossTargeting { get; set; }

        /// <summary>
        /// Whether or not to restore the packages directory using the legacy format, which write original case paths
        /// instead of lowercase.
        /// </summary>
        public bool LegacyPackagesDirectory { get; set; }

        /// <summary>
        /// Asset files. These should be equivalent to the files that would be
        /// in the nupkg after packing the project.
        /// </summary>
        public IList<ProjectRestoreMetadataFile> Files { get; set; } = new List<ProjectRestoreMetadataFile>();

        /// <summary>
        /// Compatibility check for runtime framework assets.
        /// </summary>
        public bool ValidateRuntimeAssets { get; set; }

        /// <summary>
        /// True if this is a Legacy Package Reference project
        /// </summary>
        public bool SkipContentFileWrite { get; set; }

        /// <summary>
        /// Contains Project wide properties for Warnings.
        /// </summary>
        public WarningProperties ProjectWideWarningProperties { get; set; } = new WarningProperties();

        public RestoreLockProperties RestoreLockProperties { get; set; } = new RestoreLockProperties();

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddObject(ProjectStyle);
            hashCode.AddObject(ProjectPath);
            hashCode.AddObject(ProjectJsonPath);
            hashCode.AddObject(OutputPath);
            hashCode.AddObject(ProjectName);
            hashCode.AddObject(ProjectUniqueName);
            hashCode.AddSequence(Sources);
            hashCode.AddObject(PackagesPath);
            hashCode.AddSequence(ConfigFilePaths);
            hashCode.AddSequence(FallbackFolders);
            hashCode.AddSequence(TargetFrameworks);
            hashCode.AddSequence(OriginalTargetFrameworks);
            hashCode.AddObject(CrossTargeting);
            hashCode.AddObject(LegacyPackagesDirectory);
            hashCode.AddObject(Files);
            hashCode.AddObject(ValidateRuntimeAssets);
            hashCode.AddObject(SkipContentFileWrite);
            hashCode.AddObject(ProjectWideWarningProperties);
            hashCode.AddObject(RestoreLockProperties);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ProjectRestoreMetadata);
        }

        public bool Equals(ProjectRestoreMetadata other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return ProjectStyle == other.ProjectStyle &&
                   PathUtility.GetStringComparerBasedOnOS().Equals(ProjectPath, other.ProjectPath) &&
                   PathUtility.GetStringComparerBasedOnOS().Equals(ProjectJsonPath, other.ProjectJsonPath) &&
                   PathUtility.GetStringComparerBasedOnOS().Equals(OutputPath, other.OutputPath) &&
                   PathUtility.GetStringComparerBasedOnOS().Equals(ProjectName, other.ProjectName) &&
                   PathUtility.GetStringComparerBasedOnOS().Equals(ProjectUniqueName, other.ProjectUniqueName) &&
                   Sources.OrderedEquals(other.Sources.Distinct(), source => source.Source, StringComparer.OrdinalIgnoreCase) &&
                   PathUtility.GetStringComparerBasedOnOS().Equals(PackagesPath, other.PackagesPath) &&
                   ConfigFilePaths.OrderedEquals(other.ConfigFilePaths, filePath => filePath, PathUtility.GetStringComparerBasedOnOS(), PathUtility.GetStringComparerBasedOnOS()) &&
                   FallbackFolders.OrderedEquals(other.FallbackFolders, fallbackFolder => fallbackFolder, PathUtility.GetStringComparerBasedOnOS(), PathUtility.GetStringComparerBasedOnOS()) &&
                   EqualityUtility.SequenceEqualWithNullCheck(TargetFrameworks, other.TargetFrameworks) &&
                   OriginalTargetFrameworks.OrderedEquals(other.OriginalTargetFrameworks, fw => fw, StringComparer.OrdinalIgnoreCase, StringComparer.OrdinalIgnoreCase) &&
                   CrossTargeting == other.CrossTargeting &&
                   LegacyPackagesDirectory == other.LegacyPackagesDirectory &&
                   ValidateRuntimeAssets == other.ValidateRuntimeAssets &&
                   SkipContentFileWrite == other.SkipContentFileWrite &&
                   EqualityUtility.SequenceEqualWithNullCheck(Files, other.Files) &&
                   EqualityUtility.EqualsWithNullCheck(ProjectWideWarningProperties, other.ProjectWideWarningProperties) &&
                   EqualityUtility.EqualsWithNullCheck(RestoreLockProperties, other.RestoreLockProperties);
        }

        public ProjectRestoreMetadata Clone()
        {
            return new ProjectRestoreMetadata
            {
                ProjectStyle = ProjectStyle,
                ProjectPath = ProjectPath,
                ProjectJsonPath = ProjectJsonPath,
                OutputPath = OutputPath,
                ProjectName = ProjectName,
                ProjectUniqueName = ProjectUniqueName,
                PackagesPath = PackagesPath,
                CacheFilePath = CacheFilePath,
                CrossTargeting = CrossTargeting,
                LegacyPackagesDirectory = LegacyPackagesDirectory,
                SkipContentFileWrite = SkipContentFileWrite,
                ValidateRuntimeAssets = ValidateRuntimeAssets,
                FallbackFolders = FallbackFolders != null ? new List<string>(FallbackFolders) : null,
                ConfigFilePaths = ConfigFilePaths != null ? new List<string>(ConfigFilePaths) : null,
                OriginalTargetFrameworks = OriginalTargetFrameworks != null ? new List<string>(OriginalTargetFrameworks) : null,
                Sources = Sources?.Select(c => c.Clone()).ToList(),
                TargetFrameworks = TargetFrameworks?.Select(c => c.Clone()).ToList(),
                Files = Files?.Select(c => c.Clone()).ToList(),
                ProjectWideWarningProperties = ProjectWideWarningProperties?.Clone(),
                RestoreLockProperties = RestoreLockProperties?.Clone()
            };
        }
    }
}
