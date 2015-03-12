using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileLibrary
    {
        public string Name { get; set; }

        public NuGetVersion Version { get; set; }

        public string Sha { get; set; }

        public IList<LockFileFrameworkGroup> FrameworkGroups { get; set; } = new List<LockFileFrameworkGroup>();

        public IList<string> Files { get; set; } = new List<string>();
    }

    public class LockFileFrameworkGroup
    {
        public NuGetFramework TargetFramework { get; set; }

        public IList<PackageDependency> Dependencies { get; set; } = new List<PackageDependency>();

        public IList<string> FrameworkAssemblies { get; set; } = new List<string>();

        public IList<string> RuntimeAssemblies { get; set; } = new List<string>();

        public IList<string> CompileTimeAssemblies { get; set; } = new List<string>();
    }
}