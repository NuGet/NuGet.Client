// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Commands
{
    public class RestoreRequest : IDisposable
    {
        public static readonly int DefaultDegreeOfConcurrency = 16;

        public RestoreRequest(PackageSpec project, IEnumerable<PackageSource> sources, string packagesDirectory)
            : this(
                  project,
                  sources.Select(source => Repository.Factory.GetCoreV3(source.Source)),
                  packagesDirectory)
        {
        }

        public RestoreRequest(PackageSpec project, IEnumerable<SourceRepository> sources, string packagesDirectory)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            Project = project;

            ExternalProjects = new List<ExternalProjectReference>();
            CompatibilityProfiles = new HashSet<FrameworkRuntimePair>();

            PackagesDirectory = packagesDirectory;

            CacheContext = new SourceCacheContext();

            Sources = sources.ToList();
        }

        /// <summary>
        /// The project to perform the restore on
        /// </summary>
        public PackageSpec Project { get; }

        /// <summary>
        /// The complete list of sources to retrieve packages from (excluding caches)
        /// </summary>
        public IReadOnlyList<SourceRepository> Sources { get; }

        /// <summary>
        /// The directory in which to install packages
        /// </summary>
        public string PackagesDirectory { get; set; }

        /// <summary>
        /// A list of projects provided by external build systems (i.e. MSBuild)
        /// </summary>
        public IList<ExternalProjectReference> ExternalProjects { get; set; }

        /// <summary>
        /// The path to the lock file to read/write. If not specified, uses the file 'project.lock.json' in the same
        /// directory as the provided PackageSpec.
        /// </summary>
        public string LockFilePath { get; set; }

        /// <summary>
        /// The existing lock file to use. If not specified, the lock file will be read from the <see cref="LockFilePath"/>
        /// (or, if that property is not specified, from the default location of the lock file, as specified in the
        /// description for <see cref="LockFilePath"/>)
        /// </summary>
        public LockFile ExistingLockFile { get; set; }

        /// <summary>
        /// The number of concurrent tasks to run during installs. Defaults to
        /// <see cref="DefaultDegreeOfConcurrency" />. Set this to '1' to
        /// run without concurrency.
        /// </summary>
        public int MaxDegreeOfConcurrency { get; set; } = DefaultDegreeOfConcurrency;

        /// <summary>
        /// Cache settings
        /// </summary>
        public SourceCacheContext CacheContext { get; }

        /// <summary>
        /// Additional compatibility profiles to check compatibility with.
        /// </summary>
        public ISet<FrameworkRuntimePair> CompatibilityProfiles { get; }

        public void Dispose()
        {
            CacheContext.Dispose();
        }
    }
}
