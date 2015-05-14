// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class RestoreRequest
    {
        public static readonly int DefaultDegreeOfConcurrency = 8;

        public RestoreRequest(PackageSpec project, IEnumerable<PackageSource> sources, string packagesDirectory)
        {
            Project = project;
            Sources = sources.ToList().AsReadOnly();
            PackagesDirectory = packagesDirectory;
            ExternalProjects = new List<ExternalProjectReference>();
            WriteMSBuildFiles = true;
        }

        /// <summary>
        /// The project to perform the restore on
        /// </summary>
        public PackageSpec Project { get; }

        /// <summary>
        /// The complete list of sources to retrieve packages from (excluding caches)
        /// </summary>
        public IReadOnlyList<PackageSource> Sources { get; }

        /// <summary>
        /// The directory in which to install packages
        /// </summary>
        public string PackagesDirectory { get; }

        /// <summary>
        /// A list of projects provided by external build systems (i.e. MSBuild)
        /// </summary>
        public IList<ExternalProjectReference> ExternalProjects { get; set; }

        /// <summary>
        /// The path to the lock file to read/write. If not specified, uses the file 'project.lock.json' in the same
        /// directory as the provided PackageSpec
        /// </summary>
        public string LockFilePath { get; set; }

        /// <summary>
        /// The number of concurrent tasks to run during installs. Defaults to
        /// <see cref="DefaultDegreeOfConcurrency" />. Set this to '1' to
        /// run without concurrency.
        /// </summary>
        public int MaxDegreeOfConcurrency { get; set; } = DefaultDegreeOfConcurrency;

        /// <summary>
        /// If set, ignore the cache when downloading packages
        /// </summary>
        public bool NoCache { get; set; }

        /// <summary>
        /// If set, MSBuild files (.targets/.props) will be written for the project being restored
        /// </summary>
        public bool WriteMSBuildFiles { get; set; }
    }
}
