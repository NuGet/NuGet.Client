// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace NuGet.ProjectModel
{
    public class TargetFrameworkInformation
    {
        public NuGetFramework FrameworkName { get; set; }

        public IList<LibraryDependency> Dependencies { get; set; } = new List<LibraryDependency>();

        /// <summary>
        /// A fallback PCL framework to use when no compatible items
        /// were found for <see cref="FrameworkName"/>.
        /// </summary>
        public IList<NuGetFramework> Imports { get; set; } = new List<NuGetFramework>();

        /// <summary>
        /// Display warnings when the Imports framework is used.
        /// </summary>
        public bool Warn { get; set; }

        public override string ToString()
        {
            return FrameworkName.GetShortFolderName();
        }
    }
}
