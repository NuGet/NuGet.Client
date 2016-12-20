// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        /// Fallback folders.
        /// </summary>
        public IList<string> FallbackFolders { get; set; } = new List<string>();

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
            hashCode.AddSequence(FallbackFolders);
            hashCode.AddSequence(TargetFrameworks);
            hashCode.AddSequence(OriginalTargetFrameworks);
            hashCode.AddObject(CrossTargeting);
            hashCode.AddObject(LegacyPackagesDirectory);

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
                   EqualityUtility.SequenceEqualWithNullCheck(FallbackFolders, other.FallbackFolders) &&
                   EqualityUtility.SequenceEqualWithNullCheck(TargetFrameworks, other.TargetFrameworks) &&
                   EqualityUtility.SequenceEqualWithNullCheck(OriginalTargetFrameworks, other.OriginalTargetFrameworks) &&
                   CrossTargeting == other.CrossTargeting &&
                   LegacyPackagesDirectory == other.LegacyPackagesDirectory;
        }
    }
}
