// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.PackageExtraction;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    public class RestoreRequest
    {
        public static readonly int DefaultDegreeOfConcurrency = 16;
        
        public RestoreRequest(
            PackageSpec project,
            RestoreCommandProviders dependencyProviders,
            SourceCacheContext cacheContext,
            ILogger log)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (dependencyProviders == null)
            {
                throw new ArgumentNullException(nameof(dependencyProviders));
            }

            if (cacheContext == null)
            {
                throw new ArgumentNullException(nameof(cacheContext));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            Project = project;

            ExternalProjects = new List<ExternalProjectReference>();
            CompatibilityProfiles = new HashSet<FrameworkRuntimePair>();

            PackagesDirectory = dependencyProviders.GlobalPackages.RepositoryRoot;
            IsLowercasePackagesDirectory = true;

            CacheContext = cacheContext;
            Log = log;

            DependencyProviders = dependencyProviders;

            // Default to the project folder

            // Default to the project folder
            RestoreOutputPath = Path.GetDirectoryName(Project.FilePath);
        }

        public SourceCacheContext CacheContext { get; set; }

        public ILogger Log { get; set; }

        /// <summary>
        /// The project to perform the restore on
        /// </summary>
        public PackageSpec Project { get; }

        /// <summary>
        /// The directory in which to install packages
        /// </summary>
        public string PackagesDirectory { get; }

        /// <summary>
        /// Whether or not packages written and read from the global packages directory has
        /// lowercase ID and version folder names or original case.
        /// </summary>
        public bool IsLowercasePackagesDirectory { get; set; }

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
        /// Additional compatibility profiles to check compatibility with.
        /// </summary>
        public ISet<FrameworkRuntimePair> CompatibilityProfiles { get; }

        /// <summary>
        /// Lock file version format to output.
        /// </summary>
        /// <remarks>This defaults to the latest version.</remarks>
        public int LockFileVersion { get; set; } = LockFileFormat.Version;

        /// <summary>
        /// These Runtime Ids will be added to the graph in addition to the runtimes contained
        /// in project.json under runtimes.
        /// </summary>
        /// <remarks>RIDs are case sensitive.</remarks>
        public ISet<string> RequestedRuntimes { get; } = new SortedSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the <see cref="Packaging.PackageSaveMode"/>.
        /// </summary>
        public PackageSaveMode PackageSaveMode { get; set; } = PackageSaveMode.Defaultv3;

        /// <summary>
        /// These Runtime Ids will be used if <see cref="RequestedRuntimes"/> and the project runtimes
        /// are both empty.
        /// </summary>
        /// <remarks>RIDs are case sensitive.</remarks>
        public ISet<string> FallbackRuntimes { get; } = new SortedSet<string>(StringComparer.Ordinal);

        public XmlDocFileSaveMode XmlDocFileSaveMode { get; set; } = PackageExtractionBehavior.XmlDocFileSaveMode;

        /// <summary>
        /// This contains resources that are shared between project restores.
        /// This includes both remote and local package providers.
        /// </summary>
        public RestoreCommandProviders DependencyProviders { get; set; }

        /// <summary>
        /// Defines the paths and behavior for outputs
        /// </summary>
        public ProjectStyle ProjectStyle { get; set; } = ProjectStyle.Unknown;

        /// <summary>
        /// Restore output path
        /// </summary>
        public string RestoreOutputPath { get; set; }

    }
}
