// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGet.Commands
{
    public class MSBuildPackTargetArgs
    {
        public string[] TargetPathsToSymbols { get; set; }
        public string[] TargetPathsToAssemblies { get; set; }
        public string AssemblyName { get; set; }
        public string NuspecOutputPath { get; set; }
        public IEnumerable<ProjectToProjectReference>  ProjectReferences { get; set; }
        public Dictionary<string, HashSet<ContentMetadata>> ContentFiles { get; set; }
        public ISet<NuGetFramework> TargetFrameworks { get; set; }
        public IDictionary<string, string> SourceFiles { get; set; }
        public bool IncludeBuildOutput { get; set; }
        public string BuildOutputFolder { get; set; }


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
