// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGet.ProjectModel
{
    public class ProjectRestoreMetadataFrameworkInfo
    {
        /// <summary>
        /// Target framework
        /// </summary>
        public NuGetFramework FrameworkName { get; set; }

        /// <summary>
        /// The original string before parsing the framework name. In some cases, it is important to keep this around
        /// because MSBuild framework conditions require the framework name to be the original string (non-normalized).
        /// </summary>
        public string OriginalFrameworkName { get; set; }

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
    }
}