// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class Project
    {
        public const string ProjectFileName = "project.json";

        public Project()
        {
            Scripts = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);
            TargetFrameworks = new List<TargetFrameworkInformation>();
        }

        public string ProjectFilePath { get; set; }

        public string ProjectDirectory
        {
            get
            {
                return Path.GetDirectoryName(ProjectFilePath);
            }
        }

        public string Name { get; set; }

        public NuGetVersion Version { get; set; }

        public string Description { get; set; }

        public string[] Authors { get; set; }

        public string[] Owners { get; set; }

        public string ProjectUrl { get; set; }

        public string IconUrl { get; set; }

        public string LicenseUrl { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public string Copyright { get; set; }

        public string Language { get; set; }

        public string[] Tags { get; set; }

        public IList<LibraryDependency> Dependencies { get; set; }

        public IDictionary<string, IEnumerable<string>> Scripts { get; private set; }

        public List<TargetFrameworkInformation> TargetFrameworks { get; private set; }
    }
}
