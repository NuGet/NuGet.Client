using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;

namespace NuGet.Commands
{
    public class MSBuildPackTargetArgs
    {
        public string[] TargetPaths { get; set; }
        public string AssemblyName { get; set; }
        public string Configuration { get; set; }
        public string NuspecOutputPath { get; set; }
        public IEnumerable<ProjectToProjectReference>  ProjectReferences { get; set; }
        public Dictionary<string, HashSet<string>> ContentFiles { get; set; }
        public ISet<NuGetFramework> TargetFrameworks { get; set; }
        public IDictionary<string, string> SourceFiles { get; set; }


        public MSBuildPackTargetArgs()
        {
            ProjectReferences = new List<ProjectToProjectReference>();
            SourceFiles = new Dictionary<string, string>();
        }
    }

    public struct ProjectToProjectReference
    {
        public string AssemblyName { get; set; }
        public string TargetPath { get; set; }
    }
}
