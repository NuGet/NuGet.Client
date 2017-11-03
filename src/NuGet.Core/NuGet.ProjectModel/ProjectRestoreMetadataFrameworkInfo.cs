// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public class ProjectRestoreMetadataFrameworkInfo : IEquatable<ProjectRestoreMetadataFrameworkInfo>
    {
        /// <summary>
        /// Target framework
        /// </summary>
        public NuGetFramework FrameworkName { get; set; }

        /// <summary>
        /// The original string before parsing the framework name. In some cases, it is important to keep this around
        /// because MSBuild framework conditions require the framework name to be the original string (non-normalized).
        /// </summary>
        public string OriginalFrameworkName { get; set; } // TODO NK - Remove this? Or maybe clean everything up so it shows up correctly!

        /// <summary>
        /// Project references
        /// </summary>
        public IList<ProjectRestoreReference> ProjectReferences { get; set; } = new List<ProjectRestoreReference>();

        public ProjectRestoreMetadataFrameworkInfo()
        {
        }

        public ProjectRestoreMetadataFrameworkInfo(NuGetFramework frameworkName)
        {
            FrameworkName = frameworkName;
        }

        public override string ToString()
        {
            return FrameworkName.GetShortFolderName();
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddObject(FrameworkName);
            hashCode.AddObject(OriginalFrameworkName);
            hashCode.AddSequence(ProjectReferences);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ProjectRestoreMetadataFrameworkInfo);
        }

        public bool Equals(ProjectRestoreMetadataFrameworkInfo other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityUtility.EqualsWithNullCheck(FrameworkName, other.FrameworkName) &&
                   OriginalFrameworkName == other.OriginalFrameworkName &&
                   EqualityUtility.SequenceEqualWithNullCheck(ProjectReferences, other.ProjectReferences);
        }

        public ProjectRestoreMetadataFrameworkInfo Clone()
        {
            var clonedObject = new ProjectRestoreMetadataFrameworkInfo();
            clonedObject.FrameworkName = FrameworkName;
            clonedObject.OriginalFrameworkName = OriginalFrameworkName;
            clonedObject.ProjectReferences = ProjectReferences?.Select(c => c.Clone()).ToList();
            return clonedObject;
        }
    }
}