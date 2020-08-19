// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat
{
    internal class ListPackageArgs
    {
        public ILogger Logger { get; }
        public string Path { get; }
        public IEnumerable<PackageSource> PackageSources { get; }
        public IEnumerable<string> Frameworks { get; }
        public bool OutdatedReport { get; }
        public bool DeprecatedReport { get; }
        public bool VulnerableReport { get; }
        public bool IsOffline { get; }
        public bool IncludeTransitive { get; }
        public bool Prerelease { get; internal set; }
        public bool HighestPatch { get; internal set; }
        public bool HighestMinor { get; internal set; }
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// A constructor for the arguments of list package
        /// command. This is used to execute the runner's
        /// method
        /// </summary>
        /// <param name="path"> The path to the solution or project file </param>
        /// <param name="packageSources"> The sources for the packages to check in the case of --outdated </param>
        /// <param name="frameworks"> The user inputed frameworks to look up for their packages </param>
        /// <param name="outdatedReport"> Bool for --outdated present </param>
        /// <param name="deprecatedReport"> Bool for --deprecated present </param>
        /// <param name="vulnerableReport"> Bool for --vulnerable present </param>
        /// <param name="isOffline"> Bool for --offline present </param>
        /// <param name="includeTransitive"> Bool for --include-transitive present </param>
        /// <param name="prerelease"> Bool for --include-prerelease present </param>
        /// <param name="highestPatch"> Bool for --highest-patch present </param>
        /// <param name="highestMinor"> Bool for --highest-minor present </param>
        /// <param name="logger"></param>
        /// <param name="cancellationToken"></param>
        public ListPackageArgs(
            string path,
            IEnumerable<PackageSource> packageSources,
            IEnumerable<string> frameworks,
            bool outdatedReport,
            bool deprecatedReport,
            bool vulnerableReport,
            bool isOffline,
            bool includeTransitive,
            bool prerelease,
            bool highestPatch,
            bool highestMinor,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            PackageSources = packageSources ?? throw new ArgumentNullException(nameof(packageSources));
            Frameworks = frameworks ?? throw new ArgumentNullException(nameof(frameworks));
            OutdatedReport = outdatedReport;
            DeprecatedReport = deprecatedReport;
            VulnerableReport = vulnerableReport;
            IsOffline = isOffline;
            IncludeTransitive = includeTransitive;
            Prerelease = prerelease;
            HighestPatch = highestPatch;
            HighestMinor = highestMinor;
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CancellationToken = cancellationToken;
        }
    }
}
