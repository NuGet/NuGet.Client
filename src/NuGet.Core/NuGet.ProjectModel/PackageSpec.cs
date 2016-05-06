// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NuGet.LibraryModel;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Represents the specification of a package that can be built.
    /// </summary>
    public class PackageSpec
    {
        public static readonly string PackageSpecFileName = "project.json";

        public PackageSpec(JObject rawProperties)
        {
            Scripts = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);
            PackInclude = new Dictionary<string, string>();
            TargetFrameworks = new List<TargetFrameworkInformation>();
            Properties = rawProperties;
        }

        public string FilePath { get; set; }

        public string BaseDirectory
        {
            get { return Path.GetDirectoryName(FilePath); }
        }

        public string Name { get; set; }

        public string Title { get; set; }

        private NuGetVersion _version;
        public NuGetVersion Version
        {
            get
            {
                return _version;
            }
            set
            {
                _version = value;
                this.IsDefaultVersion = false;
            }
        }
        public bool IsDefaultVersion { get; set; }

        public string Description { get; set; }

        public string Summary { get; set; }

        public string ReleaseNotes { get; set; }

        public string[] Authors { get; set; }

        public string[] Owners { get; set; }

        public string ProjectUrl { get; set; }

        public string IconUrl { get; set; }

        public string LicenseUrl { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public string Copyright { get; set; }

        public string Language { get; set; }

        public string[] Tags { get; set; }

        public IList<string> ContentFiles { get; set; }

        public IList<LibraryDependency> Dependencies { get; set; }

        public IList<ToolDependency> Tools { get; set; }

        public IDictionary<string, IEnumerable<string>> Scripts { get; private set; }

        public IDictionary<string, string> PackInclude { get; private set; }

        public PackOptions PackOptions { get; set; }

        public IList<TargetFrameworkInformation> TargetFrameworks { get; private set; }

        public RuntimeGraph RuntimeGraph { get; set; }

        /// <summary>
        /// Gets a list of all properties found in the package spec, including
        /// those not recognized by the parser.
        /// </summary>
        // TODO: Remove dependency on Newtonsoft.Json here.
        public JObject Properties { get; }
    }
}
