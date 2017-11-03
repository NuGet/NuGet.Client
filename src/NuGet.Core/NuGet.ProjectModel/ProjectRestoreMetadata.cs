// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
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
                   ProjectPath == other.ProjectPath &&
                   ProjectJsonPath == other.ProjectJsonPath &&
                   OutputPath == other.OutputPath &&
                   ProjectName == other.ProjectName &&
                   ProjectUniqueName == other.ProjectUniqueName &&
                   EqualityUtility.SequenceEqualWithNullCheck(Sources, other.Sources) &&
                   PackagesPath == other.PackagesPath &&
                   EqualityUtility.SequenceEqualWithNullCheck(ConfigFilePaths, other.ConfigFilePaths) &&
                   EqualityUtility.SequenceEqualWithNullCheck(FallbackFolders, other.FallbackFolders) &&
                   EqualityUtility.SequenceEqualWithNullCheck(TargetFrameworks, other.TargetFrameworks) &&
                   EqualityUtility.SequenceEqualWithNullCheck(OriginalTargetFrameworks, other.OriginalTargetFrameworks) &&
                   CrossTargeting == other.CrossTargeting &&
                   LegacyPackagesDirectory == other.LegacyPackagesDirectory &&
                   ValidateRuntimeAssets == other.ValidateRuntimeAssets &&
                   SkipContentFileWrite == other.SkipContentFileWrite &&
                   EqualityUtility.SequenceEqualWithNullCheck(Files, other.Files) &&
                   EqualityUtility.EqualsWithNullCheck(ProjectWideWarningProperties, other.ProjectWideWarningProperties);
        }

        public ProjectRestoreMetadata Clone()
        {
            var clonedObject = new ProjectRestoreMetadata();
            clonedObject.ProjectStyle = ProjectStyle;
            clonedObject.ProjectPath = ProjectPath;
            clonedObject.ProjectJsonPath = ProjectJsonPath;
            clonedObject.OutputPath = OutputPath;
            clonedObject.ProjectName = ProjectName;
            clonedObject.ProjectUniqueName = ProjectUniqueName;
            clonedObject.PackagesPath = PackagesPath;
            clonedObject.CacheFilePath = CacheFilePath;
            clonedObject.CrossTargeting = CrossTargeting;
            clonedObject.LegacyPackagesDirectory = LegacyPackagesDirectory;
            clonedObject.SkipContentFileWrite = SkipContentFileWrite;
            clonedObject.ValidateRuntimeAssets = ValidateRuntimeAssets;
            clonedObject.FallbackFolders = new List<string>(FallbackFolders);
            clonedObject.ConfigFilePaths = new List<string>(ConfigFilePaths);
            clonedObject.OriginalTargetFrameworks = new List<string>(ConfigFilePaths);
            clonedObject.Sources = Sources?.Select(c => c.Clone()).ToList();
            clonedObject.TargetFrameworks = TargetFrameworks?.Select( c => c.Clone()).ToList();
            clonedObject.Files = Files.Select(c => c.Clone()).ToList();
            clonedObject.ProjectWideWarningProperties = ProjectWideWarningProperties.Clone();
            return clonedObject;
    }
}
}
