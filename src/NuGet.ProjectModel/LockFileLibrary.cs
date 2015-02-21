using System;
using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileLibrary
    {
        public string Name { get; set; }

        public NuGetVersion Version { get; set; }

        public IList<PackageDependencyGroup> DependencyGroups { get; set; } = new List<PackageDependencyGroup>();

        public IList<FrameworkSpecificGroup> FrameworkReferenceGroups { get; set; } = new List<FrameworkSpecificGroup>();

        public IList<FrameworkSpecificGroup> ReferenceGroups { get; set; } = new List<FrameworkSpecificGroup>();

        public IList<string> Files { get; set; } = new List<string>();
    }
}